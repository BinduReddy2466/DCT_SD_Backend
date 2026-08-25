using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

// Shares one table between two record kinds: RecordKind = "FetchRun" for a fetch execution
// (Status/TotalCount/ProcessedCount/StartedAt/CompletedAt apply) and RecordKind = "PathChange"
// for a root source path update (FromPath/SourcePath/Remarks apply, Status is null). This
// replaces the old separate RootPathHistories table.
public class FetchRun
{
    public int Id { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public FetchRunStatus? Status { get; set; }
    public int? TotalCount { get; set; }
    public int? ProcessedCount { get; set; }
    public string? LastProcessedFolderPath { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public int ExecutedByUserId { get; set; }
    public string ExecutedByUsername { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string RecordKind { get; set; } = string.Empty;
    public string? FromPath { get; set; }
    public string? Remarks { get; set; }

    public ICollection<OcrExtractionRecord> OcrExtractionRecords { get; set; } = new List<OcrExtractionRecord>();
}
