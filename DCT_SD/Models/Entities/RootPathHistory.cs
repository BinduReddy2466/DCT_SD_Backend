namespace DCT_SD.Models.Entities;

public class RootPathHistory
{
    public int Id { get; set; }
    public string? FromPath { get; set; }
    public string ToPath { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public int ModifiedByUserId { get; set; }
    public string ModifiedByUsername { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
}
