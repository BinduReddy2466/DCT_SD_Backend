using System.Text.Json;
using System.Text.RegularExpressions;
using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
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

    public ManualValidationService(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ManualValidationListItemDto>> SearchAsync(ManualValidationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.ManualValidationRequests.AsNoTracking().Where(r => r.MigratedAt == null);

        if (!string.IsNullOrWhiteSpace(request.RdCode))
        {
            query = query.Where(r => r.RdCode == request.RdCode);
        }

        if (!string.IsNullOrWhiteSpace(request.RequestNumber))
        {
            var term = request.RequestNumber.Trim().ToLower();
            query = query.Where(r => r.RequestNumber.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.EntryNumbersCsv))
        {
            var term = request.EntryNumbersCsv.Trim().ToLower();
            query = query.Where(r => r.EntryNumbersCsv != null && r.EntryNumbersCsv.ToLower().Contains(term));
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
            query = query.Where(r => r.ExtractionDate >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.ExtractionDate < DateRangeFilter.EndOfDayExclusive(request.DateTo.Value));
        }

        // Every filter above still runs server-side exactly as before; only the grouping/paging
        // step below happens in memory, over the already-filtered set. Manual Validation is a
        // human review queue (bounded, not a bulk data table), so this trades a small, predictable
        // amount of extra memory for grouping logic that's simple to read and audit - the same
        // pragmatic in-memory-filter approach already used elsewhere in this service (see
        // RetrieveTitleSequenceAsync).
        var matchingRecords = await query.ToListAsync(cancellationToken);

        // Same sole grouping key as the Details page (GetGroupRecordsAsync): rows sharing the
        // exact same non-blank EntryNumbersCsv become one UI row; a blank/whitespace
        // EntryNumbersCsv never groups; each such row is its own group of one.
        var groups = matchingRecords
            .GroupBy(r => string.IsNullOrWhiteSpace(r.EntryNumbersCsv) ? $"__row:{r.Id}" : r.EntryNumbersCsv)
            .Select(g => g.OrderBy(r => r.Id).ToList())
            .ToList();

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
        EntryNumbersCsv = r.EntryNumbersCsv,
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

        var group = await GetGroupRecordsAsync(record, cancellationToken);
        return MapToDetail(record, group);
    }

    public async Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        // Every row sharing this record's (pre-edit) EntryNumbersCsv - the sole grouping key.
        // Locking stays scoped to `record` alone (EnsureNotLockedByAnotherUser above only checked
        // that one row); group siblings are written here regardless of their own lock state, same
        // as today's single-record Save never checked a second row's lock.
        var group = await GetGroupRecordsAsync(record, cancellationToken);
        var groupIds = group.Select(r => r.Id).ToHashSet();

        var changeDescriptionsByRecordId = new Dictionary<int, List<string>>();
        List<string> ChangesFor(int recordId) =>
            changeDescriptionsByRecordId.TryGetValue(recordId, out var list) ? list : changeDescriptionsByRecordId[recordId] = [];

        // RD Code / RD Name / Entry Number are shown once in General Information, not per Title
        // Record, so an edit there applies to every row in the group - keeping the group coherent
        // for the next time it's opened (all rows still share the same, now-updated, EntryNumbersCsv).
        var newRdCode = request.RdCode?.Trim();
        var newEntryNumbersCsv = request.EntryNumbersCsv?.Trim();
        var newRdName = string.IsNullOrWhiteSpace(newRdCode)
            ? null
            : await _context.CodeLookups.AsNoTracking()
                .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.Code == newRdCode)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        foreach (var r in group)
        {
            var originalRdCode = r.RdCode;
            var originalEntryNumbersCsv = r.EntryNumbersCsv;

            r.RdCode = newRdCode;
            r.EntryNumbersCsv = newEntryNumbersCsv;
            r.RdName = newRdName;

            AddFieldChange(ChangesFor(r.Id), "RD Code", originalRdCode, r.RdCode);
            AddFieldChange(ChangesFor(r.Id), "Entry Number", originalEntryNumbersCsv, r.EntryNumbersCsv);
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

        foreach (var r in group)
        {
            r.MissingFieldsCsv = string.Join(',', ComputeMissingFields(r));
            r.UpdatedByUserId = _currentUserService.UserId;
            r.UpdatedByUsername = _currentUserService.Username;
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
                    ApplyDocumentTypeChanges(group, documentChanges, renamesToRollBack, changeDescriptionsByRecordId, touchedRecordIds);
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
                    ByUsername = _currentUserService.Username ?? "system",
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
    // Every change's target document is resolved up front, against ONE pre-change merged/sorted
    // snapshot of the WHOLE group's DocumentsJson (see BuildCombinedSortedDocuments - the same
    // merge/dedup/sort that assigned each document's client-facing Id) - because renaming a
    // document changes its RenamedFileName (Image File Name), which is the sort key, resolving
    // each change's index one at a time as it's applied would let an earlier rename in this same
    // batch shift the sort order and cause a later change to silently target the wrong document.
    // rollbacks is populated in place as each rename succeeds - if a later change in this same call throws,
    // the caller still has every rollback recorded so far. changeDescriptions gets one "Supporting
    // Document '<original file name>' - Document Type: Previous = '...', Current = '...'" line,
    // recorded against whichever underlying row actually owns that document, for the same
    // RecordHistory.Remarks audit trail as the scalar field changes.
    private static void ApplyDocumentTypeChanges(
        List<ManualValidationRequest> group,
        List<DocumentChangeItemDto> changes,
        List<(string NewPath, string OriginalPath)> rollbacks,
        Dictionary<int, List<string>> changeDescriptionsByRecordId,
        HashSet<int> touchedRecordIds)
    {
        var perRecordItems = group.Select(r => (RecordId: r.Id, Items: ParseDocumentItems(r.DocumentsJson))).ToList();
        var combined = BuildCombinedSortedDocuments(perRecordItems);
        var allItemsFlat = perRecordItems.SelectMany(x => x.Items).ToList();

        var targets = new List<(int RecordId, DocumentJsonItem Target, DocumentChangeItemDto Change)>();
        foreach (var change in changes)
        {
            if (change.Index - 1 < combined.Count)
            {
                var (recordId, item) = combined[change.Index - 1];
                targets.Add((recordId, item, change));
            }
        }

        foreach (var (recordId, target, change) in targets)
        {
            // Captured before ApplyDocumentTypeChange mutates `target` in place, so the audit
            // line below always identifies the document by its ORIGINAL file name and shows its
            // ORIGINAL classification - never the values already being changed to. originalImagePath
            // is what identifies OTHER rows that reference this exact same physical file (see
            // sibling-sync below) - a shared "Others" bucket file is common between sibling Title
            // Records under the same Entry Number, since the OCR pipeline organizes files by RD
            // Code + Entry Number, not per Title Record.
            var originalFileName = target.RenamedFileName;
            var originalDocumentName = target.DocumentName;
            var originalImagePath = target.ImagePath;

            // Sequence numbers are scanned across every document in the group (allItemsFlat), not
            // just this document's own owning row, so two sibling rows reclassifying documents to
            // the same Document Type in one Save never collide on the same sequence number.
            var rollback = ApplyDocumentTypeChange(allItemsFlat, target, change.Code.Trim(), change.Name.Trim());
            if (rollback is { } r)
            {
                rollbacks.Add(r);
            }

            // ApplyDocumentTypeChange leaves `target` untouched (documentName included) when
            // there was nothing to apply (e.g. a missing ImagePath) - comparing before/after here
            // is the simplest way to only record documents that were actually reclassified.
            if (!string.Equals(target.DocumentName, originalDocumentName, StringComparison.Ordinal))
            {
                touchedRecordIds.Add(recordId);
                var descriptions = changeDescriptionsByRecordId.TryGetValue(recordId, out var list) ? list : changeDescriptionsByRecordId[recordId] = [];
                descriptions.Add(
                    $"Supporting Document '{originalFileName}' - Document Type: Previous = '{FormatValueForHistory(originalDocumentName)}', Current = '{target.DocumentName}'");

                // Propagate the same rename to every OTHER item across the group that referenced
                // the exact same original ImagePath - i.e. a sibling Title Record's row pointing
                // at the identical shared file. Without this, that sibling's entry keeps citing
                // the old path (which no longer exists - the file was just physically moved), so
                // it stops colliding with the target's new path on the next merge and resurfaces
                // as a phantom duplicate document pointing at a broken/missing file.
                if (!string.IsNullOrWhiteSpace(originalImagePath))
                {
                    foreach (var (siblingRecordId, siblingItems) in perRecordItems)
                    {
                        foreach (var siblingItem in siblingItems)
                        {
                            if (ReferenceEquals(siblingItem, target)
                                || !string.Equals(siblingItem.ImagePath, originalImagePath, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var siblingOriginalName = siblingItem.DocumentName;
                            siblingItem.DocumentId = target.DocumentId;
                            siblingItem.DocumentName = target.DocumentName;
                            siblingItem.RenamedFileName = target.RenamedFileName;
                            siblingItem.ImagePath = target.ImagePath;
                            touchedRecordIds.Add(siblingRecordId);

                            var siblingDescriptions = changeDescriptionsByRecordId.TryGetValue(siblingRecordId, out var sl) ? sl : changeDescriptionsByRecordId[siblingRecordId] = [];
                            siblingDescriptions.Add(
                                $"Supporting Document '{originalFileName}' - Document Type: Previous = '{FormatValueForHistory(siblingOriginalName)}', Current = '{target.DocumentName}' (shared document, also reclassified via another Title Record)");
                        }
                    }
                }
            }
        }

        // Only the owning row's own DocumentsJson is re-serialized/written back - a document that
        // was never targeted, or belongs to a row nothing here touched, is left completely alone.
        foreach (var (recordId, items) in perRecordItems)
        {
            if (!touchedRecordIds.Contains(recordId))
            {
                continue;
            }

            var owner = group.First(r => r.Id == recordId);
            owner.DocumentsJson = JsonSerializer.Serialize(items, JsonOptions);
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

        return rollback;
    }

    // Scans this record's existing supporting documents for ones already classified under the
    // exact same Document ID + Document Name, extracts the trailing "_<number>" sequence from
    // their renamedFileName (matching "<DocumentID>_<DocumentName>_<N>.<extension>"), and
    // returns the highest one found, plus 1 - or 1 if none match. Gaps are preserved on purpose
    // (existing _1 and _4 -> next is _5, not _2) since this always takes the true maximum, never
    // the first free slot.
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
        record.UpdatedByUserId = _currentUserService.UserId;
        record.UpdatedByUsername = _currentUserService.Username;
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
            ByUsername = _currentUserService.Username ?? "system",
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

    // Replaces the old "Migrate" action. This only flags the record as ready - it deliberately
    // never sets MigratedAt (that would remove it from the Manual Validation list/queries, the
    // same way the old Migrate action used to) and never touches MigrationRecords/Migration
    // Monitoring, which this app has no existing write path into at all (that table is populated
    // entirely by an external process). The actual migration mechanism is expected to pick up
    // ReadyForMigration records on its own, outside this app.
    public async Task MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        // Same mandatory-fields gate the old Migrate action used - reused as-is, since a record
        // that isn't fully validated still shouldn't be flagged ready for the next stage.
        if (ComputeMissingFields(record).Length > 0)
        {
            throw new BusinessValidationException("Please complete all mandatory fields before marking this record as Ready for Migration.");
        }

        var originalStatus = record.Status;
        record.Status = ManualValidationStatus.ReadyForMigration;
        record.UpdatedByUserId = _currentUserService.UserId;
        record.UpdatedByUsername = _currentUserService.Username;
        record.UpdatedAt = DateTime.UtcNow;

        // Action History entry via the existing RecordHistory mechanism - no new table/column.
        // Action is stored as the literal display string (not an enum .ToString()) because,
        // unlike Saved/Closed, "Ready for Migration" has spaces that a plain enum name can't
        // produce; the same string is what <status-badge> renders for this action.
        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.ManualValidationRequests,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = "Ready for Migration",
            Remarks = $"Status: Previous = '{FormatValueForHistory(StatusDisplay.ManualValidationStatusToDisplay(originalStatus.ToString()))}', Current = 'Ready for Migration'",
            ByUserId = _currentUserService.UserId,
            ByUsername = _currentUserService.Username ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Matching is staged per the acceptance criteria:
    //   1. RD Code + Title Number + Title Type.
    //   2. If still ambiguous, narrow further using Plan/Block/Lot (TCT/OCT - the only two
    //      TitleType values this codebase has, see Models/Enums/TitleType.cs).
    //   3. If still ambiguous after that (a genuinely Repeating Title Number), return every
    //      remaining candidate for the caller to show a manual-selection window instead of
    //      picking one arbitrarily.
    // RD Code isn't part of CodeLookups.Code's existing composite key/DataJson for any row in
    // the live data today, so it's only enforced against rows whose DataJson actually records
    // one - rows without it are never excluded on that basis, which keeps every existing
    // Title Sequence lookup working exactly as it does today.
    public async Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TitleType>(request.TitleType, true, out var titleType))
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        var rdCode = request.RdCode.Trim();
        var title = request.Title.Trim();
        var plan = request.Plan.Trim();
        var block = request.Block.Trim();
        var lot = request.Lot.Trim();

        var rows = await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.TitleSequence && c.IsActive)
            .ToListAsync(cancellationToken);

        var parsed = rows
            .Select(r => (Row: r, Data: DeserializeTitleSequenceData(r.DataJson)))
            .Where(x => x.Data is not null)
            .ToList();

        var stage1 = parsed.Where(x =>
            string.Equals(x.Data!.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase)
            && x.Data.TitleType == (int)titleType
            && (string.IsNullOrWhiteSpace(x.Data.RdCode) || string.Equals(x.Data.RdCode!.Trim(), rdCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (stage1.Count == 0)
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        if (stage1.Count == 1)
        {
            return new TitleSequenceDto { Sequence = stage1[0].Row.Name };
        }

        var stage2 = stage1.Where(x =>
            string.Equals(x.Data!.Plan?.Trim(), plan, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Data.Block?.Trim(), block, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Data.Lot?.Trim(), lot, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (stage2.Count == 0)
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        if (stage2.Count == 1)
        {
            return new TitleSequenceDto { Sequence = stage2[0].Row.Name };
        }

        return new TitleSequenceDto
        {
            IsAmbiguous = true,
            Candidates = stage2.Select(x => new TitleSequenceCandidateDto
            {
                RdCode = x.Data!.RdCode,
                Title = x.Data.Title ?? title,
                TitleType = titleType.ToString(),
                Plan = x.Data.Plan,
                Block = x.Data.Block,
                Lot = x.Data.Lot,
                Sequence = x.Row.Name,
            }).ToArray(),
        };
    }

    private static TitleSequenceDataJson? DeserializeTitleSequenceData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TitleSequenceDataJson>(dataJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private class TitleSequenceDataJson
    {
        public string? RdCode { get; set; }
        public string? Title { get; set; }
        public int? TitleType { get; set; }
        public string? Plan { get; set; }
        public string? Block { get; set; }
        public string? Lot { get; set; }
    }

    private async Task<ManualValidationRequest> GetActiveRecordAsync(int id, CancellationToken cancellationToken) =>
        await _context.ManualValidationRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.MigratedAt == null, cancellationToken)
            ?? throw new NotFoundException("Manual validation record", id);

    // Every active ManualValidationRequest sharing `primary`'s exact EntryNumbersCsv, in
    // ascending Id order (so "Title Record 1" is always the earliest-created row) - this is the
    // sole grouping key, never RequestNumber or Id. A blank/whitespace EntryNumbersCsv never
    // groups: a record with no Entry Number always forms a group of one (itself), so records
    // that simply haven't had an Entry Number entered yet are never lumped together by accident.
    // Returns tracked entities (so SaveAsync can mutate and persist group siblings directly).
    private async Task<List<ManualValidationRequest>> GetGroupRecordsAsync(ManualValidationRequest primary, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(primary.EntryNumbersCsv))
        {
            return [primary];
        }

        var entryNumbersCsv = primary.EntryNumbersCsv;
        var group = await _context.ManualValidationRequests
            .Where(r => r.MigratedAt == null && r.EntryNumbersCsv == entryNumbersCsv)
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
        if (string.IsNullOrWhiteSpace(r.EntryNumbersCsv)) missing.Add("entry");
        if (string.IsNullOrWhiteSpace(r.Title)) missing.Add("title");
        if (r.TitleType is null) missing.Add("titleType");
        if (string.IsNullOrWhiteSpace(r.Plan)) missing.Add("plan");
        if (string.IsNullOrWhiteSpace(r.Block)) missing.Add("block");
        if (string.IsNullOrWhiteSpace(r.Lot)) missing.Add("lot");
        if (string.IsNullOrWhiteSpace(r.TitleSequence)) missing.Add("titleSequence");
        return missing.ToArray();
    }

    private static ManualValidationDetailDto MapToDetail(ManualValidationRequest primary, List<ManualValidationRequest> group)
    {
        var perRecordItems = group.Select(r => (RecordId: r.Id, Items: ParseDocumentItems(r.DocumentsJson))).ToList();
        var combinedDocuments = BuildCombinedSortedDocuments(perRecordItems);

        return new ManualValidationDetailDto
        {
            Id = primary.Id,
            RequestNumber = primary.RequestNumber,
            RdCode = primary.RdCode,
            RdName = primary.RdName,
            EntryNumbersCsv = primary.EntryNumbersCsv,
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
            Documents = combinedDocuments.Select((d, index) => new ManualValidationDocumentDto
            {
                Id = index + 1,
                DocumentId = d.Item.DocumentId,
                DocumentName = d.Item.DocumentName,
                RenamedFileName = d.Item.RenamedFileName,
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

    // Merges every row's own DocumentsJson items into one Supporting Documents list for the
    // group, removes duplicates, and sorts the result - this is the single place that happens, so
    // GetDocumentImagePathAsync (image lookup), MapToDetail (client-facing list) and
    // ApplyDocumentTypeChanges (Save) all agree on what a given 1-based position means.
    //
    // Duplicate identity is the physical image file - ImagePath (falling back to RenamedFileName
    // only on the rare row where ImagePath is blank) - never DocumentId+DocumentName. Two
    // documents filed under the exact same Document ID and Document Name but pointing at two
    // different image files (e.g. "..._1.jpg" and "..._2.jpg") are two different images and both
    // survive; two rows that happen to reference the literal same imagePath collapse to one entry,
    // keeping the first occurrence (the earliest/lowest-Id row - "Title Record 1").
    //
    // Sorted ascending by Image File Name (renamedFileName) - the sole sort key, per the
    // acceptance criteria ("sort the supporting document images in ascending order by Image File
    // Name") - so the Supporting Documents list and the image viewer's Prev/Next order both
    // follow it, for a record with or without group siblings.
    private static List<(int RecordId, DocumentJsonItem Item)> BuildCombinedSortedDocuments(
        IEnumerable<(int RecordId, List<DocumentJsonItem> Items)> perRecordItems)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var combined = new List<(int RecordId, DocumentJsonItem Item)>();

        foreach (var (recordId, items) in perRecordItems)
        {
            foreach (var item in items)
            {
                var key = !string.IsNullOrWhiteSpace(item.ImagePath) ? item.ImagePath!.Trim() : item.RenamedFileName;
                if (string.IsNullOrWhiteSpace(key) || seenKeys.Add(key))
                {
                    combined.Add((recordId, item));
                }
            }
        }

        return combined
            .OrderBy(x => x.Item.RenamedFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        var group = string.IsNullOrWhiteSpace(record.EntryNumbersCsv)
            ? [record]
            : await _context.ManualValidationRequests.AsNoTracking()
                .Where(r => r.MigratedAt == null && r.EntryNumbersCsv == record.EntryNumbersCsv)
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken);

        var perRecordItems = group.Select(r => (RecordId: r.Id, Items: ParseDocumentItems(r.DocumentsJson))).ToList();
        var combined = BuildCombinedSortedDocuments(perRecordItems);
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
    }
}
