namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationListItemDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime MigrationDate { get; set; }
    public string RdCode { get; set; } = string.Empty;
    public string RdName { get; set; } = string.Empty;
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }

    // One of 5 consolidated values (Ready for Migration/N/A/Migrated/Failed Migration/Migrating) -
    // see MigrationService.ConsolidatedMigrationStatus.
    public string MigrationStatus { get; set; } = string.Empty;
    public string SdStatus { get; set; } = string.Empty;
    public string MigratedTo { get; set; } = string.Empty;

    public string? MigrationType { get; set; }
    public string? ProcessedBy { get; set; }
    public string? RangeEntryNumber { get; set; }
    public string? ReasonForFailing { get; set; }
    public string? TargetSystemValidation { get; set; }
    public string? TargetSystemActivationStatus { get; set; }

    // View is disabled only when a record is genuinely queued/in-flight with nothing yet to show
    // (ProcessingStatus Pending or Processing) - NOT for a Manual Validation-origin record whose
    // consolidated status also happens to read "Ready for Migration" (ProcessingStatus still
    // null, MigrationStatus = ReadyForMigration): that one has no automatic queue at all and can
    // only ever be migrated by a human opening it and clicking "Initiate Migration" on the
    // Migration Details page, so View must stay enabled for it.
    public bool CanView { get; set; }
}
