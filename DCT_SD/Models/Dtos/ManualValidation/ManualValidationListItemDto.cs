namespace DCT_SD.Models.Dtos.ManualValidation;

public class ManualValidationListItemDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
    public DateTime ExtractionDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
