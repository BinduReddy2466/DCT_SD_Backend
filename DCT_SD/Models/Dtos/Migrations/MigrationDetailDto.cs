namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationDetailDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime MigrationDate { get; set; }
    public string RdCode { get; set; } = string.Empty;
    public string RdName { get; set; } = string.Empty;
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
    public string MigrationStatus { get; set; } = string.Empty;
    public string SdStatus { get; set; } = string.Empty;
    public string MigratedTo { get; set; } = string.Empty;

    public string? MigrationType { get; set; }
    public string? ProcessedBy { get; set; }
    public string? RangeEntryNumber { get; set; }
    public string? ReasonForFailing { get; set; }
    public string? TargetSystemValidation { get; set; }
    public string? TargetSystemActivationStatus { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? ApiResponseJson { get; set; }

    // Raw MigrationProcessingStatus enum name (e.g. "Pending", "RetryPending", "Completed") or
    // null if this record has no processing-attempt concept yet (a Manual Validation row never
    // initiated). Used by the Details page to decide whether "Initiate Migration" can be shown -
    // distinct from MigrationStatus above, which is already display text for the UI to render.
    public string? ProcessingStatus { get; set; }

    // True when RdCode doesn't resolve to any active RegistryOffice CodeLookup - drives whether
    // RD Code/RD Name show as an editable searchable dropdown (spec section 3).
    public bool IsTargetRdNotIdentified { get; set; }

    // True only for a System Migration-origin record whose RD Code hasn't resolved yet - see
    // MigrationService.MarkReadyForMigrationAsync.
    public bool CanMarkReadyForMigration { get; set; }

    // Present only when this Entry Record originated from a Manual Validation record (spec
    // section 11) - null for System Migration-origin records with no Manual Validation history.
    public ManualValidationOriginDto? ManualValidationOrigin { get; set; }

    public IReadOnlyList<MigrationDocumentDto> Documents { get; set; } = Array.Empty<MigrationDocumentDto>();
    public IReadOnlyList<MigrationTitleRecordDto> TitleRecords { get; set; } = Array.Empty<MigrationTitleRecordDto>();
    public IReadOnlyList<MigrationActionHistoryDto> ActionHistory { get; set; } = Array.Empty<MigrationActionHistoryDto>();
}
