using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Models.ViewModels;

public class RdConfigIndexViewModel
{
    public string? CurrentPath { get; set; }
    public RootPathHistoryItemDto? LatestUpdate { get; set; }
    public PagedResult<FetchRunItemDto> FetchHistory { get; set; } = new();
    public PagedResult<RootPathHistoryItemDto> RootHistory { get; set; } = new();
    public RootPathFormViewModel RootPathForm { get; set; } = new();

    // "FirstName_LastName" for the logged-in user - used only to seed the live/streaming Fetch
    // History row's Executed By cell (see rd-config.js) while a run is in progress, before the
    // real persisted row (with its own, separately-sourced Executed By) exists.
    public string CurrentUserDisplayName { get; set; } = string.Empty;
}
