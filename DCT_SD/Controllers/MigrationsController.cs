using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.MigrationMonitoring}")]
public class MigrationsController : Controller
{
    private readonly IMigrationService _migrationService;
    private readonly IRegistryOfficeService _registryOfficeService;

    public MigrationsController(IMigrationService migrationService, IRegistryOfficeService registryOfficeService)
    {
        _migrationService = migrationService;
        _registryOfficeService = registryOfficeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MigrationSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Migration Monitoring";
        ViewData["ActiveMenu"] = MenuKeys.MigrationMonitoring;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        var result = await _migrationService.SearchAsync(request, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] MigrationSearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _migrationService.SearchAsync(request, cancellationToken);
        return PartialView("_Results", result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.GetByIdAsync(id, cancellationToken);
            ViewData["Title"] = "Migration Details";
            ViewData["ActiveMenu"] = MenuKeys.MigrationMonitoring;
            return View(detail);
        }
        catch (NotFoundException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
            return RedirectToAction("Index");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Compare(int id, int documentId, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.GetByIdAsync(id, cancellationToken);
            var document = detail.Documents.FirstOrDefault(d => d.Id == documentId);
            if (document is null)
            {
                TempData["ToastMessage"] = "Unable to load this document for comparison.";
                TempData["ToastVariant"] = "error";
                return RedirectToAction("Details", new { id });
            }

            ViewData["Title"] = "Compare Supporting Document";
            ViewData["ActiveMenu"] = MenuKeys.MigrationMonitoring;
            ViewData["MigrationId"] = id;
            return View(document);
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
    public async Task<IActionResult> Overwrite(int id, int documentId, CancellationToken cancellationToken)
    {
        try
        {
            await _migrationService.OverwriteDocumentAsync(id, documentId, cancellationToken);
            TempData["ToastMessage"] = "The supporting document image has been overwritten successfully.";
            TempData["ToastVariant"] = "success";
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InsertAsNew(int id, int documentId, CancellationToken cancellationToken)
    {
        try
        {
            await _migrationService.InsertAsNewDocumentAsync(id, documentId, cancellationToken);
            TempData["ToastMessage"] = "The supporting document image has been inserted as new successfully.";
            TempData["ToastVariant"] = "success";
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
        }

        return RedirectToAction("Details", new { id });
    }
}
