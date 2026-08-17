namespace DCT_SD.Models.Dtos.ManualValidation;

public class ManualValidationDetailDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ManualValidationDocumentDto> Documents { get; set; } = Array.Empty<ManualValidationDocumentDto>();
}
