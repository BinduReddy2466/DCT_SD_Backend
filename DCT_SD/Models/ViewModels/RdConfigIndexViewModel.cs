using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Models.ViewModels;

public class RdConfigIndexViewModel
{
    public string? CurrentPath { get; set; }
    public RootPathHistoryItemDto? LatestUpdate { get; set; }
    public PagedResult<FetchRunItemDto> FetchHistory { get; set; } = new();
    public PagedResult<RootPathHistoryItemDto> RootHistory { get; set; } = new();
    public RootPathFormViewModel RootPathForm { get; set; } = new();
}
