using DCT_SD.Models;
using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

public interface IRdConfigService
{
    Task<RootPathDto> GetCurrentRootPathAsync(CancellationToken cancellationToken = default);
    Task<RootPathDto> UpdateRootPathAsync(UpdateRootPathRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResult<RootPathHistoryItemDto>> SearchRootPathHistoryAsync(RootPathHistorySearchRequestDto request, CancellationToken cancellationToken = default);
    Task<FetchRunItemDto> StartFetchAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<FetchRunItemDto>> SearchFetchHistoryAsync(FetchHistorySearchRequestDto request, CancellationToken cancellationToken = default);

    /// Lists subdirectories of a server-side path for the "Browse Folder" picker. A null/empty
    /// path returns the machine's fixed drives as the top level (there is no server path a
    /// browser can hand back on its own - see the Browse Folder modal for why this has to be
    /// server-driven rather than the client-side File System Access API).
    Task<DirectoryBrowseDto> BrowseDirectoriesAsync(string? path, CancellationToken cancellationToken = default);
}
