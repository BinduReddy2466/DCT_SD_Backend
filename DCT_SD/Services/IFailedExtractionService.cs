using DCT_SD.Models;
using DCT_SD.Models.Dtos.FailedExtraction;

namespace DCT_SD.Services;

public interface IFailedExtractionService
{
    Task<PagedResult<FailedExtractionListItemDto>> SearchAsync(FailedExtractionSearchRequestDto request, CancellationToken cancellationToken = default);

    // True if any Failed Extraction record exists at all, ignoring the current filters - lets
    // the page tell "nothing has ever failed" apart from "this search matched nothing".
    Task<bool> AnyRecordsExistAsync(CancellationToken cancellationToken = default);

    // What the system-level OCR extraction process should call when extraction fails for an
    // Entry Folder: records the failure (via OcrExtractionRecords + a RecordHistory remark for
    // the reason) and returns normally so the caller can continue with the next folder.
    // fetchRunId links the record back to the FetchRuns row that produced it, when known.
    Task RecordFailureAsync(string requestNumber, string? rdCode, string? rdName, string folderPath, string failureReason, DateTime extractionDateTime, int? fetchRunId = null, CancellationToken cancellationToken = default);

    // A single still-Failed record by its row Id - used by the Reprocess action to look up the
    // FolderPath to send to the external service. Null if the id doesn't exist or is no longer
    // Failed (e.g. already reprocessed by someone else).
    Task<FailedExtractionListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    // The current still-Failed record (if any) for an exact FolderPath - used by the Reprocess
    // action after calling the external service to determine, from this app's own data rather
    // than the API response's free-text "status", whether that folder is still failing.
    Task<FailedExtractionListItemDto?> GetActiveFailedRecordByFolderPathAsync(string folderPath, CancellationToken cancellationToken = default);

    // Updates an EXISTING still-Failed record's ExtractionDateTime in place (never inserts a new
    // OcrExtractionRecords row) and appends a fresh RecordHistory remark with the latest failure
    // reason. Used by the Reprocess action when a reprocess attempt fails again - the external
    // service's own documentation says it updates these in place too, but that isn't reliably
    // observed in practice, so this app enforces it itself as the safety net, reusing the exact
    // same table/columns RecordFailureAsync already writes (no schema change, no duplicate row).
    Task UpdateFailureAsync(int id, string failureReason, DateTime extractionDateTime, CancellationToken cancellationToken = default);

    // Removes a Failed Extraction record whose reprocess attempt actually succeeded (confirmed
    // via the external service's own response, not merely an HTTP 200) - the external service
    // does not retroactively clean up this app's already-written row itself, so this app must.
    Task RemoveFailedRecordAsync(int id, CancellationToken cancellationToken = default);

    // True if a non-Failed OcrExtractionRecords row already exists for this exact FolderPath -
    // used when the external reprocess service reports "no Failed Extraction record found for
    // that folder path" (it clears its OWN internal failure tracking on success - see
    // IRdFetchApiClient.ReprocessFailedExtractionAsync), which can mean the folder was already
    // successfully reprocessed by an earlier attempt, not that the folder is genuinely unknown.
    Task<bool> HasSuccessfulRecordForFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
