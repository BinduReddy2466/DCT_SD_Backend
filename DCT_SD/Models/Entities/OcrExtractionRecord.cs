using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class OcrExtractionRecord
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public int? FetchRunId { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string? EntryNumbersCsv { get; set; }
    public string? TitleNumber { get; set; }
    public TitleType? TitleType { get; set; }
    public int DocumentCount { get; set; }
    public OcrExtractionStatus ExtractionStatus { get; set; }
    public DateTime ExtractionDateTime { get; set; }

    public FetchRun? FetchRun { get; set; }
    public ICollection<ManualValidationRequest> ManualValidationRequests { get; set; } = new List<ManualValidationRequest>();
    public ICollection<MigrationRecord> MigrationRecords { get; set; } = new List<MigrationRecord>();
}
