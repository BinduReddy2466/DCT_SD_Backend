using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class MigrationRecord
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int? OcrExtractionRecordId { get; set; }
    public DateTime MigrationDate { get; set; }

    public string RdCode { get; set; } = string.Empty;
    public string RdName { get; set; } = string.Empty;

    // Despite the name (kept as-is to avoid touching every other file that already references it -
    // views, reports, search DTOs), this holds exactly ONE entry number per row for any row created
    // after 2026-11-12 ("Migration Tracking should be per entry number", client-confirmed) - see
    // Helpers/EntryNumberCsvParser.cs, which splits a source's comma-separated entry number list
    // into one MigrationRecords row per entry number. Rows created before that change may still
    // hold the full original comma-separated list (not retroactively split - see
    // ManualValidationService.MarkReadyForMigrationAsync, the only thing that creates these rows
    // now).
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public TitleType? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }

    // Nullable (was non-nullable): the System Migration batch creates this row the first time it
    // attempts an eligible OcrExtractionRecords row (see ProcessingStatus below), before there is
    // any real migration outcome to report - these three only get a real value once
    // ProcessingStatus reaches Completed. Pre-existing (Manual Validation / legacy) rows always
    // have real values here, same as before.
    public MigrationStatus? MigrationStatus { get; set; }
    public SupportingDocumentStatus? SdStatus { get; set; }
    public string? MigratedToRdName { get; set; }

    // Identifies how this row was produced. Null for rows written before this column existed.
    public MigrationType? MigrationType { get; set; }
    public int? ProcessedByUserId { get; set; }
    public string? ProcessedByUsername { get; set; }

    // The System Migration batch's own claim/attempt/retry tracking - see
    // Models/Enums/MigrationProcessingStatus.cs. Null for Manual Validation / legacy rows, which
    // have no batch-retry concept and rely solely on MigrationStatus/SdStatus above.
    public MigrationProcessingStatus? ProcessingStatus { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? LastFailureReason { get; set; }
    public string? ApiResponseJson { get; set; }
    public DateTime? ApiResponseAt { get; set; }
    public string? TargetSystemValidation { get; set; }
    public string? TargetSystemActivationStatus { get; set; }

    // Associated Title-level Target System Validation ("Already Associated"/"Associated", mapped
    // from the SD Auto Conversion API's titleStatus) - distinct from TargetSystemValidation above,
    // which holds the Entry Record-level value ("Existing"/"Not Existing", mapped from
    // entryStatus). Both facts come from the SAME API response for this row's own submission
    // (every Title Record is already its own MigrationRecords row), so both are stored here rather
    // than one column serving two different business meanings depending on which section displays
    // it. See MigrationService.MapTitleTargetSystemValidation.
    public string? TitleTargetSystemValidation { get; set; }

    // Holds this Entry Record's supporting-document information end to end: the original
    // ManualValidationRequests.DocumentsJson copied verbatim when marked "Ready for Migration",
    // then merged with the SD Auto Conversion API's own response under a separate "apiResponse"
    // property once that API has been called from the Migration Monitoring Details page (see
    // MigrationService.InitiateMigrationAsync). Distinct from ApiResponseJson above, which is the
    // System Migration batch's own raw audit passthrough.
    public string? DocumentJson { get; set; }

    // Copied directly from the originating row's own EntryNumberRange the moment this row is
    // created (ManualValidationRequests.EntryNumberRange for a Manual Validation-origin row,
    // OcrExtractionRecords.EntryNumberRange for a System Migration-origin row) - not looked up
    // indirectly via OcrExtractionRecordId at display time, since a Manual Validation user's own
    // correction to EntryNumberRange (Save) only ever updates ManualValidationRequests, never the
    // OcrExtractionRecords row it came from.
    public string? RangeEntryNumber { get; set; }

    public OcrExtractionRecord? OcrExtractionRecord { get; set; }
    public ICollection<MigrationDocument> Documents { get; set; } = new List<MigrationDocument>();
}
