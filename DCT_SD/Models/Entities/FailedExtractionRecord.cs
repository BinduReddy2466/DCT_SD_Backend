namespace DCT_SD.Models.Entities;

// Maps the existing FailedExtractionRecords table - populated externally by the OCR/fetch
// pipeline, the same way OcrExtractionRecords and EmptyFolderRecords are - which was not
// previously modeled in this app. No schema change: this only adds an EF Core mapping onto a
// table that already exists.
public class FailedExtractionRecord
{
    public int Id { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public DateTime ExtractionDateTime { get; set; }
}
