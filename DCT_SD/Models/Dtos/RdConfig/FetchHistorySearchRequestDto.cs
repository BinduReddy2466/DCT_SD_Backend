using DCT_SD.Services;

namespace DCT_SD.Models.Dtos.RdConfig;

public class FetchHistorySearchRequestDto : IPageableRequest
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? ExecutedBy { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
