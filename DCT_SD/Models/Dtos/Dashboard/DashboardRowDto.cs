namespace DCT_SD.Models.Dtos.Dashboard;

// One statistical row for one RD + Record Type ("Folder" or "Images") on the Dashboard page.
// A null value renders as "N/A" (the statistic doesn't apply to this Record Type).
//
// Activated is always 0 (Folder row) / null (Images row, N/A): MigrationRecord
// .TargetSystemActivationStatus exists on the entity but is never written anywhere in this
// codebase (same external-API limitation as MigrationRecord.TargetSystemValidation elsewhere in
// this app) - there is no real "activated in PHILARIS-RD" signal to count yet, so this stays 0
// by the same explicit product decision already covering WIP/FailedMigration/ConfirmedDuplicate
// below, rather than inventing a status or a schema change.
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
    public int? Activated { get; set; }
    public int Total { get; set; }
}
