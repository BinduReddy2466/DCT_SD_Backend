namespace DCT_SD.Helpers;

// Ports migrationStatusDisplay.ts / manualValidationDisplay.ts verbatim - the backend
// stores/returns enum names (e.g. "MigratedToExisting"), the UI shows/filters by the
// human-readable display string (e.g. "Migrated to Existing Title/Entry Record").
public static class StatusDisplay
{
    private static readonly Dictionary<string, string> MigrationStatusMap = new()
    {
        ["MigratedToExisting"] = "Migrated to Existing Title/Entry Record",
        ["MigratedAsNew"] = "Migrated as New Record",
        ["ReadyForMigration"] = "Ready for Migration",
    };

    private static readonly Dictionary<string, string> SdStatusMap = new()
    {
        ["AllMigrated"] = "All Supporting Documents Migrated",
        ["PartiallyDuplicate"] = "Supporting Documents Migrated with Duplicate SD",
        ["AllDuplicate"] = "All Supporting Documents are Duplicate SD",
        ["MigratedWithNA"] = "Supporting Documents Migrated with N/A",
        ["Failed"] = "Failed",
    };

    private static readonly Dictionary<string, string> ManualValidationStatusMap = new()
    {
        ["IncompleteExtraction"] = "Incomplete Extraction",
        ["TargetRdNotIdentified"] = "Target RD Not Identified",
        ["ReadyForMigration"] = "Ready for Migration",
    };

    private static readonly Dictionary<string, string> MigrationDocStatusMap = new()
    {
        ["Migrated"] = "Migrated",
        ["DuplicateSd"] = "Duplicate SD",
        ["Overwritten"] = "Overwritten",
        ["InsertedAsNew"] = "Inserted",
        ["Failed"] = "Failed Migration",
        ["ConfirmedDuplicate"] = "Confirmed Duplicate",
        ["NotApplicable"] = "N/A",
        ["Pending"] = "Pending",
    };

    private static readonly Dictionary<string, string> MigrationTypeMap = new()
    {
        ["SystemMigration"] = "System Migration",
        ["ManualValidation"] = "Manual Validation",
    };

    private static readonly Dictionary<string, string> MigrationProcessingStatusMap = new()
    {
        ["Pending"] = "Pending",
        ["Processing"] = "Processing",
        ["Completed"] = "Completed",
        ["Failed"] = "Failed",
        ["RetryPending"] = "Retry Pending",
    };

    private static readonly Dictionary<string, string> MissingFieldLabels = new()
    {
        ["rdCode"] = "RD Code",
        ["rdName"] = "RD Name",
        ["entry"] = "Entry Number",
        ["title"] = "Title Number",
        ["titleType"] = "Title Type",
        ["plan"] = "Plan Number",
        ["block"] = "Block Number",
        ["lot"] = "Lot Number",
        ["titleSeq"] = "Title Sequence",
        ["titleSequence"] = "Title Sequence",
    };

    public static string MigrationStatusToDisplay(string status) => MigrationStatusMap.GetValueOrDefault(status, status);
    public static string? MigrationStatusToApi(string? display) => MigrationStatusMap.FirstOrDefault(kv => kv.Value == display).Key;
    public static IReadOnlyList<string> MigrationStatusOptions => MigrationStatusMap.Values.ToArray();

    public static string SdStatusToDisplay(string status) => SdStatusMap.GetValueOrDefault(status, status);
    public static string? SdStatusToApi(string? display) => SdStatusMap.FirstOrDefault(kv => kv.Value == display).Key;
    public static IReadOnlyList<string> SdStatusOptions => SdStatusMap.Values.ToArray();

    public static string ManualValidationStatusToDisplay(string status) => ManualValidationStatusMap.GetValueOrDefault(status, status);
    public static string? ManualValidationStatusToApi(string? display) => ManualValidationStatusMap.FirstOrDefault(kv => kv.Value == display).Key;
    public static IReadOnlyList<string> ManualValidationStatusOptions => ManualValidationStatusMap.Values.ToArray();

    public static string MigrationDocStatusToDisplay(string status) => MigrationDocStatusMap.GetValueOrDefault(status, status);

    public static string MigrationTypeToDisplay(string status) => MigrationTypeMap.GetValueOrDefault(status, status);
    public static string? MigrationTypeToApi(string? display) => MigrationTypeMap.FirstOrDefault(kv => kv.Value == display).Key;

    public static string MigrationProcessingStatusToDisplay(string status) => MigrationProcessingStatusMap.GetValueOrDefault(status, status);
    public static string? MigrationProcessingStatusToApi(string? display) => MigrationProcessingStatusMap.FirstOrDefault(kv => kv.Value == display).Key;

    public static string DescribeMissingFields(IEnumerable<string> missingFields) =>
        string.Join(", ", missingFields.Select(k => MissingFieldLabels.GetValueOrDefault(k, k)));
}
