using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

// Talks to the separate, externally-hosted RD/fetch service over HTTP (reachable only via
// Paradigm WiFi/VPN) - not this app's own database. This is the only place in DCT_SD that
// calls out to an external API.
public interface IRdFetchApiClient
{
    /// executedByUserId is the logged-in user who clicked Update, sent as "Executed_By_UserID"
    /// so the external service's own FetchRuns record attributes the change to them instead of
    /// its own service identity.
    Task<ExternalUpdateRootPathResponse> UpdateRootPathAsync(string path, string remarks, int executedByUserId, CancellationToken cancellationToken = default);

    /// Starts a fetch run and returns the raw streaming response (POST /fetch/start, an SSE
    /// stream) - the caller is responsible for reading and disposing it. rootPath is sent as the
    /// request's "path" field (the RD Configuration UI's current Root Source Path - never
    /// hardcoded here). executedByUserId is the logged-in user who clicked Start Fetching, sent
    /// as "Executed_By_UserID" so the external service's own FetchRuns record attributes the run
    /// to them instead of its own service identity. Always dry_run=false, apply_file_moves=true
    /// per the integration requirement.
    Task<HttpResponseMessage> StartFetchStreamAsync(string rootPath, int executedByUserId, CancellationToken cancellationToken = default);

    /// GET /fetch/{fetchRunId}. Returns null on a 404 (no such run on the external service) so
    /// callers can show a "Fetch run not found" message instead of an error.
    Task<FetchRunDetailDto?> GetFetchRunDetailsAsync(int fetchRunId, CancellationToken cancellationToken = default);

    /// The Failed Extraction page's Reprocess action (POST /failed-extractions/reprocess) -
    /// re-runs OCR extraction for exactly the one Entry Folder at folderPath (must match an
    /// existing failed record's FolderPath exactly - never hardcoded here). executedByUserId is
    /// the logged-in user who clicked Reprocess, sent as "Executed_By_UserID" per that endpoint's
    /// own OpenAPI spec. On success the external service creates real
    /// OcrExtractionRecords/ManualValidationRequests rows and clears the folder from its OWN
    /// internal failure tracking (confirmed via that service's own GET /failed-extractions), but -
    /// despite what its OpenAPI description claims - it does NOT go back and remove this app's
    /// already-written stale Failed OcrExtractionRecords row for the same folder, so the caller
    /// must do that itself (see FailedExtractionController.Reprocess) using the response's own
    /// records_created/manual_validation_created counts as the success signal, not the free-text
    /// "status" field and not merely the absence of an HTTP error.
    Task<ExternalFailedExtractionReprocessResponse> ReprocessFailedExtractionAsync(string folderPath, int? executedByUserId, CancellationToken cancellationToken = default);
}
