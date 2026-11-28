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

    // Corrected 2026-11-27: holds the ORIGINAL, preserved Entry Number CSV copied verbatim from the
    // source row (e.g. "100,200,300") - identical across every combination row generated from the
    // same sibling, display-only ("Entry Number" column/search), and NEVER sent to the external SD
    // Auto Conversion API. The per-combination split value lives on RangeEntryNumber below instead
    // (see its own comment) - a 2026-11-12/-27 bug had this backwards, splitting THIS field and
    // leaving RangeEntryNumber unsplit, which silently broke multi-value Entry Number Range
    // combinations.
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

    // Corrected 2026-11-27: holds the per-COMBINATION split value - one item from the originating
    // sibling's own EntryNumberRange (via Helpers/EntryNumberCsvParser.Split), e.g. "1"/"8"/"9" for
    // Entry Number Range "1,8,9" - unique per combination together with RequestNumber and
    // TitleSequence (see UK_MigrationRecords_01), sent to the external SD Auto Conversion API
    // (never EntryNumbersCsv above), and used to group Associated Title Records for the same Entry
    // Number Range. Not looked up indirectly via OcrExtractionRecordId at display time, since a
    // Manual Validation user's own correction to EntryNumberRange (Save) only ever updates
    // ManualValidationRequests, never the OcrExtractionRecords row it came from.
    public string? RangeEntryNumber { get; set; }

    public OcrExtractionRecord? OcrExtractionRecord { get; set; }
    public ICollection<MigrationDocument> Documents { get; set; } = new List<MigrationDocument>();
}
