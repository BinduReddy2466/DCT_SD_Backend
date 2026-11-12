namespace DCT_SD.Models.Enums;

public enum SupportingDocumentStatus
{
    AllMigrated = 1,
    PartiallyDuplicate = 2,
    AllDuplicate = 3,

    // At least one document is still classified "Others" (MigrationDocumentStatus.NotApplicable)
    // but everything else resolved - see MigrationService.RecalculateSupportingDocumentStatusAsync.
    MigratedWithNA = 4,

    // At least one document's migration failed outright - regardless of how the rest resolved.
    Failed = 5,
}
