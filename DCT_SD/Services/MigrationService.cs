using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;
using DCT_SD.Models.Dtos.SdAutoConversion;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class MigrationService : IMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISdAutoConversionClient _sdAutoConversionClient;
    private readonly IConfiguration _configuration;

    private readonly ILogger<MigrationService> _logger;

    public MigrationService(ApplicationDbContext context, ICurrentUserService currentUserService, ISdAutoConversionClient sdAutoConversionClient, IConfiguration configuration, ILogger<MigrationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sdAutoConversionClient = sdAutoConversionClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PagedResult<MigrationListItemDto>> SearchAsync(MigrationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        // Migration Monitoring is view-only (client requirement, 2026-11-27): it no longer pulls
        // OcrExtractionRecords in directly - every row here arrives exclusively through
        // ManualValidationService.MarkReadyForMigrationAsync. Any MigrationType.SystemMigration row
        // already in the table from before that change keeps showing as before; nothing creates a
        // new one anymore.
        var query = _context.MigrationRecords.AsNoTracking().AsQueryable();

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

        // Title Number is searched through the Associated Title Records relationship (sibling
        // MigrationRecords sharing this row's RD Code + Range Entry Number, corrected 2026-11-27
        // from EntryNumbersCsv - see GetByIdAsync's identical Associated Title Records grouping key
        // for why), not as a direct field on this row - a match returns every row in that Entry's
        // group, not just the matching Title itself (task section 2.C/4 Scenario 3). EXISTS-based
        // (not a join), so this can never fan out duplicate rows for the same MigrationRecords.Id.
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var term = request.Title.Trim().ToLower();
            query = query.Where(r => _context.MigrationRecords.Any(t =>
                t.RdCode == r.RdCode &&
                t.RangeEntryNumber == r.RangeEntryNumber &&
                t.Title != null && t.Title.ToLower().Contains(term)));
        }

        // Filters by the same consolidated 5-value status ConsolidatedMigrationStatus computes
        // for display (Index.cshtml's dropdown offers exactly these 5 literal strings) - expressed
        // directly as a SQL-translatable Where on the two underlying columns rather than
        // re-parsing a MigrationStatus enum value, since the consolidated model spans both
        // ProcessingStatus and MigrationStatus.
        query = request.MigrationStatus switch
        {
            "Migrating" => query.Where(r => r.ProcessingStatus == MigrationProcessingStatus.Processing),
            "Migrated" => query.Where(r =>
                r.ProcessingStatus == MigrationProcessingStatus.Completed ||
                (r.ProcessingStatus == null && (r.MigrationStatus == MigrationStatus.MigratedToExisting || r.MigrationStatus == MigrationStatus.MigratedAsNew))),
            "Failed Migration" => query.Where(r =>
                r.ProcessingStatus == MigrationProcessingStatus.Failed || r.ProcessingStatus == MigrationProcessingStatus.RetryPending),
            "Ready for Migration" => query.Where(r =>
                r.ProcessingStatus == MigrationProcessingStatus.Pending ||
                (r.ProcessingStatus == null && r.MigrationStatus == MigrationStatus.ReadyForMigration)),
            "N/A" => query.Where(r => r.ProcessingStatus == null && r.MigrationStatus == null),
            _ => query,
        };

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.MigrationDate >= DateRangeFilter.StartOfDayUtc(request.DateFrom.Value));
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.MigrationDate < DateRangeFilter.EndOfDayExclusive(request.DateTo.Value));
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        // Only the 4 page sizes the UI dropdown actually offers are honored - anything else
        // (tampered query string, future UI change forgetting to update both ends) falls back to
        // the default rather than letting an arbitrary page size through.
        var pageSize = request.PageSize is 10 or 25 or 50 or 100 ? request.PageSize : 25;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.MigrationDate)
            .ThenBy(r => r.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MigrationListItemDto>
        {
            Items = items.Select(MapToListItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    private static MigrationListItemDto MapToListItem(MigrationRecord r)
    {
        var consolidatedStatus = ConsolidatedMigrationStatus(r);
        return new()
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            MigrationDate = r.MigrationDate,
            RdCode = r.RdCode,
            RdName = r.RdName,
            EntryNumbersCsv = r.EntryNumbersCsv,
            Title = r.Title,
            TitleType = r.TitleType?.ToString(),
            MigrationStatus = consolidatedStatus,
            SdStatus = r.SdStatus?.ToString() ?? string.Empty,
            MigratedTo = r.MigratedToRdName ?? string.Empty,
            MigrationType = r.MigrationType?.ToString(),
            ProcessedBy = r.ProcessedByUsername,
            RangeEntryNumber = NormalizeEntryNumberRange(r.RangeEntryNumber),
            ReasonForFailing = r.LastFailureReason,
            TargetSystemValidation = r.TargetSystemValidation,
            TargetSystemActivationStatus = r.TargetSystemActivationStatus,
            // Keyed off the consolidated display status (client requirement, 2026-11-27), not the
            // raw ProcessingStatus enum - "Ready for Migration" has two underlying paths
            // (ProcessingStatus = Pending, the legacy System-Migration one; or ProcessingStatus =
            // null with MigrationStatus = ReadyForMigration, every freshly-generated
            // Entry-Number-Range x Title-Sequence combination from Manual Validation) and both must
            // disable View/Reprocess identically - there is no "Initiate Migration" button left
            // anywhere on this page for a human to act on either one from here.
            CanView = consolidatedStatus is not ("Ready for Migration" or "Migrating"),
            CanReprocess = r.ProcessingStatus is MigrationProcessingStatus.Failed or MigrationProcessingStatus.RetryPending,
        };
    }

    // Consolidated Migration Status (spec: Migration Monitoring page) - every Entry Record (and
    // each Associated Title Record sibling, which is the same entity) displays exactly one of 5
    // values, replacing the raw ProcessingStatus/MigrationStatus enum text this used to show
    // directly (e.g. "RetryPending"/"Migrated to Existing Title/Entry Record" never actually
    // appear to the user now - only this consolidated set does). This is presentation-only: the
    // underlying MigrationStatus/MigrationProcessingStatus enums and their own StatusDisplay maps
    // are unchanged and still drive DB storage (MigrationRecordConfiguration's HasConversion) and
    // the Action History table's per-row status text.
    private static string ConsolidatedMigrationStatus(MigrationRecord r)
    {
        if (r.ProcessingStatus.HasValue)
        {
            return r.ProcessingStatus.Value switch
            {
                MigrationProcessingStatus.Processing => "Migrating",
                MigrationProcessingStatus.Completed => "Migrated",
                MigrationProcessingStatus.Failed or MigrationProcessingStatus.RetryPending => "Failed Migration",
                MigrationProcessingStatus.Pending => "Ready for Migration",
                _ => "N/A",
            };
        }

        return r.MigrationStatus switch
        {
            MigrationStatus.ReadyForMigration => "Ready for Migration",
            MigrationStatus.MigratedToExisting or MigrationStatus.MigratedAsNew => "Migrated",
            _ => "N/A",
        };
    }

    // Both OcrExtractionRecords.EntryNumberRange and ManualValidationRequests.EntryNumberRange
    // (MigrationRecord.RangeEntryNumber's two possible sources) default to the literal string "0"
    // when no real range has ever been set - that default isn't a meaningful range to show, so it
    // displays the same as "no range at all".
    private static string NormalizeEntryNumberRange(string? range) =>
        string.IsNullOrWhiteSpace(range) || range == "0" ? "N/A" : range;

    // "Target RD Not Identified" (Manual Corrections spec section 3): true whenever RdCode doesn't
    // resolve to any active RegistryOffice CodeLookup - this is the only realistic way RdCode can
    // be "wrong" since the column itself is required/non-null. Computed on read, not stored -
    // becomes false automatically the moment ApplyRdCodeCorrectionAsync sets a resolvable code.
    private async Task<bool> IsTargetRdNotIdentifiedAsync(string rdCode, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(rdCode) || !await _context.CodeLookups.AsNoTracking()
            .AnyAsync(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.IsActive && c.Code == rdCode, cancellationToken);

    public async Task<MigrationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.MigrationRecords.AsNoTracking()
            .Include(r => r.Documents).ThenInclude(d => d.CodeLookup)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        // Associated Title Records: LEVEL 1 ONLY (2026-11-16, Entry Number-based consolidation
        // across processing instances - supersedes the same-day folder-wise grouping fix, which
        // incorrectly made FolderPath part of this key). Every MigrationRecords row sharing the
        // same RdCode + RangeEntryNumber as this one (corrected 2026-11-27 from EntryNumbersCsv,
        // which is now the preserved, display-only original CSV and no longer varies per entry
        // number - RangeEntryNumber is the real per-combination split value) - each row already represents exactly one
        // Title (Title/TitleSequence are scalar per-row fields), so siblings under the same Entry
        // Number are the "other Titles" associated with this Entry Record, spanning every
        // processing instance (physical source folder) that Entry Number was ever processed
        // under, not just this one record's own instance. FolderPath is deliberately NOT part of
        // this key: the same Entry Number can legitimately be processed multiple times under
        // different folders, and every one of those processing instances must consolidate into
        // this SAME Associated Title Records list, never split into separate ones just because the
        // folder changed. Mirrors ManualValidationService.GetEntryNumberGroupAsync's identical
        // Level-1 key. Sorted by Title Sequence (numerically where it parses as a number, non-
        // numeric/blank values last) rather than insertion order - TitleSequence is a free-text
        // NVARCHAR column, so this can't be translated to SQL and is applied after materializing
        // the small sibling set.
        //
        // Supporting Documents (below, record.Documents) deliberately stay scoped to THIS record's
        // own rows only, never aggregated across the broader Level-1 group here - each sibling
        // MigrationRecord (regardless of which processing instance it came from) independently
        // owns its own MigrationDocuments and goes through Initiate Migration/Reprocess on its own
        // schedule; consolidating their documents into one shared view would risk misrepresenting
        // one sibling's own migration outcome as another's ("Preserve individual migration
        // statuses... do not incorrectly merge independent migration status records").
        var titleRecords = (await _context.MigrationRecords.AsNoTracking()
            .Where(m => m.RdCode == record.RdCode && m.RangeEntryNumber == record.RangeEntryNumber)
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken))
            .OrderBy(m => decimal.TryParse(m.TitleSequence, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seq) ? seq : decimal.MaxValue)
            .ThenBy(m => m.TitleSequence)
            .ToList();

        // Action History: reads the existing MigrationRecords_Log audit trail (populated entirely
        // by trg_MigrationRecords_I/U/D - dct_sd_main.sql Section 23), not a new write path.
        var history = await _context.MigrationRecordLogEntries.AsNoTracking()
            .Where(h => h.Id == record.Id)
            .OrderByDescending(h => h.Log_Date)
            .ToListAsync(cancellationToken);

        // record.Documents always has one row per supporting document from the moment this record
        // entered Migration Monitoring (Status = Pending until the SD Auto Conversion API
        // responds) - no JSON-blob fallback needed anymore; MapToDetail maps straight from these
        // real rows exactly as it always has for a record that had already been attempted.
        var isTargetRdNotIdentified = await IsTargetRdNotIdentifiedAsync(record.RdCode, cancellationToken);
        // Keyed directly on RD Code resolution now (not ProcessingStatus - there is no more batch
        // queue distinguishing "resolved, auto-queued" from "unresolved, null"; every
        // System-Migration row starts at ProcessingStatus = null regardless, same as a
        // Manual-Validation row). ProcessingStatus is
        // still checked here (mirroring MarkReadyForMigrationAsync's own guard) so a record that
        // has already entered the queue/been processed never shows "Ready for Migration" again,
        // even in the edge case where its RD Code's CodeLookup is later deactivated.
        var canMarkReadyForMigration = record.MigrationType == MigrationType.SystemMigration
            && record.ProcessingStatus is null
            && isTargetRdNotIdentified;

        // New "Ready for Migration" trigger (client requirement, 2026-11-27), independent of the
        // legacy System-Migration RD-Code-fix gate above - shown ONLY when this Entry Record's own
        // Supporting Document Status is "Supporting Documents Migrated with N/A" AND its own
        // Migration Status is anything other than "Migrated" (see the client's own worked example:
        // 4 "Others" documents all N/A, SdStatus = MigratedWithNA, but Migration Status already
        // "Migrated" - button must stay hidden there). Mutually exclusive with
        // canMarkReadyForMigration in practice (the legacy gate only ever applies to a
        // never-yet-processed row, which can't have a non-null SdStatus - see
        // RecalculateSupportingDocumentStatusAsync's own early-return while any document is still
        // Pending) - canMarkReadyForMigration still takes precedence if both were ever somehow true.
        var canTriggerReadyForMigration = !canMarkReadyForMigration
            && record.SdStatus == SupportingDocumentStatus.MigratedWithNA
            && ConsolidatedMigrationStatus(record) != "Migrated";

        // Action History (spec section 10): merges the existing trigger-based MigrationRecords_Log
        // audit trail (`history` above - system-level processing transitions) with user-initiated
        // actions (Save / Insert as New Image / Confirm Duplicate / Ready for Migration), written
        // via the generic RecordHistory mechanism exactly like Manual Validation already does -
        // MigrationRecordLogEntry/MigrationRecords_Log is read-only (populated solely by DB
        // triggers), so user actions can never be written there.
        var userActions = await _context.RecordHistory.AsNoTracking()
            .Where(h => h.TableName == RecordHistoryTables.MigrationRecords && h.RecordId == record.Id)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return MapToDetail(record, titleRecords, history, isTargetRdNotIdentified, canMarkReadyForMigration, canTriggerReadyForMigration, userActions);
    }

    // Streams a supporting document's image straight from disk, using SourceImagePath - the
    // real DCT-SD path submitted to the SD Auto Conversion API - never a client-supplied path.
    // documentId + migrationId are both checked together so a document can only ever be resolved
    // against the Entry Record it actually belongs to. There is no equivalent local path for the
    // PHILARIS-RD (target) side - DestinationPath is a path on that external system, which this
    // app has no filesystem access to; only the source image can be served.
    public async Task<string?> GetDocumentImagePathAsync(int migrationId, int documentId, CancellationToken cancellationToken = default) =>
        await _context.MigrationDocuments.AsNoTracking()
            .Where(d => d.Id == documentId && d.MigrationRecordId == migrationId)
            .Select(d => d.SourceImagePath)
            .FirstOrDefaultAsync(cancellationToken);

    // The ONLY place that ever calls the external SD Auto Conversion API, regardless of origin.
    // No UI on Migration Monitoring triggers this directly anymore (client requirement,
    // 2026-11-27: Migration Monitoring is view-only) - it's called automatically, once per entry
    // number, by ProcessEntryNumbersSequentiallyAsync right after
    // ManualValidationService.MarkReadyForMigrationAsync creates the MigrationRecord row(s).
    // Deliberately never touches OcrExtractionRecords - every input (RdCode, TitleSequence,
    // EntryNumbersCsv, record.Documents) comes straight off the MigrationRecord itself, which
    // already has its own copy of each from the moment it was created.
    public async Task InitiateMigrationAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.MigrationRecords.AsNoTracking()
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        if (record.ProcessingStatus is not (null or MigrationProcessingStatus.RetryPending))
        {
            throw new ConflictException("This Entry Record is already being migrated or has already completed.");
        }

        // record.Documents is the sole source of truth for this record's supporting documents -
        // one row per document, created (Status = Pending) the moment this record was marked
        // Ready for Migration (see ManualValidationService.MarkReadyForMigrationAsync), no longer
        // re-parsed from Document_Json on every attempt.
        if (record.Documents.Count == 0)
        {
            throw new BusinessValidationException("No supporting documents found for this Entry Record.");
        }

        if (!decimal.TryParse(record.TitleSequence, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var titleSeq))
        {
            throw new BusinessValidationException("Title Sequence is missing or invalid for this Entry Record.");
        }

        // Atomic claim via EF Core's ExecuteUpdateAsync, so the comparison goes through the real
        // MigrationProcessingStatus enum rather than a hand-written string literal (ProcessingStatus
        // is stored as its display text, e.g. "Retry Pending" with a space - a literal 'RetryPending'
        // would silently never match). Prevents two near-simultaneous clicks on this button from
        // both calling the external API for the same Entry Record. An external HTTP call must not
        // hold a DB transaction open while it runs, so the claim is its own statement/SaveChanges,
        // not wrapped around the API call.
        var claimedAt = DateTime.UtcNow;
        var claimed = await _context.MigrationRecords
            .Where(m => m.Id == id && (m.ProcessingStatus == null || m.ProcessingStatus == MigrationProcessingStatus.RetryPending))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcessingStatus, MigrationProcessingStatus.Processing)
                .SetProperty(m => m.ProcessingStartedAt, claimedAt)
                .SetProperty(m => m.LastAttemptAt, claimedAt)
                .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                // Cleared on every new attempt (not just on success) - otherwise a retry's own
                // "Processing" log row still shows the PREVIOUS attempt's failure message, since
                // this column isn't touched again until/unless this attempt itself fails too.
                .SetProperty(m => m.LastFailureReason, (string?)null),
                cancellationToken);

        if (claimed == 0)
        {
            throw new ConflictException("This Entry Record is already being migrated or has already completed.");
        }

        var tracked = await _context.MigrationRecords
            .Include(r => r.Documents)
            .FirstAsync(r => r.Id == id, cancellationToken);

        await ProcessMigrationAsync(tracked, titleSeq, isExplicitReprocess: false, cancellationToken);
    }

    // Drives migration for every entry number a single Manual Validation "Ready for Migration"
    // confirm just created (client requirement, 2026-11-27: each entry number is submitted to the
    // external API individually and sequentially - never in parallel, never as one combined
    // request - and one entry failing must never stop the others). Called from a background work
    // item enqueued by ManualValidationController.ReadyForMigration via IBackgroundTaskQueue, so
    // this never runs on the request thread and the user's Confirm click returns immediately.
    //
    // Each id is processed through the same InitiateMigrationAsync/ProcessMigrationAsync path a
    // manual "Initiate Migration" click used to use - its atomic claim already makes a second call
    // for the same id a safe no-op, so this loop needs no additional duplicate-prevention of its
    // own. InitiateMigrationAsync only ever throws BEFORE that claim (NotFoundException,
    // BusinessValidationException for no documents/invalid Title Sequence, or ConflictException if
    // something else already claimed it) - everything after the claim is caught internally by
    // ProcessMigrationAsync and always leaves the record in a terminal/retryable status with
    // LastFailureReason set. A pre-claim throw does NOT update the record at all, so one is
    // recorded here (guarded to never clobber a status InitiateMigrationAsync itself already set)
    // - otherwise that entry would sit at ProcessingStatus = null forever with no failure reason
    // shown anywhere on Migration Monitoring.
    public async Task ProcessEntryNumbersSequentiallyAsync(IReadOnlyList<int> migrationRecordIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in migrationRecordIds)
        {
            try
            {
                await InitiateMigrationAsync(id, cancellationToken);
            }
            catch (ConflictException)
            {
                // Already claimed/completed by something else (e.g. a duplicate Confirm click
                // racing this same loop) - nothing to do, move on to the next entry number.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sequential migration initiation failed for MigrationRecord {Id}.", id);
                await _context.MigrationRecords
                    .Where(m => m.Id == id && m.ProcessingStatus == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.ProcessingStatus, MigrationProcessingStatus.Failed)
                        .SetProperty(m => m.LastFailureReason, Truncate(ex.Message, 500))
                        .SetProperty(m => m.LastAttemptAt, DateTime.UtcNow)
                        .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1),
                        cancellationToken);
            }
        }
    }

    // Shared by InitiateMigrationAsync and ReprocessAsync - "tracked" must already be claimed
    // (ProcessingStatus = Processing) by the caller before this is called; this only does the
    // actual submit-and-process-response work and the Processing -> terminal-status transition,
    // identically regardless of which origin (Manual Validation or System Migration) or which
    // action (first attempt or Reprocess) claimed it. isExplicitReprocess suppresses this method's
    // own "Attempt N completed successfully" auto-logging (below) when the caller is
    // ReprocessAsync, which always writes its own "Reprocess Migration" entry (with the user's
    // actual remarks) right after this returns - without the flag, a Reprocess click would log
    // two separate "Reprocess Migration" entries for the same action.
    private async Task ProcessMigrationAsync(MigrationRecord tracked, decimal titleSeq, bool isExplicitReprocess, CancellationToken cancellationToken)
    {
        try
        {
            // tracked.Documents is the sole source of truth for this record's supporting
            // documents - one row per document, created (Status = Pending) the moment this record
            // entered Migration Monitoring (see ManualValidationService.MarkReadyForMigrationAsync),
            // no longer re-parsed from JSON on every attempt. This is also what makes a Document Type correction
            // (ApplyRealDocumentCorrectionsAsync, which sets CodeLookupId/DocumentName/
            // SourceImagePath directly on these same rows) actually take effect on the next
            // attempt, and what makes "update the existing row, never insert a new one" automatic
            // below - the row being updated IS the row that was submitted.
            var documents = tracked.Documents.OrderBy(d => d.Id).ToList();

            // A document still classified "Others" has CodeLookupId == null (set at creation,
            // kept in sync by Document Type correction) - never submitted to the SD Auto
            // Conversion API at all (spec: "N/A - the image was identified as 'Others' and was
            // not migrated to PHILARIS-RD").
            var documentsToSubmit = documents.Where(d => d.CodeLookupId is not null).ToList();

            if (documentsToSubmit.Count == 0)
            {
                // Nothing can be submitted - every document is still Others. No API call to make,
                // and no failure either - as "done" as the system can make it without a human
                // first correcting at least one Document Type (still possible afterward via
                // Migration Details' Document Type correction, which targets any document with a
                // null CodeLookupId - NotApplicable rows qualify).
                foreach (var document in documents)
                {
                    UpsertNotApplicableDocument(document);
                }

                // No API call was made - nothing to merge into Document_Json, left exactly as it was.
                tracked.ProcessingStatus = MigrationProcessingStatus.Completed;
                tracked.ProcessingCompletedAt = DateTime.UtcNow;
                tracked.MigratedToRdName = tracked.RdName;
                tracked.TargetSystemActivationStatus = "Activated";
                await _context.SaveChangesAsync(cancellationToken);
                await RecalculateSupportingDocumentStatusAsync(tracked.Id, cancellationToken);
                await MarkManualValidationRequestMigratedIfCompleteAsync(tracked.RequestNumber, cancellationToken);
                return;
            }

            var request = new SdAutoConversionRequest
            {
                RdCode = tracked.RdCode,
                TitleSeq = titleSeq,
                // RangeEntryNumber (corrected 2026-11-27), not EntryNumbersCsv - the latter is the
                // preserved, display-only original Entry Number CSV and must never reach the
                // external API.
                EntryNumber = tracked.RangeEntryNumber ?? string.Empty,
                SupportingDocuments = documentsToSubmit
                    .Select(d => new SdAutoConversionDocumentRequest
                    {
                        DocumentId = d.DocumentName,
                        ImagePath = d.SourceImagePath!,
                    })
                    .ToList(),
            };

            var result = await _sdAutoConversionClient.CreateSdConversionAsync(request, cancellationToken);
            var now = DateTime.UtcNow;
            tracked.ApiResponseJson = result.RawResponseJson;
            tracked.ApiResponseAt = now;

            if (!result.IsSuccess || result.Response is null)
            {
                // Section 5.5: never overwrite Document_Json with an empty/invalid response -
                // leave it exactly as it was.
                tracked.ProcessingStatus = MigrationProcessingStatus.RetryPending;
                tracked.LastFailureReason = Truncate(result.ErrorMessage ?? "The SD Auto Conversion service returned an unexpected empty response.", 500);
                tracked.TargetSystemActivationStatus = "Not Activated";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            // Entry Record / Associated Title Target System Validation (spec: "Display Target
            // System Validation Based on Business Rules and Migration API Response") - both facts
            // come from this same successful response, regardless of whether every submitted
            // document ends up recognized below, so both are set unconditionally here rather than
            // only on a fully-Completed outcome.
            tracked.TargetSystemValidation = MapEntryTargetSystemValidation(result.Response.EntryStatus);
            tracked.TitleTargetSystemValidation = MapTitleTargetSystemValidation(result.Response.TitleStatus);

            // Positional correlation - same rationale as the former SystemMigrationService
            // .ProcessOneAsync (the API's documentId/sequence are not reliable per-item keys; see
            // that history for the confirmed real-world example). documentsToSubmit[i] <->
            // request.SupportingDocuments[i] <-> images[i], same order as submitted - the row
            // being updated IS the row that was submitted, so there is no separate "existing row"
            // lookup needed anymore.
            var images = result.Response.Images;
            var recognizedCount = 0;
            var insertedCount = 0;
            var duplicateCount = 0;
            var missingOrUnrecognized = new List<string>();

            for (var i = 0; i < documentsToSubmit.Count; i++)
            {
                var document = documentsToSubmit[i];
                var submitted = request.SupportingDocuments[i];
                var image = i < images.Count ? images[i] : null;

                if (image is null || (image.Status != "Inserted" && image.Status != "Duplicate SD"))
                {
                    missingOrUnrecognized.Add(submitted.DocumentId);
                    var reason = image is null
                        ? "This document was not returned by the SD Auto Conversion API response."
                        : $"The SD Auto Conversion API returned an unrecognized status '{image.Status}' for this document.";
                    UpsertFailedMigrationDocument(document, reason);
                    continue;
                }

                recognizedCount++;
                if (image.Status == "Duplicate SD")
                {
                    duplicateCount++;
                }
                else
                {
                    insertedCount++;
                }

                UpsertMigrationDocument(document, image);
            }

            // More response images than were submitted - a genuine "unmatched" case. Logged for
            // investigation, never inserted (client requirement, 2026-11-12).
            if (images.Count > documentsToSubmit.Count)
            {
                for (var i = documentsToSubmit.Count; i < images.Count; i++)
                {
                    _logger.LogWarning(
                        "InitiateMigration: MigrationRecords.Id {Id} - response contained an extra image at position {Position} (documentId {DocumentId}, sequence {Sequence}, fileName {FileName}) with no corresponding submitted document - ignored, not inserted.",
                        tracked.Id, i, images[i].DocumentId, images[i].Sequence, images[i].FileName);
                }
            }

            // Section 5.3: merge the API response into the same Document_Json column, preserving
            // the original supporting-document information rather than overwriting it.
            tracked.DocumentJson = MergeDocumentJsonWithApiResponse(tracked.DocumentJson, result.Response);

            if (recognizedCount == documentsToSubmit.Count && recognizedCount > 0)
            {
                tracked.ProcessingStatus = MigrationProcessingStatus.Completed;
                tracked.ProcessingCompletedAt = now;
                tracked.MigratedToRdName = tracked.RdName;
                tracked.TargetSystemActivationStatus = "Activated";
            }
            else
            {
                tracked.ProcessingStatus = MigrationProcessingStatus.RetryPending;
                tracked.LastFailureReason = Truncate(
                    $"{missingOrUnrecognized.Count} of {documentsToSubmit.Count} supporting document(s) missing or unrecognized in the API response: {string.Join(", ", missingOrUnrecognized)}",
                    500);
                tracked.TargetSystemActivationStatus = "Not Activated";
            }

            await _context.SaveChangesAsync(cancellationToken);
            await RecalculateSupportingDocumentStatusAsync(tracked.Id, cancellationToken);
            if (tracked.ProcessingStatus == MigrationProcessingStatus.Completed)
            {
                await MarkManualValidationRequestMigratedIfCompleteAsync(tracked.RequestNumber, cancellationToken);
            }

            // Reprocess Migration (spec section 10.2) - re-clicking Initiate Migration on a record
            // that already failed at least once IS that reprocess attempt (for a RetryPending row
            // claimed via InitiateMigrationAsync), so it's logged under that name specifically when
            // this isn't the first attempt. Suppressed for ReprocessAsync's own explicit Reprocess
            // action (for a Failed row), which writes its own RecordHistory entry separately.
            if (!isExplicitReprocess && tracked.AttemptCount > 1 && tracked.ProcessingStatus == MigrationProcessingStatus.Completed)
            {
                var reprocessDisplayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
                _context.RecordHistory.Add(new RecordHistory
                {
                    TableName = RecordHistoryTables.MigrationRecords,
                    RecordId = tracked.Id,
                    RefNo = tracked.RequestNumber,
                    Action = "Reprocess Migration",
                    Remarks = $"Attempt {tracked.AttemptCount} completed successfully.",
                    ByUserId = _currentUserService.UserId,
                    ByUsername = reprocessDisplayName ?? "system",
                    CreatedAt = DateTime.UtcNow,
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Never leave the record stuck in "Processing" forever.
            tracked.ProcessingStatus = MigrationProcessingStatus.RetryPending;
            tracked.LastFailureReason = Truncate(ex.Message, 500);
            tracked.TargetSystemActivationStatus = "Not Activated";
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // Client requirement, 2026-11-28: once every Entry-Number-Range x Title-Sequence combination
    // generated for one Manual Validation sibling (one ManualValidationRequests row, identified by
    // RequestNumber) has genuinely finished migrating (ProcessingStatus = Completed - never just
    // "attempted"), stamp that sibling's MigratedAt so it drops out of Manual Validation's own
    // listing (BuildSearchQuery's base query already filters MigratedAt IS NOT NULL - it was
    // simply never populated before this). Deliberately touches ONLY ManualValidationRequests -
    // MigrationRecords/MigrationDocuments/RecordHistory (Migration Monitoring's own data and
    // history) are never modified or hidden by this. A no-op for a non-Manual-Validation-origin
    // record (no matching RequestNumber row) or one already stamped.
    private async Task MarkManualValidationRequestMigratedIfCompleteAsync(string requestNumber, CancellationToken cancellationToken)
    {
        var allCompleted = await _context.MigrationRecords.AsNoTracking()
            .Where(m => m.RequestNumber == requestNumber)
            .AllAsync(m => m.ProcessingStatus == MigrationProcessingStatus.Completed, cancellationToken);

        if (!allCompleted)
        {
            return;
        }

        await _context.ManualValidationRequests
            .Where(v => v.RequestNumber == requestNumber && v.MigratedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.MigratedAt, DateTime.UtcNow), cancellationToken);
    }

    // Duplicate SD resolution (spec sections 6-8): the two actions a Duplicate SD image can
    // resolve to are Insert as New Image (this DCT-SD image is additionally registered,
    // PHILARIS-RD's existing image is kept) and Confirm Duplicate (PHILARIS-RD's existing image is
    // kept, this DCT-SD image is never migrated) - see ConfirmDuplicateAsync below.
    //
    // Insert as New Image calls the SAME SD Auto Conversion API used for the original migration
    // (client-confirmed, 2026-11-13: there is no separate PHILARIS-RD write endpoint - submitting
    // this one document to CreateSDConversion again, under a renamed file name, IS how it gets
    // additionally registered in PHILARIS-RD without replacing the existing image there) -
    // submitting only this one document, with the same RdCode/TitleSeq/EntryNumber already used
    // for this Entry Record's own migration. The file is renamed first (continuing this document
    // type's own sequence, same convention as ApplyRealDocumentCorrectionsAsync) so the API sees a
    // genuinely new file rather than the one it already flagged as a duplicate.
    public async Task<MigrationDetailDto> InsertAsNewDocumentAsync(int migrationId, int documentId, string remarks, CancellationToken cancellationToken = default)
    {
        var trimmedRemarks = ValidateRemarks(remarks);
        var document = await GetDuplicateDocumentForActionAsync(migrationId, documentId, cancellationToken);
        var record = document.MigrationRecord;

        if (!decimal.TryParse(record.TitleSequence, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var titleSeq))
        {
            throw new BusinessValidationException("Title Sequence is missing or invalid for this Entry Record.");
        }

        var originalPath = document.SourceImagePath;
        if (string.IsNullOrWhiteSpace(originalPath) || !System.IO.File.Exists(originalPath))
        {
            throw new BusinessValidationException($"The original image file for '{document.FileName}' could not be found on disk.");
        }

        var siblingDocuments = await _context.MigrationDocuments.AsNoTracking()
            .Where(d => d.MigrationRecordId == migrationId)
            .ToListAsync(cancellationToken);

        var code = document.CodeLookup?.Code ?? document.DocumentName;
        var name = document.CodeLookup?.Name ?? document.DocumentName;

        var directory = Path.GetDirectoryName(originalPath)!;
        var extension = Path.GetExtension(originalPath);
        var sequence = NextSequenceForRealDocuments(siblingDocuments, code, name);
        var newFileName = $"{SanitizeForFileName(code)}_{SanitizeForFileName(name)}_{sequence}{extension}";
        var newPath = Path.Combine(directory, newFileName);

        var renamed = !string.Equals(newPath, originalPath, StringComparison.OrdinalIgnoreCase);
        if (renamed)
        {
            System.IO.File.Move(originalPath, newPath);
        }

        try
        {
            var request = new SdAutoConversionRequest
            {
                RdCode = record.RdCode,
                TitleSeq = titleSeq,
                // RangeEntryNumber (corrected 2026-11-27), not EntryNumbersCsv - see
                // ProcessMigrationAsync's identical request construction for why.
                EntryNumber = record.RangeEntryNumber ?? string.Empty,
                SupportingDocuments = new List<SdAutoConversionDocumentRequest>
                {
                    new() { DocumentId = document.DocumentName, ImagePath = newPath },
                },
            };

            var result = await _sdAutoConversionClient.CreateSdConversionAsync(request, cancellationToken);
            var image = result.IsSuccess ? result.Response?.Images.FirstOrDefault() : null;

            if (!result.IsSuccess || image is null || (image.Status != "Inserted" && image.Status != "Duplicate SD"))
            {
                var reason = !result.IsSuccess
                    ? (result.ErrorMessage ?? "The SD Auto Conversion service returned an unexpected empty response.")
                    : image is null
                        ? "This document was not returned by the SD Auto Conversion API response."
                        : $"The SD Auto Conversion API returned an unrecognized status '{image.Status}' for this document.";

                document.ReasonForFailing = Truncate(reason, 500);
                await _context.SaveChangesAsync(cancellationToken);
                if (renamed)
                {
                    System.IO.File.Move(newPath, originalPath);
                }

                throw new BusinessValidationException("Unable to insert the image as a new image. Please review the failure reason.");
            }

            if (image.Status == "Duplicate SD")
            {
                // The API itself still recognized this as a duplicate even under the renamed
                // file - the insertion did not actually happen, so this stays Duplicate SD
                // (spec: retain the Duplicate SD status on failure), not InsertedAsNew.
                document.ReasonForFailing = "The SD Auto Conversion API still reported this document as a duplicate after renaming.";
                await _context.SaveChangesAsync(cancellationToken);
                if (renamed)
                {
                    System.IO.File.Move(newPath, originalPath);
                }

                throw new BusinessValidationException("Unable to insert the image as a new image. Please review the failure reason.");
            }

            document.FileName = !string.IsNullOrWhiteSpace(image.FileName) ? image.FileName : newFileName;
            document.SourceImagePath = newPath;
            document.DestinationPath = image.DestinationPath;
            document.SizeBytes = image.SizeBytes;
            document.Sequence = image.Sequence;
            document.ReasonForFailing = null;
            document.Status = MigrationDocumentStatus.InsertedAsNew;
            // The only reachable success outcome here is image.Status == "Inserted" (a lingering
            // "Duplicate SD" throws above instead) - Not Existing per the same per-image mapping
            // UpsertMigrationDocument uses (spec section 3).
            document.TargetSystemValidation = "Not Existing";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            if (renamed && System.IO.File.Exists(newPath) && !System.IO.File.Exists(originalPath))
            {
                System.IO.File.Move(newPath, originalPath);
            }

            throw;
        }

        document.PerformedByUserId = _currentUserService.UserId;
        document.PerformedByUsername = _currentUserService.Username;
        document.ActionDate = DateTime.UtcNow;

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.MigrationRecords,
            RecordId = migrationId,
            RefNo = record.RequestNumber,
            Action = "Insert as New Image",
            Remarks = $"Image File Name: {document.FileName} | Remarks: {trimmedRemarks}",
            ByUserId = _currentUserService.UserId,
            ByUsername = displayName ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
        await RecalculateSupportingDocumentStatusAsync(migrationId, cancellationToken);

        return await GetByIdAsync(migrationId, cancellationToken);
    }

    public async Task<MigrationDetailDto> ConfirmDuplicateAsync(int migrationId, int documentId, string remarks, CancellationToken cancellationToken = default)
    {
        var trimmedRemarks = ValidateRemarks(remarks);
        var document = await GetDuplicateDocumentForActionAsync(migrationId, documentId, cancellationToken);

        document.Status = MigrationDocumentStatus.ConfirmedDuplicate;
        document.PerformedByUserId = _currentUserService.UserId;
        document.PerformedByUsername = _currentUserService.Username;
        document.ActionDate = DateTime.UtcNow;

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.MigrationRecords,
            RecordId = migrationId,
            RefNo = document.MigrationRecord.RequestNumber,
            Action = "Confirm Duplicate",
            Remarks = $"Image File Name: {document.FileName} | Remarks: {trimmedRemarks}",
            ByUserId = _currentUserService.UserId,
            ByUsername = displayName ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
        await RecalculateSupportingDocumentStatusAsync(migrationId, cancellationToken);

        return await GetByIdAsync(migrationId, cancellationToken);
    }

    // Remarks validation (spec sections 7.1/8.1): mandatory, trimmed, at least 10 characters -
    // enforced here (server side) in addition to the client-side check, per spec section 14.
    private static string ValidateRemarks(string? remarks)
    {
        var trimmed = remarks?.Trim() ?? string.Empty;
        if (trimmed.Length < 10)
        {
            throw new BusinessValidationException("Remarks is required and must be at least 10 characters.");
        }

        return trimmed;
    }

    // Supporting Document Status recalculation (spec: Migration Monitoring page) - called after
    // Save (corrections), Insert as New Image, Confirm Duplicate, and Initiate Migration's own
    // completion branch, so there is one single source of truth instead of each caller computing
    // it inline. Follows a defined priority (most to least severe/actionable), never overridden by
    // a lower one:
    //   1. Failed      - at least one document's migration failed outright.
    //   2. AllDuplicate / PartiallyDuplicate - at least one Duplicate SD remains unresolved.
    //   3. MigratedWithNA - at least one document is still "Others" (NotApplicable), nothing else
    //      outstanding.
    //   4. AllMigrated - everything resolved, no Duplicate/N/A/Failed left.
    // A record with no documents at all yet is left untouched (nothing to report).
    private async Task RecalculateSupportingDocumentStatusAsync(int migrationId, CancellationToken cancellationToken)
    {
        var documents = await _context.MigrationDocuments.AsNoTracking()
            .Where(d => d.MigrationRecordId == migrationId)
            .Select(d => d.Status)
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return;
        }

        // Every document is created Pending immediately at "Ready for Migration" - don't roll up
        // SdStatus at all while any of them haven't been attempted yet (e.g. a Document Type
        // correction on a still-unsubmitted record also calls this), or an all-Pending set would
        // otherwise fall into the "nothing duplicate/N-A" else branch below and be wrongly
        // reported as fully migrated before any attempt was ever made.
        if (documents.Any(s => s == MigrationDocumentStatus.Pending))
        {
            return;
        }

        SupportingDocumentStatus newStatus;
        if (documents.Any(s => s == MigrationDocumentStatus.Failed))
        {
            newStatus = SupportingDocumentStatus.Failed;
        }
        else
        {
            var duplicateCount = documents.Count(s => s == MigrationDocumentStatus.DuplicateSd);
            var notApplicableCount = documents.Count(s => s == MigrationDocumentStatus.NotApplicable);
            var resolvedOrMigratedCount = documents.Count(s =>
                s is MigrationDocumentStatus.Migrated or MigrationDocumentStatus.InsertedAsNew or MigrationDocumentStatus.ConfirmedDuplicate);

            newStatus = duplicateCount > 0
                ? (resolvedOrMigratedCount == 0 ? SupportingDocumentStatus.AllDuplicate : SupportingDocumentStatus.PartiallyDuplicate)
                : notApplicableCount > 0
                    ? SupportingDocumentStatus.MigratedWithNA
                    : SupportingDocumentStatus.AllMigrated;
        }

        var record = await _context.MigrationRecords.FirstAsync(r => r.Id == migrationId, cancellationToken);
        if (record.SdStatus != newStatus)
        {
            record.SdStatus = newStatus;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<MigrationDocument> GetDuplicateDocumentForActionAsync(int migrationId, int documentId, CancellationToken cancellationToken)
    {
        var document = await _context.MigrationDocuments
            .Include(d => d.MigrationRecord)
            .Include(d => d.CodeLookup)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.MigrationRecordId == migrationId, cancellationToken)
            ?? throw new NotFoundException("Supporting document", documentId);

        if (document.Status != MigrationDocumentStatus.DuplicateSd)
        {
            throw new ConflictException("This supporting document has already been resolved.");
        }

        return document;
    }

    // Manual corrections Save (spec section 4): persists RD Code (only when this record is
    // currently "Target RD Not Identified") and/or Others -> real Document Type corrections
    // together, in one transaction (a single SaveChangesAsync call), and records one "Save" Action
    // History entry listing only the fields that actually changed.
    public async Task<MigrationDetailDto> SaveCorrectionsAsync(int id, SaveMigrationCorrectionsRequestDto request, CancellationToken cancellationToken = default)
    {
        var record = await _context.MigrationRecords
            .Include(r => r.Documents).ThenInclude(d => d.CodeLookup)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        var changeDescriptions = new List<string>();

        // The hidden RdCode field always carries the record's current value, whether or not the
        // user actually touched the RD Code combobox (it isn't cleared like DocumentCorrectionsJson
        // is) - only an ACTUAL change counts as a correction request, otherwise every Save on an
        // already-identified record would incorrectly hit the "already identified" guard below.
        var newRdCode = request.RdCode?.Trim();
        if (!string.IsNullOrWhiteSpace(newRdCode) && !string.Equals(newRdCode, record.RdCode, StringComparison.Ordinal))
        {
            if (!await IsTargetRdNotIdentifiedAsync(record.RdCode, cancellationToken))
            {
                throw new BusinessValidationException("This Entry Record's Target RD is already identified and cannot be changed here.");
            }

            var resolvedRdName = await _context.CodeLookups.AsNoTracking()
                .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.IsActive && c.Code == newRdCode)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessValidationException("The selected RD Code was not found. Please select a valid RD Code.");

            var originalRdCode = record.RdCode;
            var originalRdName = record.RdName;
            record.RdCode = newRdCode;
            record.RdName = resolvedRdName;
            AddFieldChange(changeDescriptions, "RD Code", originalRdCode, record.RdCode);
            AddFieldChange(changeDescriptions, "RD Name", originalRdName, record.RdName);
        }

        var documentCorrections = ParseDocumentCorrections(request.DocumentCorrectionsJson);
        var renamesToRollBack = new List<(string NewPath, string OriginalPath)>();

        try
        {
            if (documentCorrections.Count > 0)
            {
                // record.Documents always has one row per supporting document from the moment
                // this record entered Migration Monitoring - there is no longer a "pending,
                // Document_Json-only" case to route to separately.
                await ApplyRealDocumentCorrectionsAsync(record, documentCorrections, renamesToRollBack, changeDescriptions, cancellationToken);
            }

            var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
            _context.RecordHistory.Add(new RecordHistory
            {
                TableName = RecordHistoryTables.MigrationRecords,
                RecordId = record.Id,
                RefNo = record.RequestNumber,
                Action = "Save",
                Remarks = BuildChangeRemarks(changeDescriptions),
                ByUserId = _currentUserService.UserId,
                ByUsername = displayName ?? "system",
                CreatedAt = DateTime.UtcNow,
            });

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

        await RecalculateSupportingDocumentStatusAsync(id, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // Ready for Migration on Migration Details (spec section 5) - distinct from
    // ManualValidationService.MarkReadyForMigrationAsync, which operates on a ManualValidationRequest
    // and creates a brand-new MigrationRecords row. This one operates on an EXISTING, legacy
    // MigrationType.SystemMigration MigrationRecords row (no new ones are created anymore - see
    // SearchAsync), for the one case that needs a human confirmation before migration can proceed:
    // its RD Code didn't resolve at creation time - the only realistic scenario this gate applies
    // to, since a Manual Validation-origin row already requires a resolved RD Code and no "Others"
    // documents before ManualValidationService ever creates it in the first place (see
    // MigrationDetailDto.CanMarkReadyForMigration for the exact visibility rule). Note: fixing RD
    // Code here no longer leads anywhere further automatically, since "Initiate Migration" has been
    // removed from this page (client requirement, 2026-11-27) - an accepted limitation for any
    // already-stuck legacy System-Migration row, since this origin is being retired.
    public async Task MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.MigrationRecords
            .Include(r => r.Documents).ThenInclude(d => d.CodeLookup)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        if (record.MigrationType != MigrationType.SystemMigration || record.ProcessingStatus is not null)
        {
            throw new ConflictException("This Entry Record is already in the migration queue or has already been processed.");
        }

        var blockers = new List<string>();
        if (await IsTargetRdNotIdentifiedAsync(record.RdCode, cancellationToken))
        {
            blockers.Add("RD Code");
        }

        // record.Documents always has one row per supporting document from the moment this record
        // entered Migration Monitoring - there is no longer a "pending, Document_Json-only" case.
        if (record.Documents.Any(d => d.CodeLookupId is null))
        {
            blockers.Add("Document Type");
        }

        if (record.Documents.Any(d => d.Status == MigrationDocumentStatus.DuplicateSd))
        {
            blockers.Add("Duplicate SD resolution");
        }

        if (blockers.Count > 0)
        {
            throw new BusinessValidationException($"Please complete the following before marking this Entry Record Ready for Migration: {string.Join(", ", blockers)}.");
        }

        // ProcessingStatus is left exactly as it already is (null) - there is no more batch queue
        // to enter. The record becomes an ordinary null-status row, same as if its RD Code had
        // resolved at creation time, which is what makes "Initiate Migration" appear next
        // (CanMarkReadyForMigration is now false, since RD Code is resolved).

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.MigrationRecords,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = "Ready for Migration",
            Remarks = "This Entry Record is now ready to Initiate Migration.",
            ByUserId = _currentUserService.UserId,
            ByUsername = displayName ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    // New "Ready for Migration" trigger on Migration Monitoring (client requirement, 2026-11-27) -
    // distinct from both MarkReadyForMigrationAsync above (legacy RD-Code fix, no migration call)
    // and ManualValidationService.MarkReadyForMigrationAsync (creates brand-new MigrationRecords
    // rows). This one re-triggers migration for an EXISTING MigrationRecords row that's already
    // "Supporting Documents Migrated with N/A" but not yet "Migrated" overall - see
    // GetByIdAsync's canTriggerReadyForMigration computation for the exact gate, re-checked here
    // server-side since a client can't be trusted to enforce it. Only records the Action History
    // entry and returns - the caller (MigrationsController) enqueues the actual background
    // processing via the same IBackgroundTaskQueue + ProcessEntryNumbersSequentiallyAsync path
    // Manual Validation's own Ready for Migration uses, so the logic itself is never duplicated.
    public async Task TriggerReadyForMigrationAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.MigrationRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        if (record.SdStatus != SupportingDocumentStatus.MigratedWithNA || ConsolidatedMigrationStatus(record) == "Migrated")
        {
            throw new ConflictException("This Entry Record is not eligible for Ready for Migration.");
        }

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.MigrationRecords,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = "Ready for Migration",
            Remarks = "This Entry Record is now ready to migrate.",
            ByUserId = _currentUserService.UserId,
            ByUsername = displayName ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Reprocess (Migration Details button, shown when ProcessingStatus = Failed or RetryPending -
    // both display as the same "Failed Migration" consolidated status, see
    // ConsolidatedMigrationStatus, so the user can't otherwise tell them apart) - a record nothing
    // automatically retries. Now that "Initiate Migration" no longer exists on Migration Monitoring
    // (client requirement, 2026-11-27), this is the ONLY way left to retry a RetryPending row (an
    // API-call-level failure - network/timeout/incomplete response - as opposed to Failed, reserved
    // for genuinely bad data: no supporting documents, an invalid Title Sequence) - without this,
    // such a row would be stuck forever with no way to resubmit it. Claims it itself (same
    // atomic-claim shape as InitiateMigrationAsync) and reuses the same ProcessMigrationAsync core -
    // identical regardless of origin.
    public async Task<MigrationDetailDto> ReprocessAsync(int id, string remarks, CancellationToken cancellationToken = default)
    {
        // Mandatory (spec: Reprocess Failed Migration), but no minimum length - unlike Insert as
        // New Image/Confirm Duplicate's 10-character rule, which is a different, unrelated action.
        var trimmedRemarks = remarks?.Trim() ?? string.Empty;
        if (trimmedRemarks.Length == 0)
        {
            throw new BusinessValidationException("Remarks is required.");
        }

        var preCheck = await _context.MigrationRecords.AsNoTracking()
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        // Captured before the claim below clears LastFailureReason - spec: "keep old message in
        // Log" when a Reprocess attempt fails again, so the previous failure reason isn't silently
        // discarded, only the live Reason for Failing column is overwritten with the latest one.
        var previousFailureReason = preCheck.LastFailureReason;

        if (preCheck.Documents.Count == 0)
        {
            throw new BusinessValidationException("No supporting documents found for this Entry Record.");
        }

        if (!decimal.TryParse(preCheck.TitleSequence, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var titleSeq))
        {
            throw new BusinessValidationException("Title Sequence is missing or invalid for this Entry Record.");
        }

        var claimedAt = DateTime.UtcNow;
        var claimed = await _context.MigrationRecords
            .Where(m => m.Id == id && (m.ProcessingStatus == MigrationProcessingStatus.Failed || m.ProcessingStatus == MigrationProcessingStatus.RetryPending))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcessingStatus, MigrationProcessingStatus.Processing)
                .SetProperty(m => m.ProcessingStartedAt, claimedAt)
                .SetProperty(m => m.LastAttemptAt, claimedAt)
                .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                .SetProperty(m => m.LastFailureReason, (string?)null),
                cancellationToken);

        if (claimed == 0)
        {
            throw new ConflictException("This Entry Record is not currently in a Failed Migration status, or is already being processed.");
        }

        var tracked = await _context.MigrationRecords
            .Include(r => r.Documents)
            .FirstAsync(r => r.Id == id, cancellationToken);

        await ProcessMigrationAsync(tracked, titleSeq, isExplicitReprocess: true, cancellationToken);

        var record = await _context.MigrationRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        var displayName = await _currentUserService.GetDisplayNameAsync(cancellationToken);
        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.MigrationRecords,
            RecordId = id,
            RefNo = record.RequestNumber,
            Action = "Reprocess Migration",
            Remarks = record.ProcessingStatus == MigrationProcessingStatus.Completed
                ? trimmedRemarks
                : Truncate($"{trimmedRemarks} | Outcome: Reprocess attempt did not complete. Previous failure reason: {(string.IsNullOrWhiteSpace(previousFailureReason) ? "(none)" : previousFailureReason)} | Latest failure reason: {record.LastFailureReason}", 500),
            ByUserId = _currentUserService.UserId,
            ByUsername = displayName ?? "system",
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync(cancellationToken);

        await RecalculateSupportingDocumentStatusAsync(id, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static List<DocumentCorrectionItemDto> ParseDocumentCorrections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<DocumentCorrectionItemDto>>(json, JsonOptions) ?? [];
    }

    // Document Type correction (spec section 2) - CodeLookupId null means this document's type is
    // still unresolved/"Others" (see MapToDetail's CanChangeDocumentType). The underlying DCT-SD
    // image file is renamed on disk, same <Code>_<Name>_<Sequence> convention as Manual
    // Validation's own ApplyDocumentTypeChange.
    private async Task ApplyRealDocumentCorrectionsAsync(
        MigrationRecord record,
        List<DocumentCorrectionItemDto> corrections,
        List<(string NewPath, string OriginalPath)> rollbacks,
        List<string> changeDescriptions,
        CancellationToken cancellationToken)
    {
        var documentsById = record.Documents.ToDictionary(d => d.Id);
        foreach (var change in corrections)
        {
            if (!documentsById.TryGetValue(change.DocumentId, out var document))
            {
                continue;
            }

            if (document.CodeLookupId is not null)
            {
                throw new BusinessValidationException("Only supporting documents currently classified as \"Others\" can have their Document Type corrected.");
            }

            var newType = await _context.CodeLookups.AsNoTracking()
                .FirstOrDefaultAsync(c => c.LookupType == CodeLookupTypes.DocumentType && c.IsActive && c.Code == change.Code, cancellationToken)
                ?? throw new BusinessValidationException("The selected Document Type was not found.");

            var originalFileName = document.FileName;

            if (!string.IsNullOrWhiteSpace(document.SourceImagePath))
            {
                if (!System.IO.File.Exists(document.SourceImagePath))
                {
                    throw new BusinessValidationException($"The original image file for '{originalFileName}' could not be found on disk. The Document Type change was not saved.");
                }

                var directory = Path.GetDirectoryName(document.SourceImagePath)!;
                var extension = Path.GetExtension(document.SourceImagePath);
                var sequence = NextSequenceForRealDocuments(record.Documents, newType.Code, newType.Name);
                var newFileName = $"{SanitizeForFileName(newType.Code)}_{SanitizeForFileName(newType.Name)}_{sequence}{extension}";
                var newPath = Path.Combine(directory, newFileName);

                if (!string.Equals(newPath, document.SourceImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Move(document.SourceImagePath, newPath);
                    rollbacks.Add((newPath, document.SourceImagePath));
                }

                document.SourceImagePath = newPath;
                document.FileName = newFileName;
            }

            document.CodeLookupId = newType.Id;
            // Kept in sync with CodeLookupId - submission (InitiateMigrationAsync) now sources
            // DocumentId straight from this row's own DocumentName, so without this a later
            // (re)submission would still send the stale "OTHERS" value to the external API even
            // after a successful correction.
            document.DocumentName = newType.Code;
            changeDescriptions.Add($"Supporting Document '{originalFileName}' - Document Type: Previous = 'Others', Current = '{newType.Name}'");
        }
    }

    private static int NextSequenceForRealDocuments(IEnumerable<MigrationDocument> documents, string code, string name)
    {
        var pattern = new Regex("^" + Regex.Escape(SanitizeForFileName(code)) + "_" + Regex.Escape(SanitizeForFileName(name)) + @"_(\d+)\.[^.]+$", RegexOptions.IgnoreCase);
        var max = 0;
        foreach (var document in documents)
        {
            var match = pattern.Match(document.FileName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > max)
            {
                max = n;
            }
        }

        return max + 1;
    }

    // Mirrors ManualValidationService.SanitizeForFileName exactly - a CodeLookups Document Name
    // can contain characters invalid in a file name (e.g. a "/").
    private static string SanitizeForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    // Same pattern as ManualValidationService's own copies - "<label>: Previous = '<before>',
    // Current = '<after>'", only when the value actually changed.
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

    // RecordHistory.Remarks is capped at 500 characters, same as ManualValidationService's own
    // BuildChangeRemarks.
    private const int RemarksMaxLength = 500;

    private static string BuildChangeRemarks(List<string> changeDescriptions)
    {
        if (changeDescriptions.Count == 0)
        {
            return "No changes were made.";
        }

        var combined = string.Join('\n', changeDescriptions);
        if (combined.Length <= RemarksMaxLength)
        {
            return combined;
        }

        const string suffix = "...";
        return combined[..(RemarksMaxLength - suffix.Length)] + suffix;
    }

    private static MigrationDetailDto MapToDetail(
        MigrationRecord r,
        IReadOnlyList<MigrationRecord> titleRecords,
        IReadOnlyList<MigrationRecordLogEntry> history,
        bool isTargetRdNotIdentified,
        bool canMarkReadyForMigration,
        bool canTriggerReadyForMigration,
        IReadOnlyList<RecordHistory> userActions) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        MigrationDate = r.MigrationDate,
        RdCode = r.RdCode,
        RdName = r.RdName,
        EntryNumbersCsv = r.EntryNumbersCsv,
        Title = r.Title,
        TitleType = r.TitleType?.ToString(),
        Plan = r.Plan,
        Block = r.Block,
        Lot = r.Lot,
        TitleSequence = r.TitleSequence,
        MigrationStatus = ConsolidatedMigrationStatus(r),
        SdStatus = r.SdStatus?.ToString() ?? string.Empty,
        MigratedTo = r.MigratedToRdName ?? string.Empty,
        MigrationType = r.MigrationType?.ToString(),
        ProcessedBy = r.ProcessedByUsername,
        RangeEntryNumber = NormalizeEntryNumberRange(r.RangeEntryNumber),
        ReasonForFailing = r.LastFailureReason,
        TargetSystemValidation = r.TargetSystemValidation,
        TargetSystemActivationStatus = r.TargetSystemActivationStatus,
        AttemptCount = r.AttemptCount,
        ProcessingStartedAt = r.ProcessingStartedAt,
        ProcessingCompletedAt = r.ProcessingCompletedAt,
        LastAttemptAt = r.LastAttemptAt,
        ApiResponseJson = r.ApiResponseJson,
        ProcessingStatus = r.ProcessingStatus?.ToString(),
        IsTargetRdNotIdentified = isTargetRdNotIdentified,
        CanMarkReadyForMigration = canMarkReadyForMigration,
        CanTriggerReadyForMigration = canTriggerReadyForMigration,
        // Document Type ascending, then Image File Name ascending - the Supporting Document
        // Images table and the viewer's Previous/Next navigation share this exact same order
        // (the JS simply walks the array this DTO list is rendered from), so they can never
        // disagree. Nulls (unresolved Document Type) sort first. r.Documents always has one row
        // per supporting document from record creation onward (Status = Pending until the SD
        // Auto Conversion API responds) - no JSON-blob fallback needed anymore.
        Documents = r.Documents
            .OrderBy(d => d.CodeLookup?.Name)
            .ThenBy(d => d.FileName)
            .Select(d => new MigrationDocumentDto
            {
                Id = d.Id,
                DocumentName = d.DocumentName,
                FileName = d.FileName,
                Status = d.Status.ToString(),
                ExistingFileName = d.ExistingFileName,
                PerformedBy = d.PerformedByUsername,
                ActionDate = d.ActionDate,
                DestinationPath = d.DestinationPath,
                SizeBytes = d.SizeBytes,
                DocumentType = d.CodeLookup?.Name,
                SourceImagePath = d.SourceImagePath,
                // Document-level value (MigrationDocuments.TargetSystemValidation) - independent
                // of the parent record's own value at r.TargetSystemValidation, used for the
                // Entry/Title level displays instead (see those two mappings).
                TargetSystemValidation = d.TargetSystemValidation,
                ReasonForFailing = d.ReasonForFailing,
                // Unresolved (CodeLookupId null) means this document's type is still "Others" -
                // see MigrationService.ApplyRealDocumentCorrections.
                CanChangeDocumentType = d.CodeLookupId is null,
            })
            .ToArray(),
        TitleRecords = titleRecords.Select(t => new MigrationTitleRecordDto
        {
            Id = t.Id,
            Title = t.Title,
            TitleSequence = t.TitleSequence,
            MigrationType = t.MigrationType?.ToString(),
            MigrationStatus = ConsolidatedMigrationStatus(t),
            // Associated Title-level value ("Already Associated"/"Associated") - distinct from
            // t.TargetSystemValidation, which is this same row's own Entry Record-level value
            // ("Existing"/"Not Existing"). Every sibling here (including the currently-opened
            // record itself, which is always included in titleRecords) has both values on its own
            // row - see MigrationRecord.TitleTargetSystemValidation.
            TargetSystemValidation = t.TitleTargetSystemValidation,
            ReasonForFailing = t.LastFailureReason,
        }).ToArray(),
        ActionHistory = history.Select(h => new MigrationActionHistoryDto
        {
            ActionDate = h.Log_Date,
            Activity = DisplayLogActivity(h.Log_Activity),
            MigrationType = h.MigrationType,
            Status = !string.IsNullOrEmpty(h.ProcessingStatus)
                ? h.ProcessingStatus
                : h.MigrationStatus ?? string.Empty,
            PerformedBy = h.ProcessedByUsername,
            Remarks = h.LastFailureReason,
        })
        .Concat(userActions.Select(h => new MigrationActionHistoryDto
        {
            ActionDate = h.CreatedAt,
            Activity = h.Action,
            MigrationType = null,
            Status = string.Empty,
            PerformedBy = h.ByUsername,
            Remarks = h.Remarks,
        }))
        .OrderByDescending(h => h.ActionDate)
        .ToArray(),
    };

    private static string DisplayLogActivity(string activity) => activity switch
    {
        "I" => "Created",
        "U" => "Updated",
        "D" => "Deleted",
        _ => activity,
    };

    // Entry Record-level Target System Validation (spec: "Display Target System Validation Based
    // on Business Rules and Migration API Response", section 1). entryStatus == "Existing entry"
    // -> "Existing" is CONFIRMED (client-provided real API response, 2026-11-20) and hardcoded.
    // What entryStatus looks like for a brand-new entry has no confirmed sample yet - the value
    // shown for that case (TargetSystemValidation:EntryNewValue, default "Not Existing") is
    // configurable in appsettings.json rather than hardcoded, so the client's eventual confirmed
    // answer can be applied without a code change. Applied whenever entryStatus isn't the
    // confirmed "Existing entry" value (absent, blank, or anything else) - this is only called
    // after a successful API response that otherwise proves the migration ran.
    private string MapEntryTargetSystemValidation(string? entryStatus) =>
        string.Equals(entryStatus?.Trim(), "Existing entry", StringComparison.OrdinalIgnoreCase)
            ? "Existing"
            : _configuration["TargetSystemValidation:EntryNewValue"] ?? "Not Existing";

    // Associated Title-level Target System Validation (spec section 2). Client-confirmed rule,
    // 2026-11-28 ("existing means already associated, not existing means associated") - no
    // Entry-No cross-check (an earlier version only assigned a value when the Entry No embedded in
    // titleStatus, e.g. "Existing title Entry No: 2026000001", literally matched the Range Entry
    // Number we submitted, which left this blank for genuinely-associated titles whenever
    // PHILARIS-RD's embedded reference wasn't a literal match, as seen on a real record):
    //  - null/blank titleStatus -> null (nothing to assign confidently either way).
    //  - titleStatus starts with "Existing title" -> the configured ExistingTitleValue (default
    //    "Already Associated") - acceptance criteria item a.
    //  - any other non-blank value -> the configured TitleNewValue (default "Associated") -
    //    acceptance criteria item b (first-time association).
    private string? MapTitleTargetSystemValidation(string? titleStatus)
    {
        if (string.IsNullOrWhiteSpace(titleStatus))
        {
            return null;
        }

        return titleStatus.TrimStart().StartsWith("Existing title", StringComparison.OrdinalIgnoreCase)
            ? _configuration["TargetSystemValidation:ExistingTitleValue"] ?? "Already Associated"
            : _configuration["TargetSystemValidation:TitleNewValue"] ?? "Associated";
    }

    // FileName is saved as whatever the SD Auto Conversion API itself returned for this image, not
    // the name we submitted - mirrors SystemMigrationService.UpsertMigrationDocument exactly (same
    // rationale: the API is free to alter the filename it echoes back). Matching an existing row
    // across retries is done by POSITION (see the caller), not by this value.
    // FileName is saved as whatever the SD Auto Conversion API itself returned for this image -
    // not the name we submitted, even though the API is free to alter it. Falls back to our own
    // submitted name only if the response omits fileName entirely. "document" is always a
    // pre-existing, already-tracked MigrationDocuments row (created when this record was marked
    // Ready for Migration - see ManualValidationService.MarkReadyForMigrationAsync) - this only
    // ever UPDATES it in place, never inserts (client requirement, 2026-11-12).
    private static void UpsertMigrationDocument(MigrationDocument document, SdAutoConversionImageDto image)
    {
        document.FileName = !string.IsNullOrWhiteSpace(image.FileName) ? image.FileName : Path.GetFileName(document.SourceImagePath) ?? document.FileName;
        document.Status = image.Status == "Duplicate SD" ? MigrationDocumentStatus.DuplicateSd : MigrationDocumentStatus.Migrated;
        document.DestinationPath = image.DestinationPath;
        document.SizeBytes = image.SizeBytes;
        document.Sequence = image.Sequence;
        document.ReasonForFailing = null;
        // Supporting Document-level Target System Validation (spec section 3): Duplicate SD ->
        // Existing (already in PHILARIS-RD), Inserted -> Not Existing (migrated for the first
        // time). image.Status is only ever "Duplicate SD" or "Inserted" by this point - the caller
        // only reaches here for a recognized status (see the missing/unrecognized branch above,
        // which routes to UpsertFailedMigrationDocument instead) - but the fallback null still
        // guards against an unrecognized third value appearing in a future API response.
        document.TargetSystemValidation = image.Status switch
        {
            "Duplicate SD" => "Existing",
            "Inserted" => "Not Existing",
            _ => null,
        };
    }

    // A document the API never recognized - Status = Failed, ReasonForFailing set - mirrors
    // SystemMigrationService.UpsertFailedMigrationDocument.
    private static void UpsertFailedMigrationDocument(MigrationDocument document, string reason)
    {
        document.Status = MigrationDocumentStatus.Failed;
        document.ReasonForFailing = Truncate(reason, 500);
        document.DestinationPath = null;
        document.SizeBytes = null;
        document.Sequence = null;
        // No recognized image status to map from - leave unassigned rather than guessing.
        document.TargetSystemValidation = null;
    }

    // Still classified "Others" (CodeLookupId null) - never submitted to the API at all (see
    // InitiateMigrationAsync), so there is no response image to update from.
    private static void UpsertNotApplicableDocument(MigrationDocument document)
    {
        document.Status = MigrationDocumentStatus.NotApplicable;
        document.ReasonForFailing = null;
        document.DestinationPath = null;
        document.SizeBytes = null;
        document.Sequence = null;
        // Never submitted to the API at all - no validation to report.
        document.TargetSystemValidation = null;
    }

    // Preserves the original supporting-document JSON under a "documents" property and adds the
    // API response under a separate "apiResponse" property (task section 5.3/9) - never discards
    // or restructures the original content. If Document_Json was already merged by a previous
    // attempt, only its "documents" portion is kept (the stale apiResponse is replaced, not
    // nested again).
    private static string MergeDocumentJsonWithApiResponse(string? existingDocumentJson, SdAutoConversionResponseDto response)
    {
        var originalDocumentsJson = ExtractOriginalDocumentsJson(existingDocumentJson);
        var documentsNode = string.IsNullOrWhiteSpace(originalDocumentsJson) ? null : JsonNode.Parse(originalDocumentsJson);

        var merged = new JsonObject
        {
            ["documents"] = documentsNode,
            ["apiResponse"] = JsonSerializer.SerializeToNode(response, JsonOptions),
        };

        return merged.ToJsonString(JsonOptions);
    }

    // Document_Json starts out as a plain array (the verbatim copy of ManualValidationRequests.
    // DocumentsJson made on "Ready for Migration"). The first successful/attempted API call wraps
    // it into { documents: [...], apiResponse: {...} } (see MergeDocumentJsonWithApiResponse) - any
    // later read of "the original documents array" (a retry's request-building, or a later merge)
    // must handle both shapes, never assuming which one is currently stored.
    private static string? ExtractOriginalDocumentsJson(string? documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            return documentJson;
        }

        var parsed = JsonNode.Parse(documentJson);
        return parsed is JsonObject obj && obj.TryGetPropertyValue("documents", out var docsProp)
            ? docsProp?.ToJsonString(JsonOptions)
            : documentJson;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
