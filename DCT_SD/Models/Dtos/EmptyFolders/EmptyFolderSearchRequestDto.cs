using DCT_SD.Services;

namespace DCT_SD.Models.Dtos.EmptyFolders;

public class EmptyFolderSearchRequestDto : IPageableRequest
{
    public string? RdCode { get; set; }
    public string? FolderName { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
