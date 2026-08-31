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
    Task RecordFailureAsync(string requestNumber, string? rdCode, string? rdName, string folderPath, string failureReason, DateTime extractionDateTime, CancellationToken cancellationToken = default);
}
