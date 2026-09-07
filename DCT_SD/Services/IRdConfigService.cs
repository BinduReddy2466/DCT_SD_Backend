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

    /// Updates the local FetchRuns mirror row created by StartFetchAsync once the external run
    /// finishes, so the existing Fetch History table reflects the real outcome without callers
    /// having to re-query the external service just to render the list/pagination.
    Task CompleteFetchRunAsync(int localFetchRunId, FetchRunDetailDto details, CancellationToken cancellationToken = default);

    /// Marks a local mirror row Failed when the external call never yielded enough information
    /// to reconcile it properly (couldn't reach the service, connection dropped mid-stream,
    /// etc.) - without this, a row stuck Ongoing would permanently block every future fetch
    /// attempt via StartFetchAsync's own "already in progress" guard.
    Task FailFetchRunAsync(int localFetchRunId, CancellationToken cancellationToken = default);

    /// Looks up one local FetchRuns mirror row (for the Fetch History "View" action) along with
    /// whatever external fetch_run_id was stashed on it by CompleteFetchRunAsync, if any.
    Task<(FetchRunItemDto Item, int? ExternalFetchRunId)?> GetFetchRunAsync(int localFetchRunId, CancellationToken cancellationToken = default);

    /// Lists subdirectories of a server-side path for the "Browse Folder" picker. A null/empty
    /// path returns the machine's fixed drives as the top level (there is no server path a
    /// browser can hand back on its own - see the Browse Folder modal for why this has to be
    /// server-driven rather than the client-side File System Access API).
    Task<DirectoryBrowseDto> BrowseDirectoriesAsync(string? path, CancellationToken cancellationToken = default);
}
