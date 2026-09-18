using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.ManualValidation}")]
public class ManualValidationController : Controller
{
    private readonly IManualValidationService _manualValidationService;
    private readonly IRegistryOfficeService _registryOfficeService;
    private readonly IDocumentTypeService _documentTypeService;
    private readonly ILogger<ManualValidationController> _logger;

    public ManualValidationController(IManualValidationService manualValidationService, IRegistryOfficeService registryOfficeService, IDocumentTypeService documentTypeService, ILogger<ManualValidationController> logger)
    {
        _manualValidationService = manualValidationService;
        _registryOfficeService = registryOfficeService;
        _documentTypeService = documentTypeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ManualValidationSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Manual Validation";
        ViewData["ActiveMenu"] = MenuKeys.ManualValidation;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        var result = await _manualValidationService.SearchAsync(request, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] ManualValidationSearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _manualValidationService.SearchAsync(request, cancellationToken);
        return PartialView("_Results", result);
    }

    [HttpGet]
    public async Task<IActionResult> RemarksHistory(int id, string requestNumber, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var remarks = await _manualValidationService.GetRemarksHistoryAsync(id, pageNumber, 5, cancellationToken);
        return PartialView("_RemarksHistory", new RemarksHistoryViewModel
        {
            RecordId = id,
            RequestNumber = requestNumber,
            Remarks = remarks,
        });
    }

    [HttpGet]
    public async Task<IActionResult> RemarksHistoryInline(int id, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var remarks = await _manualValidationService.GetRemarksHistoryAsync(id, pageNumber, 5, cancellationToken);
        return PartialView("_RemarksHistoryTable", new RemarksHistoryViewModel { RecordId = id, Remarks = remarks });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _manualValidationService.OpenForEditAsync(id, cancellationToken);
            var remarks = await _manualValidationService.GetRemarksHistoryAsync(id, 1, 5, cancellationToken);

            ViewData["Title"] = "Manual Validation Details";
            ViewData["ActiveMenu"] = MenuKeys.ManualValidation;
            ViewData["Remarks"] = remarks;
            ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);
            ViewData["DocumentTypes"] = await _documentTypeService.GetAllActiveAsync(cancellationToken);
            return View(detail);
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenAppException)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
            return RedirectToAction("Index");
        }
    }

    // Streams a supporting document's image straight from disk, using the imagePath stored in
    // that record's DocumentsJson - never a client-supplied path (documentId is only the
    // synthesized 1-based position from ManualValidationDocumentDto.Id; the actual file path is
    // always looked up server-side).
    [HttpGet]
    public async Task<IActionResult> DocumentImage(int id, int documentId, CancellationToken cancellationToken)
    {
        var imagePath = await _manualValidationService.GetDocumentImagePathAsync(id, documentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
        {
            return NotFound();
        }

        if (!new FileExtensionContentTypeProvider().TryGetContentType(imagePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(imagePath, contentType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, SaveManualValidationRequestDto model, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _manualValidationService.SaveAsync(id, model, cancellationToken);
            // Renaming a document's file changes its DocumentName, which the Supporting
            // Documents list/viewer are sorted by - so the full, freshly re-sorted list is
            // returned here too, not just on the initial page load. titleRecords is likewise
            // returned so a re-save immediately reflects each row's own freshly computed missing
            // fields, without a full page reload.
            return Json(new { success = true, message = "Saved Successfully.", rdName = detail.RdName ?? "", missingFields = detail.MissingFields, titleRecords = detail.TitleRecords, documents = detail.Documents });
        }
        catch (Exception ex) when (ex is NotFoundException or ForbiddenAppException or BusinessValidationException)
        {
            return Json(new { success = false, message = ex.Message });
        }
        // A Document Type correction renames a physical file on disk (see
        // ManualValidationService.ApplyDocumentTypeChange) - unlike the exceptions above, a file
        // system failure here (e.g. the destination file already exists, or the file is locked/
        // permission-denied) isn't a BusinessValidationException, so without this it would fall
        // through to the app's generic HTML error page instead of JSON, which the client's
        // fetch(...).then(r => r.json()) can't parse - the Save silently appears to do nothing at
        // all in the browser, with no toast and no indication of what happened. Caught here so the
        // user always gets an explicit error message instead of silence, while the full exception
        // is still logged for diagnosis.
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Manual Validation Save failed for record {RecordId} due to a file system error.", id);
            return Json(new { success = false, message = "Unable to save changes because a supporting document file could not be renamed on disk. Please try again, and contact support if this keeps happening." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id, [FromForm] string remarks, CancellationToken cancellationToken)
    {
        try
        {
            await _manualValidationService.CloseAsync(id, remarks, cancellationToken);
            return Json(new { success = true, message = "Record closed with remarks." });
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException or ForbiddenAppException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Replaces the old "Migrate" action - only flags the record Status as Ready for Migration and
    // records it in Action History; never starts an actual migration.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadyForMigration(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _manualValidationService.MarkReadyForMigrationAsync(id, cancellationToken);
            return Json(new { success = true, message = "Record marked as Ready for Migration." });
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException or ForbiddenAppException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetrieveTitleSequence(RetrieveTitleSequenceRequestDto model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _manualValidationService.RetrieveTitleSequenceAsync(model, cancellationToken);
            if (result.IsAmbiguous)
            {
                return Json(new { success = false, ambiguous = true, candidates = result.Candidates });
            }

            return Json(new { success = true, sequence = result.Sequence });
        }
        catch (NotFoundException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
