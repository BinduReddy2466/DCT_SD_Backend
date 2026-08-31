namespace DCT_SD.Models.Dtos.FailedExtraction;

public class FailedExtractionListItemDto
{
    public int Id { get; set; }
    public DateTime ExtractionDateTime { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
}
