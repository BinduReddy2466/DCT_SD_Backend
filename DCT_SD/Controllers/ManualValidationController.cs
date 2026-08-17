using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.ManualValidation}")]
public class ManualValidationController : Controller
{
    private readonly IManualValidationService _manualValidationService;
    private readonly IRegistryOfficeService _registryOfficeService;

    public ManualValidationController(IManualValidationService manualValidationService, IRegistryOfficeService registryOfficeService)
    {
        _manualValidationService = manualValidationService;
        _registryOfficeService = registryOfficeService;
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
            return View(detail);
        }
        catch (NotFoundException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, SaveManualValidationRequestDto model, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _manualValidationService.SaveAsync(id, model, cancellationToken);
            return Json(new { success = true, message = "Saved Successfully.", rdName = detail.RdName ?? "", missingFields = detail.MissingFields });
        }
        catch (NotFoundException ex)
        {
            return Json(new { success = false, message = ex.Message });
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
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Migrate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _manualValidationService.MigrateAsync(id, cancellationToken);
            return Json(new { success = true, message = "Record validated and migrated to PHILARIS-RD." });
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException)
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
            return Json(new { success = true, sequence = result.Sequence });
        }
        catch (NotFoundException)
        {
            return Json(new { success = false });
        }
    }
}
