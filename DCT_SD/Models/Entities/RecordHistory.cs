namespace DCT_SD.Models.Entities;

// Generic history/remarks trail (replaces the old ManualValidationRemarks table), shared
// across modules via TableName + RecordId rather than a dedicated FK per parent table.
public class RecordHistory
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int? RecordId { get; set; }
    public string? RefNo { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public int? ByUserId { get; set; }
    public string? ByUsername { get; set; }
    public string? AppName { get; set; }
    public string? HostName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
}
