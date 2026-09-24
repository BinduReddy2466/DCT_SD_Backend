namespace DCT_SD.Models.Dtos.Dashboard;

// One statistical row for one RD + Record Type ("Folder" or "Images") on the Dashboard page.
// A null value renders as "N/A" (the statistic doesn't apply to this Record Type); WIP,
// FailedMigration and, on Images rows, ConfirmedDuplicate are 0 rather than null - see
// DashboardService for why (no existing data source for those, per explicit product decision
// to show 0 instead of inventing a status or schema change).
public class DashboardRowDto
{
    public string RdCode { get; set; } = string.Empty;
    public string RdName { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;

    public int Fetched { get; set; }
    public int Wip { get; set; }
    public int? ReadyForMigration { get; set; }
    public int? ManualValidation { get; set; }
    public int? IncompleteExtraction { get; set; }
    public int Migrated { get; set; }
    public int FailedMigration { get; set; }
    public int? EmptyFolders { get; set; }
    public int? DuplicateSd { get; set; }
    public int? ConfirmedDuplicate { get; set; }
    public int? Inserted { get; set; }
    public int? NotApplicableOthers { get; set; }
    public int Total { get; set; }
}
