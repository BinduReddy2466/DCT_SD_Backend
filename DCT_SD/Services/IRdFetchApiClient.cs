using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

// Talks to the separate, externally-hosted RD/fetch service over HTTP (reachable only via
// Paradigm WiFi/VPN) - not this app's own database. This is the only place in DCT_SD that
// calls out to an external API.
public interface IRdFetchApiClient
{
    Task<ExternalUpdateRootPathResponse> UpdateRootPathAsync(string path, string remarks, CancellationToken cancellationToken = default);

    /// Starts a fetch run and returns the raw streaming response (POST /fetch/start, an SSE
    /// stream) - the caller is responsible for reading and disposing it. Always dry_run=false,
    /// apply_file_moves=false per the integration requirement.
    Task<HttpResponseMessage> StartFetchStreamAsync(CancellationToken cancellationToken = default);

    /// GET /fetch/{fetchRunId}. Returns null on a 404 (no such run on the external service) so
    /// callers can show a "Fetch run not found" message instead of an error.
    Task<FetchRunDetailDto?> GetFetchRunDetailsAsync(int fetchRunId, CancellationToken cancellationToken = default);
}
