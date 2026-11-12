namespace DCT_SD.Models.Entities;

// Read-only mapping over MigrationRecords_Log (dct_sd_main.sql Section 23), populated entirely
// by the existing trg_MigrationRecords_I/U/D triggers - the app never inserts into this table,
// only queries it to show Action History on the Migration Details page.
public class MigrationRecordLogEntry
{
    public int Log_ID { get; set; }
    public string Log_UserID { get; set; } = string.Empty;
    public DateTime Log_Date { get; set; }
    public string Log_Activity { get; set; } = string.Empty;
    public int? Id { get; set; }
    public string? MigrationType { get; set; }
    public string? ProcessingStatus { get; set; }
    public string? MigrationStatus { get; set; }
    public string? ProcessedByUsername { get; set; }
    public string? LastFailureReason { get; set; }
}
