namespace DCT_SD.Models.Dtos.RdConfig;

public class FetchRunItemDto
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RunTime { get; set; }
    public int ProcessedCount { get; set; }
    public int? TotalCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ExecutedBy { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
}
