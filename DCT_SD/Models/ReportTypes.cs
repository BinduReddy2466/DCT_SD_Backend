namespace DCT_SD.Models;

// Central registry of the 6 report types the Reports page can generate. Each key reuses the
// filter criteria and query logic of its corresponding existing module rather than inventing
// new search behavior - see ReportService for the mapping.
public static class ReportTypes
{
    public const string RootSourcePathHistory = "root-source-path-history";
    public const string FetchHistory = "fetch-history";
    public const string MigrationMonitoring = "migration-monitoring";
    public const string ManualValidation = "manual-validation";
    public const string EmptyEntryFolders = "empty-entry-folders";
    public const string FailedExtraction = "failed-extraction";

    // Display label doubles as the report file name prefix (see the "<Report Type>_<timestamp>"
    // naming rule), so keep these exactly as they should appear to the user.
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [RootSourcePathHistory] = "Root Source Path Update History",
        [FetchHistory] = "Fetch History",
        [MigrationMonitoring] = "Migration Monitoring",
        [ManualValidation] = "Manual Validation",
        [EmptyEntryFolders] = "Empty Entry Folders",
        [FailedExtraction] = "Failed Extraction",
    };

    public static readonly IReadOnlyList<string> All = Labels.Keys.ToArray();
}
