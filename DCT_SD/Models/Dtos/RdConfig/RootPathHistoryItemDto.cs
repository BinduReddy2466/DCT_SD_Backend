namespace DCT_SD.Models.Dtos.RdConfig;

public class RootPathHistoryItemDto
{
    public int Id { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string? FromPath { get; set; }
    public string ToPath { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}
