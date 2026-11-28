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

    // View is disabled whenever the consolidated Migration Status reads "Ready for Migration" or
    // "Migrating" (client requirement, 2026-11-27) - regardless of which underlying path produced
    // that status (the legacy ProcessingStatus = Pending case, or a freshly-generated
    // Entry-Number-Range x Title-Sequence combination with ProcessingStatus still null). There is
    // no "Initiate Migration" button anywhere on this page for a human to act on from here, so
    // there's nothing a View would show that isn't already visible from the list itself.
    public bool CanView { get; set; }

    // Reprocess is only ever valid for ProcessingStatus = Failed (see
    // MigrationService.ReprocessAsync's own claim condition) - NOT RetryPending, even though both
    // consolidate to the same "Failed Migration" MigrationStatus display text above. Computed
    // separately so the list row's Reprocess button is offered only when it would actually
    // succeed, mirroring Details.cshtml's own canReprocess.
    public bool CanReprocess { get; set; }
}
