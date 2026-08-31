using DCT_SD.Services;

namespace DCT_SD.Models.Dtos.RdConfig;

public class RootPathHistorySearchRequestDto : IPageableRequest
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? ModifiedBy { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
