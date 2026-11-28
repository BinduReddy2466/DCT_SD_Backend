using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
using DCT_SD.Models.Dtos.SdAutoConversion;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class ManualValidationService : IManualValidationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // How long a record stays locked to the user who opened it before it's treated as
    // abandoned (browser closed, network drop, etc.) and released automatically. Without this,
    // a lock nobody ever explicitly releases would block that record forever for everyone else.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRdFetchApiClient _rdFetchApiClient;
    private readonly ISdAutoConversionClient _sdAutoConversionClient;
    private readonly ILogger<ManualValidationService> _logger;

    public ManualValidationService(ApplicationDbContext context, ICurrentUserService currentUserService, IRdFetchApiClient rdFetchApiClient, ISdAutoConversionClient sdAutoConversionClient, ILogger<ManualValidationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _rdFetchApiClient = rdFetchApiClient;
        _sdAutoConversionClient = sdAutoConversionClient;
        _logger = logger;
    }

    private IQueryable<ManualValidationRequest> BuildSearchQuery(ManualValidationSearchRequestDto request, bool excludeReadyForMigration = false)
    {
        var query = _context.ManualValidationRequests.AsNoTracking().Where(r => r.MigratedAt == null);

        if (excludeReadyForMigration)
        {
            query = query.Where(r => r.Status != ManualValidationStatus.ReadyForMigration);
        }

        if (!string.IsNullOrWhiteSpace(request.RdCode))
        {
            query = query.Where(r => r.RdCode == request.RdCode);
        }

        if (!string.IsNullOrWhiteSpace(request.RequestNumber))
        {
            var term = request.RequestNumber.Trim().ToLower();
            query = query.Where(r => r.RequestNumber.ToLower().Contains(term));
        }

        // The "Entry Number" search box and the Results list column both filter/display
        // EntryNumberRange now (2026-11-03, per client request) - not EntryNumbersCsv - so a
        // search always matches the same value the grouping itself and the list column show,
        // instead of searching one field while rows are grouped/displayed by a different one.
        // The request/query-string field name stays EntryNumbersCsv (no URL/JS contract change);
        // only which database column it searches against has moved.
        if (!string.IsNullOrWhiteSpace(request.EntryNumbersCsv))
        {
            var term = request.EntryNumbersCsv.Trim().ToLower();
            query = query.Where(r => r.EntryNumberRange != null && r.EntryNumberRange.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var term = request.Title.Trim().ToLower();
            query = query.Where(r => r.Title != null && r.Title.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ManualValidationStatus>(request.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.ExtractionDate >= DateRangeFilter.StartOfDayUtc(request.DateFrom.Value));
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.ExtractionDate < DateRangeFilter.EndOfDayExclusive(request.DateTo.Value));
        }

        return query;
    }

    // Level 1 ONLY (2026-11-16, Entry Number-based consolidation across processing instances -
    // supersedes the 2026-11-16 folder-wise grouping fix, which incorrectly made FolderPath part
    // of this key): rows sharing the exact same (RdCode, EntryNumbersCsv) pair become one UI row -
    // never EntryNumbersCsv alone (the same Entry Number can recur under a different RD) and never
    // RdCode alone (one RD has many unrelated Entry Numbers). EntryNumberRange is a separate field
    // (the Details page's own editable "Entry Number" display field - see SaveAsync) and plays no
    // part in this key. A blank/whitespace EntryNumbersCsv (NULL included), or a blank RdCode,
    // never groups - each such row is identified independently by its own Id.
    //
    // FolderPath is deliberately NOT part of this key: the same Entry Number can legitimately be
    // processed multiple times under different physical folders (different OcrExtractionRecords
    // rows), and every one of those processing instances must still consolidate into this SAME
    // overall group, not split into separate ones just because the folder changed. See
    // GetEntryNumberGroupAsync/PartitionIntoProcessingInstances for how FolderPath/
    // OcrExtractionRecordId are instead used as a SECOND, narrower level - which processing
    // instance (and therefore which DocumentsJson) a given row's documents come from - entirely
    // within this broader group, never to decide what belongs in it.
    private static List<List<ManualValidationRequest>> GroupMatchingRecords(List<ManualValidationRequest> matchingRecords) =>
        matchingRecords
            .GroupBy(r => string.IsNullOrWhiteSpace(r.RdCode) || string.IsNullOrWhiteSpace(r.EntryNumbersCsv)
                ? $"__row:{r.Id}"
                : $"{r.RdCode}|{r.EntryNumbersCsv}")
            .Select(g => g.OrderBy(r => r.Id).ToList())
            .ToList();

    public async Task<PagedResult<ManualValidationListItemDto>> SearchAsync(ManualValidationSearchRequestDto request, bool excludeReadyForMigration = false, CancellationToken cancellationToken = default)
    {
        // Every filter above still runs server-side exactly as before; only the grouping/paging
        // step below happens in memory, over the already-filtered set. Manual Validation is a
        // human review queue (bounded, not a bulk data table), so this trades a small, predictable
        // amount of extra memory for grouping logic that's simple to read and audit - the same
        // pragmatic in-memory-filter approach already used elsewhere in this service (see
        // RetrieveTitleSequenceAsync).
        var matchingRecords = await BuildSearchQuery(request, excludeReadyForMigration).ToListAsync(cancellationToken);
        var groups = GroupMatchingRecords(matchingRecords);

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = groups.Count;
        // Each group's earliest (lowest Id) row is its representative for every displayed column
        // and for the View link's target id - the same "Title Record 1" row Details.cshtml already
        // treats as the group's primary record, so View immediately expands back to the full group.
        var pageItems = groups
            .OrderByDescending(g => g[0].ExtractionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => MapToListItem(g[0]))
            .ToArray();

        return new PagedResult<ManualValidationListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public async Task<List<ManualValidationListItemDto>> SearchAllAsync(ManualValidationSearchRequestDto request, bool excludeReadyForMigration = false, CancellationToken cancellationToken = default)
    {
        var matchingRecords = await BuildSearchQuery(request, excludeReadyForMigration).ToListAsync(cancellationToken);
        var groups = GroupMatchingRecords(matchingRecords);

        return groups
            .OrderByDescending(g => g[0].ExtractionDate)
            .Select(g => MapToListItem(g[0]))
            .ToList();
    }

    public async Task<PagedResult<ManualValidationRemarkDto>> GetRemarksHistoryAsync(int id, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.RecordHistory.AsNoTracking()
            .Where(r => r.TableName == RecordHistoryTables.ManualValidationRequests && r.RecordId == id);

        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize is < 1 or > 100 ? 25 : pageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ManualValidationRemarkDto>
        {
            Items = items.Select(MapToRemarkDto).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    private static ManualValidationListItemDto MapToListItem(ManualValidationRequest r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        RdCode = r.RdCode,
        RdName = r.RdName,
        // The Results list's "ENTRY NUMBER" column shows EntryNumberRange now (2026-11-03, per
        // client request), not EntryNumbersCsv - the DTO property keeps its existing name
        // (EntryNumbersCsv) to avoid an unrelated rename across the view/JS, but the value it
        // carries is EntryNumberRange, the same field the row grouping above is based on.
        EntryNumbersCsv = r.EntryNumberRange,
        Title = r.Title,
        TitleType = r.TitleType?.ToString(),
        Status = r.Status.ToString(),
        MissingFields = r.MissingFieldsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ExtractionDate = r.ExtractionDate,
        UpdatedBy = r.UpdatedByUsername,
        UpdatedDate = r.UpdatedAt,
    };

    private static ManualValidationRemarkDto MapToRemarkDto(RecordHistory r) => new()
    {
        Id = r.Id,
        UpdatedAt = r.CreatedAt,
        By = r.ByUsername ?? string.Empty,
        Remarks = r.Remarks ?? string.Empty,
        Action = r.Action,
    };

    public async Task<ManualValidationDetailDto> OpenForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        record.LockedByUserId = _currentUserService.UserId;
        record.LockedByUsername = _currentUserService.Username;
        record.LockedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var group = await GetEntryNumberGroupAsync(record, cancellationToken);
        return MapToDetail(record, group);
    }

    public async Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        // Every row sharing this record's RdCode + EntryNumbersCsv - the Level-1 Entry Number
        // group (see GetEntryNumberGroupAsync), spanning every processing instance/folder, not
        // just this record's own. Locking stays scoped to `record` alone
        // (EnsureNotLockedByAnotherUser above only checked that one row); group siblings are
        // written here regardless of their own lock state, same as today's single-record Save
        // never checked a second row's lock.
        var group = await GetEntryNumberGroupAsync(record, cancellationToken);
        var groupIds = group.Select(r => r.Id).ToHashSet();

        var changeDescriptionsByRecordId = new Dictionary<int, List<string>>();
        List<string> ChangesFor(int recordId) =>
            changeDescriptionsByRecordId.TryGetValue(recordId, out var list) ? list : changeDescriptionsByRecordId[recordId] = [];

        // RD Code / RD Name / Entry Number are shown once in General Information, not per Title
        // Record, so an edit there applies to every row in the group. RdCode is overwritten
        // uniformly here, which keeps the group's RdCode half of the (RdCode, EntryNumberRange)
        // key consistent.
        //
        // The "Entry Number" field (posted as request.EntryNumbersCsv - the form/DTO property
        // name is unchanged to avoid an unrelated rename across the view/JS) is now written into
        // EntryNumberRange, not EntryNumbersCsv (2026-11-03, per client request: the Details page
        // displays and edits EntryNumberRange directly, since that's the same field the grouping
        // itself uses - editing "Entry Number" now directly changes which group a record belongs
        // to, instead of changing a separate display-only field that never affected grouping).
        // EntryNumberRange is NOT NULL in the database, so a cleared field is written as "" (an
        // empty string still satisfies "blank/whitespace means independent" in GetGroupRecordsAsync/
        // GroupMatchingRecords), never null. EntryNumbersCsv itself is no longer written by this
        // Save - it keeps whatever value it already had (set only at record-creation time by the
        // external extraction pipeline).
        var newRdCode = request.RdCode?.Trim();
        var newEntryNumberRange = request.EntryNumbersCsv?.Trim() ?? string.Empty;
        var newRdName = string.IsNullOrWhiteSpace(newRdCode)
            ? null
            : await _context.CodeLookups.AsNoTracking()
                .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.Code == newRdCode)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        foreach (var r in group)
        {
            var originalRdCode = r.RdCode;
            var originalEntryNumberRange = r.EntryNumberRange;

            r.RdCode = newRdCode;
            r.EntryNumberRange = newEntryNumberRange;
            r.RdName = newRdName;

            AddFieldChange(ChangesFor(r.Id), "RD Code", originalRdCode, r.RdCode);
            AddFieldChange(ChangesFor(r.Id), "Entry Number", originalEntryNumberRange, r.EntryNumberRange);
        }

        // Required change: once a record flagged "Target RD Not Identified" (the status the
        // external fetch service assigns when it can't match a target RD) gets a real RD Code from
        // this Save, it no longer belongs in that status - move it to "Incomplete Extraction"
        // instead, since the rest of its data may still be incomplete. Scoped to `record` alone,
        // never other rows in `group`. Set on the tracked entity here (not via a separate
        // SaveChangesAsync), so it commits atomically with everything else below - if this Save
        // throws before the SaveChangesAsync call further down, nothing is persisted, including
        // this, matching "if saving fails, do not update the status."
        if (record.Status == ManualValidationStatus.TargetRdNotIdentified && !string.IsNullOrWhiteSpace(newRdCode))
        {
            var originalStatus = record.Status;
            record.Status = ManualValidationStatus.IncompleteExtraction;
            AddFieldChange(ChangesFor(record.Id), "Status", originalStatus.ToString(), record.Status.ToString());
        }

        // Each Title Record targets exactly one underlying row by RecordId - never by position/
        // order - and only a RecordId that is actually a member of this group is honored, so a
        // stale or tampered payload can never write Title Record fields to an unrelated record.
        var titleRecordLabelByRecordId = group
            .Select((r, index) => (r.Id, Label: $"Title Record {index + 1}"))
            .ToDictionary(x => x.Id, x => x.Label);

        foreach (var item in request.TitleRecords)
        {
            if (!groupIds.Contains(item.RecordId))
            {
                continue;
            }

            var r = group.First(x => x.Id == item.RecordId);
            var label = titleRecordLabelByRecordId[r.Id];

            var originalTitle = r.Title;
            var originalTitleType = r.TitleType;
            var originalPlan = r.Plan;
            var originalBlock = r.Block;
            var originalLot = r.Lot;
            var originalTitleSequence = r.TitleSequence;

            r.Title = item.Title?.Trim();
            r.TitleType = Enum.TryParse<TitleType>(item.TitleType, true, out var titleType) ? titleType : null;
            r.Plan = item.Plan?.Trim();
            r.Block = item.Block?.Trim();
            r.Lot = item.Lot?.Trim();
            r.TitleSequence = item.TitleSequence?.Trim();

            var changes = ChangesFor(r.Id);
            AddFieldChange(changes, $"{label} - Title Number", originalTitle, r.Title);
            AddFieldChange(changes, $"{label} - Title Type", originalTitleType?.ToString(), r.TitleType?.ToString());
            AddFieldChange(changes, $"{label} - Plan Number", originalPlan, r.Plan);
            AddFieldChange(changes, $"{label} - Block Number", originalBlock, r.Block);
            AddFieldChange(changes, $"{label} - Lot Number", originalLot, r.Lot);
            AddFieldChange(changes, $"{label} - Title Sequence", originalTitleSequence, r.TitleSequence);
        }

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        foreach (var r in group)
        {
            r.MissingFieldsCsv = string.Join(',', ComputeMissingFields(r));
            r.UpdatedByUserId = _currentUserService.UserId;
            r.UpdatedByUsername = displayName;
            r.UpdatedAt = DateTime.UtcNow;
        }

        // Others -> real Document Type correction(s): the physical file(s) are renamed (never the
        // containing folder) and DocumentsJson updated only here, as part of Save itself - never
        // when a dropdown selection changes. renamesToRollBack is populated as each rename
        // actually happens (not just returned at the end) and this whole block - including
        // ApplyDocumentTypeChanges itself, not only the SaveChangesAsync call after it - is
        // covered by the catch below, so a rename that fails partway through a multi-change Save
        // still correctly undoes whichever earlier renames in this same Save already succeeded,
        // and if the database save then fails for some other reason every rename applied in this
        // Save is undone too - the file(s), DocumentsJson, and imagePath(s) stay consistent either way.
        var renamesToRollBack = new List<(string NewPath, string OriginalPath)>();
        var touchedRecordIds = new HashSet<int>();
        try
        {
            if (!string.IsNullOrWhiteSpace(request.DocumentChangesJson))
            {
                var documentChanges = (JsonSerializer.Deserialize<List<DocumentChangeItemDto>>(request.DocumentChangesJson, JsonOptions) ?? [])
                    .Where(c => c.Index > 0 && !string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.Name))
                    .ToList();

                if (documentChanges.Count > 0)
                {
                    ApplyDocumentTypeChanges(record, group, documentChanges, renamesToRollBack, changeDescriptionsByRecordId, touchedRecordIds);
                }
            }

            // Built last, once every scalar and Supporting Document change has been recorded -
            // never the old generic message when real changes exist, and never a fabricated
            // change description when nothing actually changed. The opened record (`record`)
            // always gets a RecordHistory row even with zero changes, matching the single-record
            // behavior this replaces; a group sibling only gets one when something on that
            // specific row actually changed, so Saving one Title Record doesn't spam history onto
            // every other related row.
            foreach (var r in group)
            {
                var changes = changeDescriptionsByRecordId.TryGetValue(r.Id, out var list) ? list : [];
                if (r.Id != record.Id && changes.Count == 0 && !touchedRecordIds.Contains(r.Id))
                {
                    continue;
                }

                _context.RecordHistory.Add(new RecordHistory
                {
                    TableName = RecordHistoryTables.ManualValidationRequests,
                    RecordId = r.Id,
                    RefNo = r.RequestNumber,
                    Action = RemarkAction.Saved.ToString(),
                    Remarks = BuildChangeRemarks(changes),
                    ByUserId = _currentUserService.UserId,
                    ByUsername = displayName ?? "system",
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var rollback in renamesToRollBack)
            {
                if (System.IO.File.Exists(rollback.NewPath))
                {
                    System.IO.File.Move(rollback.NewPath, rollback.OriginalPath);
                }
            }

            throw;
        }

        // Additional to the existing Save flow above - never replaces or changes it. Runs only
        // after SaveChangesAsync has fully committed (and only reached at all if the try block
        // above succeeded - an exception there propagates out of SaveAsync before this point, so
        // the existing rollback-and-rethrow behavior on a failed Save is unaffected). This has to
        // run AFTER the existing Save, not before: the external service writes document_json for
        // this record directly, and calling it any earlier meant our own SaveChangesAsync above -
        // still holding this entity exactly as loaded at the top, before the external write -
        // would flush that original DocumentsJson back over whatever the external service had
        // just written, silently reverting it. A failure here is logged by RdFetchApiClient
        // itself and swallowed - it must never fail a Save that has already succeeded.
        //
        // Matching key fixed to RdCode + EntryNumberRange (2026-11-03) - this used to compare/
        // query on EntryNumbersCsv, which was correct back when EntryNumbersCsv WAS the grouping
        // key, but became stale once the grouping key changed to EntryNumberRange without this
        // block being updated to match (the regression). NOTE (2026-11-16): this block is about
        // the EXTERNAL RD Fetch service's own "merge entry" concept, a different, narrower concern
        // from this app's own Entry Number consolidation grouping (GetEntryNumberGroupAsync, keyed
        // on RdCode + EntryNumbersCsv) - it intentionally stays keyed on EntryNumberRange, the
        // field this Details page's own "Entry Number" box edits, not on EntryNumbersCsv.
        // EntryNumberRange is now written by this Save itself (the mutation loop above, from the
        // "Entry Number" field), so this check always runs against the record's just-saved value,
        // not a stale pre-edit snapshot. A blank/whitespace EntryNumberRange means this record has
        // nothing to merge, so the whole block is skipped for it. Otherwise, any active row outside
        // this record's own Entry Number group that already shares the same (RdCode,
        // EntryNumberRange) is a genuine merge candidate - this is always re-checked fresh here,
        // never inferred from "did a specific field change".
        if (!string.IsNullOrWhiteSpace(record.EntryNumberRange))
        {
            // The external service's own match query excludes any row that is currently locked
            // (LockedByUserId not null) - both this record (locked by the current user for the
            // whole edit session, set by OpenForEditAsync, never cleared by Save) and the
            // pre-existing row it needs to merge into, if that one happens to be locked too (e.g.
            // left open in another tab). Left as-is, either lock makes the external service unable
            // to find/merge into the match, so every lock on a plausible match (this record, plus
            // any other active row sharing the new RdCode + EntryNumberRange) is released here,
            // the call is made, then all of them are restored afterward - everyone's "being
            // reviewed" state must be exactly what it was once this finishes, same as before this
            // call. groupIds (not just record.Id) is excluded, since every sibling in this
            // record's own group trivially shares the same EntryNumberRange already and is never
            // itself a merge candidate.
            var lockedByUserId = record.LockedByUserId;
            var lockedByUsername = record.LockedByUsername;
            var lockedAt = record.LockedAt;

            var candidateTargets = await _context.ManualValidationRequests
                .Where(r => !groupIds.Contains(r.Id)
                    && r.MigratedAt == null
                    && r.RdCode == newRdCode
                    && r.EntryNumberRange == record.EntryNumberRange
                    && r.LockedByUserId != null)
                .ToListAsync(cancellationToken);
            var candidateLockState = candidateTargets
                .Select(r => (r.Id, r.LockedByUserId, r.LockedByUsername, r.LockedAt))
                .ToList();

            record.LockedByUserId = null;
            record.LockedByUsername = null;
            record.LockedAt = null;
            foreach (var candidate in candidateTargets)
            {
                candidate.LockedByUserId = null;
                candidate.LockedByUsername = null;
                candidate.LockedAt = null;
            }

            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                await _rdFetchApiClient.MergeEntryAsync(newRdCode ?? string.Empty, record.EntryNumberRange ?? string.Empty, record.RequestNumber, cancellationToken);
            }
            catch (BusinessValidationException)
            {
                // Logged already inside RdFetchApiClient - swallowed here so this additional,
                // best-effort call never fails an otherwise-successful Save.
            }
            finally
            {
                foreach (var (candidateId, userId, username, at) in candidateLockState)
                {
                    var candidate = candidateTargets.First(r => r.Id == candidateId);
                    candidate.LockedByUserId = userId;
                    candidate.LockedByUsername = username;
                    candidate.LockedAt = at;
                }

                record.LockedByUserId = lockedByUserId;
                record.LockedByUsername = lockedByUsername;
                record.LockedAt = lockedAt;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return MapToDetail(record, group);
    }

    // Appends "<label>: Previous = '<before>', Current = '<after>'" to `changes` - but only when
    // the value actually changed, per the acceptance criteria ("do not record fields that did not
    // change"). Null/blank values are shown as "Empty" so the history entry never looks like it's
    // missing data.
    private static void AddFieldChange(List<string> changes, string label, string? before, string? after)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add($"{label}: Previous = '{FormatValueForHistory(before)}', Current = '{FormatValueForHistory(after)}'");
    }

    private static string FormatValueForHistory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Empty" : value;

    // RecordHistory.Remarks is capped at 500 characters (see RecordHistoryConfiguration) - a Save
    // touching many fields/documents could otherwise generate a message long enough to make the
    // audit log itself the reason a legitimate Save fails, so this always fits within that limit.
    private const int RemarksMaxLength = 500;

    private static string BuildChangeRemarks(List<string> changeDescriptions)
    {
        if (changeDescriptions.Count == 0)
        {
            return "No changes were made during manual validation.";
        }

        var combined = string.Join('\n', changeDescriptions);
        if (combined.Length <= RemarksMaxLength)
        {
            return combined;
        }

        const string suffix = "...";
        return combined[..(RemarksMaxLength - suffix.Length)] + suffix;
    }

    // Applies one or more pending Others -> real Document Type corrections in a single Save.
    // Every change's target document is resolved up front, against ONE pre-change sorted snapshot
    // of the OPENED record's OWN DocumentsJson (see MapToDetail - the exact same source/sort that
    // assigned each document's client-facing Id, since display no longer merges across the
    // group) - because renaming a document changes its RenamedFileName (Image File Name), which is
    // the sort key, resolving each change's index one at a time as it's applied would let an
    // earlier rename in this same batch shift the sort order and cause a later change to silently
    // target the wrong document.
    // rollbacks is populated in place as each rename succeeds - if a later change in this same call throws,
    // the caller still has every rollback recorded so far. changeDescriptions gets one "Supporting
    // Document '<original file name>' - Document Type: Previous = '...', Current = '...'" line,
    // recorded against whichever underlying row actually owns that document, for the same
    // RecordHistory.Remarks audit trail as the scalar field changes.
    private static void ApplyDocumentTypeChanges(
        ManualValidationRequest primary,
        List<ManualValidationRequest> group,
        List<DocumentChangeItemDto> changes,
        List<(string NewPath, string OriginalPath)> rollbacks,
        Dictionary<int, List<string>> changeDescriptionsByRecordId,
        HashSet<int> touchedRecordIds)
    {
        // Must match MapToDetail's displayDocuments ordering EXACTLY - change.Index is a 1-based
        // position in that SAME combined, multi-processing-instance list (BuildCombinedDocuments),
        // not just the opened record's own instance - a document shown at a given position can
        // now come from any processing instance in the whole Entry Number group.
        var combined = BuildCombinedDocuments(group);
        var instances = PartitionIntoProcessingInstances(group);
        var instanceByRepresentativeId = instances.ToDictionary(inst => inst[0].Id, inst => inst);

        var targets = new List<(DocumentJsonItem Target, int OwnerRecordId, DocumentChangeItemDto Change)>();
        foreach (var change in changes)
        {
            if (change.Index - 1 < combined.Count)
            {
                var (item, ownerRecordId) = combined[change.Index - 1];
                targets.Add((item, ownerRecordId, change));
            }
        }

        foreach (var (target, ownerRecordId, change) in targets)
        {
            // A correction never crosses a processing-instance boundary: it only ever propagates
            // to the SAME instance's own sibling rows (every one of which is guaranteed to carry
            // an identical copy of this exact document - spec: "identical DocumentsJson values are
            // repeated across sibling records"), never into a different instance's distinct,
            // unrelated document set, even though both instances sit in the same broader Entry
            // Number group.
            var instanceMembers = instanceByRepresentativeId[ownerRecordId];
            var perRecordItems = instanceMembers.Select(r => (RecordId: r.Id, Items: ParseDocumentItems(r.DocumentsJson))).ToList();
            var instanceItemsFlat = perRecordItems.SelectMany(x => x.Items).ToList();

            var originalIdentityKey = DocumentIdentityKey(target);

            // Every item across this ONE processing instance that shares this document's identity
            // (DocumentIdentityKey - RD Code/Entry Folder/Document Type Folder/File Name) - always
            // includes `target` itself. Grouped by each item's ORIGINAL absolute ImagePath
            // (captured now, before any renames below): a physical rename only ever needs to
            // happen ONCE per distinct physical file. Two rows from the SAME instance commonly
            // cite the literal same file (e.g. a shared "Others" bucket file, which is why
            // `target` can itself share its original path with another member) - renaming that
            // file a second time would fail (it's already been moved by the first rename), so
            // only ONE member per path group is actually renamed (preferring `target`'s own row
            // when it's a member of that group, so the plain, unsuffixed audit line still lands
            // on the row the user directly edited); every other member in that same group just
            // gets its fields copied from whichever one was actually renamed.
            var allMatching = perRecordItems
                .SelectMany(x => x.Items.Select(item => (RecordId: x.RecordId, Item: item)))
                .Where(x => ReferenceEquals(x.Item, target) || string.Equals(DocumentIdentityKey(x.Item), originalIdentityKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var pathGroup in allMatching.GroupBy(x => x.Item.ImagePath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var members = pathGroup.ToList();
                var renameIndex = members.FindIndex(x => ReferenceEquals(x.Item, target));
                if (renameIndex < 0)
                {
                    renameIndex = 0;
                }

                var renamedItem = members[renameIndex].Item;
                // Captured before ApplyDocumentTypeChange mutates `renamedItem` in place, so the
                // audit line below always identifies the document by its ORIGINAL file name and
                // shows its ORIGINAL classification - never the values already being changed to.
                var groupOriginalFileName = renamedItem.RenamedFileName;
                var groupOriginalName = renamedItem.DocumentName;

                // Sequence numbers are scanned across every document in THIS processing instance
                // (instanceItemsFlat), not just this document's own owning row and never a
                // different instance's own, otherwise-unrelated files - two genuinely different
                // physical files reclassified to the same Document Type in one Save never collide
                // on the same sequence number, and two different instances' own "Others" buckets
                // never interfere with each other's numbering either.
                var groupRollback = ApplyDocumentTypeChange(instanceItemsFlat, renamedItem, change.Code.Trim(), change.Name.Trim());
                if (groupRollback is { } gr)
                {
                    rollbacks.Add(gr);
                }

                // ApplyDocumentTypeChange leaves `renamedItem` untouched (documentName included)
                // when there was nothing to apply (e.g. a missing ImagePath) - comparing
                // before/after here is the simplest way to only record documents that were
                // actually reclassified.
                if (!string.Equals(renamedItem.DocumentName, groupOriginalName, StringComparison.Ordinal))
                {
                    for (var i = 0; i < members.Count; i++)
                    {
                        if (i == renameIndex)
                        {
                            continue;
                        }

                        var dupItem = members[i].Item;
                        dupItem.DocumentId = renamedItem.DocumentId;
                        dupItem.DocumentName = renamedItem.DocumentName;
                        dupItem.RenamedFileName = renamedItem.RenamedFileName;
                        dupItem.ImagePath = renamedItem.ImagePath;
                        dupItem.DocumentTypeCode = renamedItem.DocumentTypeCode;
                    }

                    foreach (var (memberRecordId, memberItem) in members)
                    {
                        touchedRecordIds.Add(memberRecordId);
                        var descriptions = changeDescriptionsByRecordId.TryGetValue(memberRecordId, out var list) ? list : changeDescriptionsByRecordId[memberRecordId] = [];
                        var suffix = ReferenceEquals(memberItem, target) ? string.Empty : " (same document, also reclassified via another Title Record)";
                        descriptions.Add(
                            $"Supporting Document '{groupOriginalFileName}' - Document Type: Previous = '{FormatValueForHistory(groupOriginalName)}', Current = '{renamedItem.DocumentName}'{suffix}");
                    }
                }
            }

            // Only this instance's touched member rows are re-serialized/written back here - a
            // document never targeted, or belonging to a row nothing in THIS instance touched, is
            // left completely alone; a different processing instance's own rows are never even
            // considered, let alone written to.
            foreach (var (recordId, items) in perRecordItems)
            {
                if (!touchedRecordIds.Contains(recordId))
                {
                    continue;
                }

                var owner = instanceMembers.First(r => r.Id == recordId);
                owner.DocumentsJson = JsonSerializer.Serialize(items, JsonOptions);
            }
        }
    }

    // Renames one document's physical image file to
    // "<DocumentID>_<DocumentName>_<SequenceNumber><extension>" - SequenceNumber is always one
    // past the highest existing sequence number already used by this record's OTHER documents
    // that share this exact Document ID + Document Name (or 1 if none do; see
    // NextDocumentSequenceNumber) - and updates the matching item's
    // documentId/documentName/renamedFileName/imagePath in `items` (the caller re-serializes
    // `items` back into record.DocumentsJson once every requested change has been applied, so a
    // second change targeting the same Document Type in this same batch sees the first change's
    // rename and is assigned the next number after it).
    private static (string NewPath, string OriginalPath)? ApplyDocumentTypeChange(List<DocumentJsonItem> items, DocumentJsonItem target, string newCode, string newName)
    {
        if (string.IsNullOrWhiteSpace(target.ImagePath))
        {
            return null;
        }

        if (!System.IO.File.Exists(target.ImagePath))
        {
            throw new BusinessValidationException("The original image file could not be found on disk. The Document Type change was not saved.");
        }

        var directory = Path.GetDirectoryName(target.ImagePath)!;
        var extension = Path.GetExtension(target.ImagePath);
        var sequence = NextDocumentSequenceNumber(items, newCode, newName);
        var newFileName = $"{SanitizeForFileName(newCode)}_{SanitizeForFileName(newName)}_{sequence}{extension}";
        var newPath = Path.Combine(directory, newFileName);

        (string NewPath, string OriginalPath)? rollback = null;
        if (!string.Equals(newPath, target.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            System.IO.File.Move(target.ImagePath, newPath);
            rollback = (newPath, target.ImagePath);
        }

        target.DocumentId = newCode;
        target.DocumentName = newName;
        target.RenamedFileName = newFileName;
        target.ImagePath = newPath;

        // Permanent "this document was corrected from Others" marker, so it stays correctable
        // through this same dropdown even after being reclassified and saved - if the user picks
        // the wrong type, they can still fix it later without needing to go back to "Others"
        // first (which is no longer possible once it's been renamed away from it). Reuses
        // documentTypeCode - already part of DocumentsJson's shape but never otherwise populated
        // or read by this app - rather than adding a new field. See IsOthersEligible.
        target.DocumentTypeCode = "OTHERS";

        return rollback;
    }

    // A document is correctable through the Document Type dropdown when it's still "OTHERS"
    // (needs its first correction) or was ever corrected from "OTHERS" in the past (marked via
    // DocumentTypeCode - see ApplyDocumentTypeChange) - never for a document that was extracted
    // as a real type directly and has never been "OTHERS".
    private static bool IsOthersEligible(DocumentJsonItem item) =>
        string.Equals(item.DocumentId, "OTHERS", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.DocumentTypeCode, "OTHERS", StringComparison.OrdinalIgnoreCase);

    // Scans every supporting document across the WHOLE processing instance (every Title Record
    // sharing this document's own OcrExtractionRecordId/folder - see ApplyDocumentTypeChanges's
    // `instanceItemsFlat` - never a different processing instance in the same broader Entry
    // Number group) for ones already classified under the exact same Document ID + Document Name,
    // extracts the trailing "_<number>" sequence from their renamedFileName (matching
    // "<DocumentID>_<DocumentName>_<N>.<extension>"), and returns the highest one found, plus 1 -
    // or 1 if none match. Gaps are preserved on purpose (existing _1 and _4 -> next is _5, not
    // _2) since this always takes the true maximum, never the first free slot.
    //
    // Deliberately scans the whole instance, not just the file's own folder: a document being
    // corrected from Others always renames within the "Others" bucket folder, which is a
    // different folder than a genuine document of the same real type that was extracted directly
    // (each Document Type gets its own folder) - scoping the scan to one folder would miss that
    // existing document entirely and could generate a filename that already exists elsewhere,
    // which is exactly the wrong-sequence-number/visible-collision bug this must avoid. Never
    // scans beyond this one instance - a different processing instance's own "Others" bucket is a
    // physically different folder and must never influence this instance's own numbering.
    private static int NextDocumentSequenceNumber(List<DocumentJsonItem> items, string code, string name)
    {
        var pattern = new Regex("^" + Regex.Escape(SanitizeForFileName(code)) + "_" + Regex.Escape(SanitizeForFileName(name)) + @"_(\d+)\.[^.]+$", RegexOptions.IgnoreCase);
        var max = 0;
        foreach (var item in items)
        {
            if (!string.Equals(item.DocumentId, code, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(item.DocumentName, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = pattern.Match(item.RenamedFileName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > max)
            {
                max = n;
            }
        }

        return max + 1;
    }

    // A CodeLookups Document Name can contain characters that are invalid in a file name (e.g.
    // "Tax Declaration on Improvement (Certified Copy)/Certificate of No Improvement" has a "/"),
    // so the generated renamedFileName replaces them with "_" - matching the same substitution
    // the OCR pipeline's own file names already use for these exact document types. Only the
    // physical/generated file name is sanitized this way; the documentName value stored in
    // DocumentsJson keeps the original, unmodified CodeLookups Name.
    private static string SanitizeForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    public async Task CloseAsync(int id, string remarks, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessValidationException("Remarks is required.");
        }

        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        // Closing is itself a save on the record (it's the action that finalizes it), so it
        // must update Updated By/Date the same way SaveAsync does - otherwise a record closed
        // without ever going through the separate Save button keeps showing whatever (possibly
        // stale, possibly never-set) values it had before.
        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        record.UpdatedByUserId = _currentUserService.UserId;
        record.UpdatedByUsername = displayName;
        record.UpdatedAt = DateTime.UtcNow;

        // Closing releases the "opened for edit" lock (OpenForEditAsync sets it on every Details
        // view) - without this, a record the closing user simply viewed and then closed stays
        // reported as locked to them for the rest of the 15-minute LockTimeout, blocking any
        // other user from opening it even though nobody is actually still editing it.
        record.LockedByUserId = null;
        record.LockedByUsername = null;
        record.LockedAt = null;

        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.ManualValidationRequests,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = RemarkAction.Closed.ToString(),
            Remarks = remarks.Trim(),
            ByUserId = _currentUserService.UserId,
            ByUsername = displayName ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Releases every Manual Validation review lock currently held by this user, regardless of
    // which record it's on - called from AccountController.Logout so a user who logs out without
    // explicitly clicking Close doesn't leave records reported as locked to them for the rest of
    // the LockTimeout window. Uses the exact same LockedByUserId/LockedByUsername/LockedAt fields
    // CloseAsync already clears - no new locking mechanism, just the existing one applied at a
    // different point in the flow. A record already past LockTimeout is naturally already
    // unlocked from EnsureNotLockedByAnotherUser's perspective, but clearing it here too keeps the
    // stored state honest rather than leaving stale values around until someone else opens it.
    public async Task ReleaseLocksForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var lockedRecords = await _context.ManualValidationRequests
            .Where(r => r.LockedByUserId == userId && r.MigratedAt == null)
            .ToListAsync(cancellationToken);

        if (lockedRecords.Count == 0)
        {
            return;
        }

        foreach (var record in lockedRecords)
        {
            record.LockedByUserId = null;
            record.LockedByUsername = null;
            record.LockedAt = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Replaces the old "Migrate" action. Flags the record as ready, then creates its own
    // MigrationRecords row(s) (MigrationType=ManualValidation, one per entry number) so the Entry
    // Record immediately becomes visible on Migration Monitoring - this app's only writer into
    // that table now (client requirement, 2026-11-27: Migration Monitoring is view-only and no
    // longer fed directly from OcrExtractionRecords). Returns the newly created rows' ids so the
    // caller can kick off migration for each of them right away, in the background - see
    // ManualValidationController.ReadyForMigration and MigrationService.
    // ProcessEntryNumbersSequentiallyAsync. MigrationStatus/SdStatus/TargetSystemValidation/
    // TargetSystemActivationStatus stay at their "awaiting outcome" values (ReadyForMigration /
    // null / null / null) here - the actual outcome isn't known until that background processing
    // (or a later Reprocess) completes. Deliberately never sets MigratedAt itself (that's reserved
    // for when a real migration outcome exists). Processes the WHOLE Entry Number group the clicked record belongs to
    // (RdCode + EntryNumbersCsv, spanning every processing instance/folder - see
    // GetEntryNumberGroupAsync/GroupMatchingRecords), not just the single selected record - fixed
    // 2026-11-16: once the list page started collapsing a group's sibling Title Records into one
    // visible row, a sibling that isn't the group's representative row had no way to be
    // individually selected from the list anymore, so leaving this single-record would have
    // silently orphaned those siblings in Manual Validation forever. Each sibling still gets its
    // own independent MigrationRecord + MigrationDocuments, copying that sibling's own
    // Title/TitleType/Plan/Block/Lot/TitleSequence/DocumentsJson (its OWN processing instance's
    // document set, never a different instance's) - this does NOT merge or delete any underlying
    // ManualValidationRequests row, and does NOT change how MigrationService processes/submits
    // each resulting MigrationRecord afterward (that stays entirely per-record, unchanged).
    // Returns the id of the single next not-yet-attempted Entry-Number-Range x Title-Sequence
    // combination to initiate, or null if every combination for this group has already been
    // created and attempted (Migrated/Migrating/Failed-RetryPending - the last of which is only
    // retried through Migration Monitoring's Reprocess action, never by clicking this again).
    // Client requirement, 2026-11-27: one button, clicked repeatedly, each click advances exactly
    // one combination - "the combinations should not be displayed separately on the Manual
    // Validation page." Combination creation itself stays idempotent/incremental (safe to call on
    // every click) so this single method serves both the very first click (nothing exists yet) and
    // every later one (some/all combinations already exist).
    public async Task<int?> MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        // Locking stays scoped to `record` alone, same precedent as SaveAsync's own group handling
        // above - group siblings are written regardless of their own lock state.
        var group = await GetEntryNumberGroupAsync(record, cancellationToken);

        // Same mandatory-fields gate the old Migrate action used, now applied to every member of
        // the group up front (fail the whole click before changing anything if any sibling isn't
        // fully validated yet) - a half-migrated group would be worse than rejecting the click.
        foreach (var sibling in group)
        {
            if (ComputeMissingFields(sibling).Length > 0)
            {
                throw new BusinessValidationException("Please complete all mandatory fields before marking this record as Ready for Migration.");
            }
        }

        // Per-COMBINATION existing check (RequestNumber + the specific split Range Entry Number
        // value), not per-sibling (client requirement, 2026-11-27 - fixes a pre-existing bug: a
        // sibling can legitimately have SOME of its split Entry Number Range values already
        // migrated and others still missing; only the missing combinations are created below,
        // every click is safe to call again without duplicating what already exists).
        var requestNumbers = group.Select(g => g.RequestNumber).ToList();
        var existingCombinations = (await _context.MigrationRecords
            .Where(m => requestNumbers.Contains(m.RequestNumber))
            .Select(m => new { m.RequestNumber, m.RangeEntryNumber })
            .ToListAsync(cancellationToken))
            .Select(e => (e.RequestNumber, RangeValue: e.RangeEntryNumber ?? string.Empty))
            .ToHashSet();

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);

        // Pre-resolved once (not per sibling/entry number/document) - used below to populate each
        // new MigrationDocument's CodeLookupId immediately, same shape as
        // MigrationService.GetDocumentTypeLookupAsync.
        var documentTypeLookup = await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.DocumentType)
            .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

        foreach (var sibling in group)
        {
            // The real operative entry-number axis is EntryNumberRange (client-confirmed,
            // 2026-11-27 - this used to incorrectly split the preserved, display-only
            // EntryNumbersCsv instead; see MigrationRecord.EntryNumbersCsv's own comment for why
            // that field must stay unsplit/verbatim on every row created below).
            var siblingRangeValues = EntryNumberCsvParser.Split(sibling.EntryNumberRange);
            var missingRangeValues = siblingRangeValues
                .Where(v => !existingCombinations.Contains((sibling.RequestNumber, v ?? string.Empty)))
                .ToList();

            if (missingRangeValues.Count == 0)
            {
                continue;
            }

            if (sibling.Status != ManualValidationStatus.ReadyForMigration)
            {
                var originalStatus = sibling.Status;
                sibling.Status = ManualValidationStatus.ReadyForMigration;
                sibling.UpdatedByUserId = _currentUserService.UserId;
                sibling.UpdatedByUsername = displayName;
                sibling.UpdatedAt = DateTime.UtcNow;

                // Action History entry via the existing RecordHistory mechanism - no new
                // table/column. Action is stored as the literal display string (not an enum
                // .ToString()) because, unlike Saved/Closed, "Ready for Migration" has spaces that
                // a plain enum name can't produce; the same string is what <status-badge> renders
                // for this action.
                _context.RecordHistory.Add(new RecordHistory
                {
                    TableName = RecordHistoryTables.ManualValidationRequests,
                    RecordId = sibling.Id,
                    RefNo = sibling.RequestNumber,
                    Action = "Ready for Migration",
                    Remarks = $"Status: Previous = '{FormatValueForHistory(StatusDisplay.ManualValidationStatusToDisplay(originalStatus.ToString()))}', Current = 'Ready for Migration'",
                    ByUserId = _currentUserService.UserId,
                    ByUsername = displayName ?? "system",
                    CreatedAt = DateTime.UtcNow,
                });
            }

            var documentItems = ParseDocumentItems(sibling.DocumentsJson);

            // One MigrationRecords row per (sibling x split Entry Number Range value) combination
            // (client requirement, 2026-11-12, corrected 2026-11-27 to split the right field) -
            // everything else copied identically into each row.
            foreach (var rangeValue in missingRangeValues)
            {
                var migrationRecord = new MigrationRecord
                {
                    RequestNumber = sibling.RequestNumber,
                    OcrExtractionRecordId = sibling.OcrExtractionRecordId,
                    MigrationDate = DateTime.UtcNow,
                    RdCode = sibling.RdCode!,
                    RdName = sibling.RdName!,
                    // Preserved verbatim (never split) - the client's "Original Entry Number CSV"
                    // reference value, identical across every combination from this sibling, never
                    // sent to the external API (see ProcessMigrationAsync, which sends
                    // RangeEntryNumber instead).
                    EntryNumbersCsv = sibling.EntryNumbersCsv,
                    Title = sibling.Title,
                    TitleType = sibling.TitleType,
                    Plan = sibling.Plan,
                    Block = sibling.Block,
                    Lot = sibling.Lot,
                    TitleSequence = sibling.TitleSequence,
                    MigrationStatus = Models.Enums.MigrationStatus.ReadyForMigration,
                    MigrationType = Models.Enums.MigrationType.ManualValidation,
                    ProcessedByUserId = _currentUserService.UserId,
                    ProcessedByUsername = displayName,
                    // Verbatim copy - preserves the original JSON structure exactly as Manual
                    // Validation produced it. ProcessingStatus starts null ("not yet initiated") -
                    // InitiateMigrationAsync's own atomic claim accepts a null row, so the
                    // background processing kicked off once this combination is picked as the next
                    // eligible one (see ManualValidationController.ReadyForMigration) picks it up
                    // normally.
                    DocumentJson = sibling.DocumentsJson,
                    // The per-combination split value (client requirement, 2026-11-27) - this, not
                    // EntryNumbersCsv, is what's unique per combination, sent to the external API,
                    // and used to group Associated Title Records for the same Entry Number Range.
                    RangeEntryNumber = rangeValue,
                };

                // One MigrationDocuments row per supporting document, created immediately - not
                // deferred until the SD Auto Conversion API responds (client requirement,
                // 2026-11-12: the API response must UPDATE these rows, never INSERT new ones).
                // CodeLookupId is left null exactly when DocumentName is still "OTHERS" (never
                // resolves in the lookup), so an already-classified document is correctly never
                // treated as "Others" from the moment it's created - see
                // MigrationService.InitiateMigrationAsync, which sources submission from these
                // rows directly instead of re-parsing DocumentsJson.
                foreach (var item in documentItems)
                {
                    var documentName = item.DocumentId ?? item.DocumentTypeCode ?? string.Empty;
                    migrationRecord.Documents.Add(new MigrationDocument
                    {
                        DocumentName = documentName,
                        FileName = item.RenamedFileName,
                        SourceImagePath = item.ImagePath,
                        CodeLookupId = documentTypeLookup.TryGetValue(documentName, out var codeLookupId) ? codeLookupId : null,
                        Status = MigrationDocumentStatus.Pending,
                    });
                }

                _context.MigrationRecords.Add(migrationRecord);
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // UK_MigrationRecords_01 (unique RequestNumber + RangeEntryNumber + TitleSequence)
            // caught a concurrent duplicate submission - the pre-check above already covers the
            // common case; this is the defense-in-depth backstop for a genuine race between two
            // near-simultaneous clicks (on the same record, or on two different siblings of the
            // same group at once).
            throw new ConflictException("This Entry Record has already been marked Ready for Migration.");
        }

        // Pick the next not-yet-attempted combination across the WHOLE group (pre-existing rows
        // included, not just ones created just now) - deterministic by Id, i.e. creation order.
        // This is what actually advances on each click; the creation above only ensures every
        // combination exists at all.
        var nextEligibleId = await _context.MigrationRecords
            .Where(m => requestNumbers.Contains(m.RequestNumber) && m.ProcessingStatus == null)
            .OrderBy(m => m.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return nextEligibleId;
    }

    // Matching is staged per the acceptance criteria:
    //   1. RD Code + Title Number + Title Type.
    //   2. If still ambiguous, narrow further using Plan/Block/Lot (TCT/OCT - the only two
    //      TitleType values this codebase has, see Models/Enums/TitleType.cs).
    //   3. If still ambiguous after that (a genuinely Repeating Title Number), return every
    //      remaining candidate for the caller to show a manual-selection window instead of
    //      picking one arbitrarily.
    // Calls the external GetTitleSequence API (PHILARIS-RD - the same external system as SD Auto
    // Conversion, see ISdAutoConversionClient) instead of matching against local CodeLookups
    // data. A single match is persisted immediately to the Title Record row identified by
    // request.RecordId, so the retrieved value survives even if the user never clicks this
    // page's own Save button afterward (explicit instruction, 2026-11-12). More than one match is
    // a genuine "Repeating Title Number" - every candidate is returned, not yet persisted, for the
    // user to pick from via SelectTitleSequenceAsync.
    public async Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default)
    {
        var rdCode = request.RdCode.Trim();
        var titleType = request.TitleType.Trim();
        var titleNo = request.Title.Trim();

        if (titleNo.Length == 0 || titleType.Length == 0)
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        var result = await _sdAutoConversionClient.GetTitleSequenceAsync(
            new SdTitleSequenceRequest { RdCode = rdCode, TitleType = titleType, TitleNo = titleNo },
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "GetTitleSequence failed for RecordId {RecordId}, RdCode {RdCode}, TitleNo {TitleNo}: {Error}",
                request.RecordId, rdCode, titleNo, result.ErrorMessage);
            throw new NotFoundException(result.ErrorMessage ?? "No Title Sequence record was found.");
        }

        if (result.Results.Count == 0)
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        if (result.Results.Count == 1)
        {
            var sequence = result.Results[0].TitleSeq.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await PersistTitleSequenceAsync(request.RecordId, sequence, cancellationToken);
            return new TitleSequenceDto { Sequence = sequence };
        }

        return new TitleSequenceDto
        {
            IsAmbiguous = true,
            Candidates = result.Results.Select(x => new TitleSequenceCandidateDto
            {
                RdCode = x.RdCode,
                Title = x.TitleNo ?? titleNo,
                TitleType = x.TitleType ?? titleType,
                Pbl = x.Pbl,
                Sequence = x.TitleSeq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }).ToArray(),
        };
    }

    public Task SelectTitleSequenceAsync(int recordId, string sequence, CancellationToken cancellationToken = default) =>
        PersistTitleSequenceAsync(recordId, sequence, cancellationToken);

    private async Task PersistTitleSequenceAsync(int recordId, string sequence, CancellationToken cancellationToken)
    {
        var record = await GetActiveRecordAsync(recordId, cancellationToken);
        record.TitleSequence = sequence;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ManualValidationRequest> GetActiveRecordAsync(int id, CancellationToken cancellationToken) =>
        await _context.ManualValidationRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.MigratedAt == null, cancellationToken)
            ?? throw new NotFoundException("Manual validation record", id);

    // LEVEL 1 - the overall Entry Number group (2026-11-16, Entry Number-based consolidation
    // across processing instances). Every active ManualValidationRequest sharing `primary`'s
    // exact RdCode AND EntryNumbersCsv, in ascending Id order (so "Title Record 1" is always the
    // earliest-created row) - the key is the (RdCode, EntryNumbersCsv) pair, never EntryNumbersCsv
    // alone (the same Entry Number can legitimately recur under a different RD) and never RdCode
    // alone (one RD has many unrelated Entry Numbers), and never RequestNumber or Id.
    // EntryNumberRange (the Details page's own editable "Entry Number" display/search field - see
    // SaveAsync/BuildSearchQuery) is a DIFFERENT, narrower concern and plays no part in this key -
    // EntryNumbersCsv is the raw value the external extraction pipeline sets once at
    // record-creation time and never changes afterward, which is exactly why it - not the
    // user-editable EntryNumberRange - is the stable identity that must tie multiple processing
    // instances of "the same real Entry Number" together, even when each instance's own OCR-
    // extracted EntryNumberRange text could vary slightly between runs. A blank/whitespace
    // EntryNumbersCsv (NULL included), or a blank RdCode, never groups - each such row forms a
    // group of one (itself), identified independently by Id. Returns tracked entities (so SaveAsync/
    // MarkReadyForMigrationAsync can mutate and persist group siblings directly).
    //
    // IMPORTANT: this key deliberately does NOT include FolderPath. An earlier fix (same date)
    // tried RdCode + EntryNumberRange + FolderPath as a single flat key - that was wrong: the same
    // Entry Number can legitimately be processed multiple times under different physical folders
    // (a different OcrExtractionRecords row each time), and every one of those processing
    // instances must still consolidate into this SAME overall group, never split into separate
    // ones just because the folder changed. FolderPath/OcrExtractionRecordId instead identify
    // which PROCESSING INSTANCE (and therefore which DocumentsJson) a row within this broader
    // group belongs to - see PartitionIntoProcessingInstances/BuildCombinedDocuments below.
    private async Task<List<ManualValidationRequest>> GetEntryNumberGroupAsync(ManualValidationRequest primary, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(primary.RdCode) || string.IsNullOrWhiteSpace(primary.EntryNumbersCsv))
        {
            return [primary];
        }

        var rdCode = primary.RdCode;
        var entryNumbersCsv = primary.EntryNumbersCsv;

        var group = await _context.ManualValidationRequests
            .Include(r => r.OcrExtractionRecord)
            .Where(r => r.MigratedAt == null && r.RdCode == rdCode && r.EntryNumbersCsv == entryNumbersCsv)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        if (!group.Any(r => r.Id == primary.Id))
        {
            group.Insert(0, primary);
        }

        return group;
    }

    private void EnsureNotLockedByAnotherUser(ManualValidationRequest record)
    {
        var lockedByAnotherUser = record.LockedByUserId.HasValue
            && record.LockedByUserId != _currentUserService.UserId
            && record.LockedAt.HasValue
            && DateTime.UtcNow - record.LockedAt.Value < LockTimeout;

        if (lockedByAnotherUser)
        {
            throw new ForbiddenAppException("This record is currently being reviewed by another user.");
        }
    }

    private static string[] ComputeMissingFields(ManualValidationRequest r)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(r.RdCode)) missing.Add("rdCode");
        if (string.IsNullOrWhiteSpace(r.RdName)) missing.Add("rdName");
        if (string.IsNullOrWhiteSpace(r.EntryNumberRange)) missing.Add("entry");
        if (string.IsNullOrWhiteSpace(r.Title)) missing.Add("title");
        if (r.TitleType is null) missing.Add("titleType");
        if (string.IsNullOrWhiteSpace(r.Plan)) missing.Add("plan");
        if (string.IsNullOrWhiteSpace(r.Block)) missing.Add("block");
        if (string.IsNullOrWhiteSpace(r.Lot)) missing.Add("lot");
        if (string.IsNullOrWhiteSpace(r.TitleSequence)) missing.Add("titleSequence");
        return missing.ToArray();
    }

    // LEVEL 2 - partitions a Level-1 Entry Number group (RdCode + EntryNumbersCsv) into individual
    // processing instances, each of which shares exactly one DocumentsJson (spec: "identical
    // DocumentsJson values are repeated across sibling records" within one instance). Keyed by
    // OcrExtractionRecordId when present - two rows pointing at the same OcrExtractionRecords row
    // are unambiguously the same physical fetch/extraction, the most reliable identity available
    // (never the FolderPath string alone - "do not assume FolderPath alone uniquely identifies a
    // processing instance if it can be reused"). A row with no OcrExtractionRecordId at all forms
    // its own singleton instance, keyed by its own Id - two such rows are never assumed to be the
    // same instance just because both lack a link.
    private static List<List<ManualValidationRequest>> PartitionIntoProcessingInstances(List<ManualValidationRequest> group) =>
        group
            .GroupBy(r => r.OcrExtractionRecordId.HasValue ? $"ocr:{r.OcrExtractionRecordId}" : $"solo:{r.Id}")
            .Select(g => g.OrderBy(r => r.Id).ToList())
            .ToList();

    // Builds the consolidated Supporting Documents list for an Entry Number group: one
    // representative DocumentsJson per processing instance (the lowest-Id sibling in that
    // instance - every other sibling in it is guaranteed to carry an identical copy, so only the
    // representative's is ever parsed), UNIONED across every distinct processing instance found
    // in the group - never just the single opened record's own instance. Different instances'
    // document sets are combined, never overwritten or concatenated as raw JSON; identical
    // documents repeated across sibling rows WITHIN one instance are still only ever read once.
    // Sorted ascending by Document Type, then Image File Name, matching the single-instance
    // ordering this always used before consolidation existed. Each returned item carries which
    // record actually owns it (OwnerRecordId - always that instance's representative), so a
    // correction later resolves back to the correct instance without ever crossing into another.
    private static List<(DocumentJsonItem Item, int OwnerRecordId)> BuildCombinedDocuments(List<ManualValidationRequest> group)
    {
        var combined = new List<(DocumentJsonItem Item, int OwnerRecordId)>();
        foreach (var instance in PartitionIntoProcessingInstances(group))
        {
            var representative = instance[0];
            combined.AddRange(ParseDocumentItems(representative.DocumentsJson).Select(item => (item, representative.Id)));
        }

        return combined
            .OrderBy(x => x.Item.DocumentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Item.RenamedFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ManualValidationDetailDto MapToDetail(ManualValidationRequest primary, List<ManualValidationRequest> group)
    {
        // Supporting Documents are now consolidated across every processing instance in the
        // Level-1 Entry Number group (2026-11-16) - see BuildCombinedDocuments. Previously this
        // read only the opened record's own DocumentsJson; that was correct when a group could
        // only ever represent one physical folder, but silently discarded other processing
        // instances' documents once the same Entry Number could legitimately be processed more
        // than once under a different folder.
        var displayDocuments = BuildCombinedDocuments(group);

        return new ManualValidationDetailDto
        {
            Id = primary.Id,
            RequestNumber = primary.RequestNumber,
            RdCode = primary.RdCode,
            RdName = primary.RdName,
            // Details page's editable "Entry Number" field reads/writes EntryNumberRange now
            // (2026-11-03, per client request) - see the Save-side comment in SaveAsync. The DTO
            // property keeps its existing name (EntryNumbersCsv) to avoid an unrelated rename
            // across Details.cshtml/manual-validation-details.js.
            EntryNumbersCsv = primary.EntryNumberRange,
            Title = primary.Title,
            TitleType = primary.TitleType?.ToString(),
            Plan = primary.Plan,
            Block = primary.Block,
            Lot = primary.Lot,
            TitleSequence = primary.TitleSequence,
            Status = primary.Status.ToString(),
            // Computed live from the record's actual field values (same rule the Save path already
            // uses) rather than trusting the stored MissingFieldsCsv - that column is populated by
            // the external OCR pipeline at record-creation time and doesn't reliably reflect which
            // Title Record fields are actually blank (e.g. leaves plan/block unflagged even when
            // empty, and carries unrelated keys like "documentClassification").
            MissingFields = ComputeMissingFields(primary),
            TitleRecords = group.Select(r => new ManualValidationTitleRecordDto
            {
                RecordId = r.Id,
                Title = r.Title,
                TitleType = r.TitleType?.ToString(),
                Plan = r.Plan,
                Block = r.Block,
                Lot = r.Lot,
                TitleSequence = r.TitleSequence,
                MissingFields = ComputeMissingFields(r)
                    .Where(f => f is "title" or "titleType" or "plan" or "block" or "lot" or "titleSequence")
                    .ToArray(),
            }).ToArray(),
            Documents = displayDocuments.Select((x, index) => new ManualValidationDocumentDto
            {
                Id = index + 1,
                DocumentId = x.Item.DocumentId,
                DocumentName = x.Item.DocumentName,
                RenamedFileName = x.Item.RenamedFileName,
                CanChangeDocumentType = IsOthersEligible(x.Item),
            }).ToArray(),
        };
    }

    // Raw parse of one row's own DocumentsJson - {documentId, documentTypeCode, documentName,
    // originalFileName, renamedFileName, imagePath} per item - in storage order, unsorted.
    private static List<DocumentJsonItem> ParseDocumentItems(string? documentsJson)
    {
        if (string.IsNullOrWhiteSpace(documentsJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<DocumentJsonItem>>(documentsJson, JsonOptions) ?? [];
    }

    // The last 4 path segments of ImagePath (RD Code / Entry Folder / Document Type Folder /
    // File Name) - stable across a re-fetch into a new Root Source Path folder, unlike the full
    // absolute path. Falls back to the full ImagePath, then RenamedFileName, when there aren't at
    // least 4 segments to work with (kept safe rather than throwing on an unexpected shape).
    //
    // Used solely to find every OTHER Title Record's own copy of the document a Document Type
    // change targets, WITHIN that document's own processing instance (see
    // ApplyDocumentTypeChanges's `allMatching`), so the same correction can be synchronized into
    // all of them on Save - never across a different processing instance, and never for display
    // (Supporting Documents display consolidates one representative per instance across the whole
    // Entry Number group; see BuildCombinedDocuments/MapToDetail).
    private static string DocumentIdentityKey(DocumentJsonItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ImagePath))
        {
            return item.RenamedFileName ?? string.Empty;
        }

        // Explicit char[] (not a [...] collection expression) - some .NET 8 SDK patch versions
        // have a known overload-resolution ambiguity between Split(char[]?, StringSplitOptions)
        // and Split(string?, StringSplitOptions) when a collection expression is used here
        // instead, causing CS0121 on those SDKs even though it compiles fine on newer patches.
        var segments = item.ImagePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4 ? string.Join('/', segments[^4..]) : item.ImagePath.Trim();
    }

    public async Task<string?> GetDocumentImagePathAsync(int id, int documentId, CancellationToken cancellationToken = default)
    {
        var record = await _context.ManualValidationRequests.AsNoTracking()
            .Where(r => r.Id == id && r.MigratedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return null;
        }

        // Same source and same build as MapToDetail's displayDocuments (the whole Entry Number
        // group's consolidated document list, not just this one record's own instance - 2026-11-16),
        // so a given documentId always resolves to the exact document shown at that position on
        // screen. Must match MapToDetail's BuildCombinedDocuments ordering EXACTLY - a mismatch
        // here would stream back a different document's image than the one actually clicked.
        var group = await GetEntryNumberGroupAsync(record, cancellationToken);
        var combined = BuildCombinedDocuments(group);

        var index = documentId - 1;
        return index >= 0 && index < combined.Count ? combined[index].Item.ImagePath : null;
    }

    private class DocumentJsonItem
    {
        public string? DocumentId { get; set; }
        public string? DocumentTypeCode { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public string RenamedFileName { get; set; } = string.Empty;
        public string? ImagePath { get; set; }

        // Every DocumentsJson property not modeled above (e.g. titleNumber, plan, block, lot,
        // duplicate, and any legacy isDuplicate value from before the Duplicate checkbox feature
        // was removed) - captured here purely so re-serializing this item (ApplyDocumentTypeChanges)
        // round-trips them unchanged instead of silently dropping them; nothing in this service
        // ever reads or writes through this bag directly.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
