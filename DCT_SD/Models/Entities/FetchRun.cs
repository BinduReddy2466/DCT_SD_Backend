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

    // Both written directly by the external RD Fetch API (this app never writes RecordKind=
    // FetchRun rows at all - see StartFetchAsync) - confirmed against live data: SummaryMessage
    // is the exact "Successfully Extracted: X, folders\nFailed: Y, ...\nMoved to Empty Entry
    // Folders: Z, ...\nTotal Folders Processed: N" breakdown for every completed run, whether it
    // came from Start Fetching or a Failed Extraction Reprocess. RootPath is the actual root
    // folder that run executed against, distinct from SourcePath (which can be a specific Entry
    // Folder for a narrower Reprocess run) - see MapToFetchRunItem for how these are surfaced as
    // Fetch History's Failure Reason/Source Path.
    public string? SummaryMessage { get; set; }
    public string? RootPath { get; set; }

    public ICollection<OcrExtractionRecord> OcrExtractionRecords { get; set; } = new List<OcrExtractionRecord>();
}
