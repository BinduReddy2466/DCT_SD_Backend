namespace DCT_SD.Models.Dtos.RdConfig;

// The fields the Fetch Run Details view needs, regardless of whether they came live from the
// external fetch service (GET /fetch/{id}) or from this app's own local FetchRuns mirror.
public class FetchRunDetailDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int? TotalCount { get; set; }
    public string? RunTime { get; set; }
    public string ExecutedBy { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string? LastProcessedFolderPath { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
