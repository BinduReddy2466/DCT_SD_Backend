using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class FetchRun
{
    public int Id { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public FetchRunStatus Status { get; set; } = FetchRunStatus.Ongoing;
    public int? TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public string? LastProcessedFolderPath { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public int ExecutedByUserId { get; set; }
    public string ExecutedByUsername { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<OcrExtractionRecord> OcrExtractionRecords { get; set; } = new List<OcrExtractionRecord>();
}
