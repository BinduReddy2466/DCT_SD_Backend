using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.FailedExtraction;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.FailedExtraction}")]
public class FailedExtractionController : Controller
{
    private readonly IFailedExtractionService _failedExtractionService;
    private readonly IRegistryOfficeService _registryOfficeService;
    private readonly IRdFetchApiClient _rdFetchApiClient;
    private readonly ICurrentUserService _currentUserService;

    public FailedExtractionController(
        IFailedExtractionService failedExtractionService,
        IRegistryOfficeService registryOfficeService,
        IRdFetchApiClient rdFetchApiClient,
        ICurrentUserService currentUserService)
    {
        _failedExtractionService = failedExtractionService;
        _registryOfficeService = registryOfficeService;
        _rdFetchApiClient = rdFetchApiClient;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FailedExtractionSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Failed Extraction";
        ViewData["ActiveMenu"] = MenuKeys.FailedExtraction;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        var model = await BuildResultsAsync(request, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] FailedExtractionSearchRequestDto request, CancellationToken cancellationToken)
    {
        var model = await BuildResultsAsync(request, cancellationToken);
        return PartialView("_Results", model);
    }

    // Reprocesses ONLY the Entry Folder for this one Failed Extraction record - never the full
    // Fetch process - via the existing external OCR service's own dedicated reprocess endpoint.
    // Success/failure is decided from this app's own data plus the response's own
    // records_created/manual_validation_created counts, not by trusting the response's free-text
    // "status" alone. Confirmed directly against the live service: on a real success it clears
    // the folder from its OWN internal failure tracking and creates real
    // OcrExtractionRecords/ManualValidationRequests rows, but it does NOT go back and remove or
    // update the stale Failed row this app already wrote earlier - so this app must remove that
    // stale row itself whenever the response proves the retry actually succeeded.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprocess(int id, CancellationToken cancellationToken)
    {
        var record = await _failedExtractionService.GetByIdAsync(id, cancellationToken);
        if (record is null)
        {
            return Json(new { success = false, message = "This record could not be found - it may have already been reprocessed or removed." });
        }

        try
        {
            var response = await _rdFetchApiClient.ReprocessFailedExtractionAsync(record.FolderPath, _currentUserService.UserId, cancellationToken);

            var stillFailed = await _failedExtractionService.GetActiveFailedRecordByFolderPathAsync(record.FolderPath, cancellationToken);
            if (stillFailed is null)
            {
                return Json(new { success = true, message = "The folder was reprocessed successfully." });
            }

            var succeeded = response.RecordsCreated is > 0 || response.ManualValidationCreated is > 0;
            if (succeeded)
            {
                await _failedExtractionService.RemoveFailedRecordAsync(stillFailed.Id, cancellationToken);
                return Json(new { success = true, message = "The folder was reprocessed successfully." });
            }

            var reason = !string.IsNullOrWhiteSpace(response.Reason) ? response.Reason : stillFailed.FailureReason;
            var extractionDateTime = DateTime.UtcNow;
            await _failedExtractionService.UpdateFailureAsync(stillFailed.Id, reason, extractionDateTime, cancellationToken);

            return Json(new { success = false, message = string.IsNullOrWhiteSpace(reason) ? "Reprocessing failed." : $"Reprocessing failed: {reason}" });
        }
        catch (BusinessValidationException ex)
        {
            // The external service returns exactly this shape once it no longer has this folder
            // in its OWN internal failure tracking - which is also what happens once an earlier
            // reprocess attempt already succeeded (e.g. a lost/timed-out response that the user
            // then retried). If a real, successful record already exists for this folder, that's
            // what actually happened - clean up the stale row instead of reporting a false failure.
            if (await _failedExtractionService.HasSuccessfulRecordForFolderAsync(record.FolderPath, cancellationToken))
            {
                await _failedExtractionService.RemoveFailedRecordAsync(id, cancellationToken);
                return Json(new { success = true, message = "The folder was reprocessed successfully." });
            }

            return Json(new { success = false, message = ex.Message });
        }
    }

    private async Task<FailedExtractionResultsViewModel> BuildResultsAsync(FailedExtractionSearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _failedExtractionService.SearchAsync(request, cancellationToken);
        var hasAnyRecords = result.TotalCount > 0 || await _failedExtractionService.AnyRecordsExistAsync(cancellationToken);

        return new FailedExtractionResultsViewModel { Result = result, HasAnyRecords = hasAnyRecords };
    }
}
