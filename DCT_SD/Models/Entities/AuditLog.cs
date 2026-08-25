namespace DCT_SD.Models.Entities;

// Forensic trail populated entirely by database triggers - the app never writes to this
// table. Not a data source for any of the 22 screens; mapped here only so the existing
// table is represented in the model, per DCT_SD being the source of truth for schema.
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int? RecordId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string PerformedByLogin { get; set; } = string.Empty;
    public int? PerformedByAppUserId { get; set; }
    public string? AppName { get; set; }
    public string? HostName { get; set; }
    public DateTime LogDate { get; set; }
}
