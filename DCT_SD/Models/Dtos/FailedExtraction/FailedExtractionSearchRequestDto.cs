using DCT_SD.Services;

namespace DCT_SD.Models.Dtos.FailedExtraction;

public class FailedExtractionSearchRequestDto : IPageableRequest
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    // Matched against RD name (the "dropdown with text search" filter).
    public string? Rd { get; set; }
    public string? FolderName { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
