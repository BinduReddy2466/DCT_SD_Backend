using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.MigrationMonitoring}")]
public class MigrationsController : Controller
{
    private readonly IMigrationService _migrationService;
    private readonly IRegistryOfficeService _registryOfficeService;
    private readonly IDocumentTypeService _documentTypeService;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly ILogger<MigrationsController> _logger;

    public MigrationsController(
        IMigrationService migrationService,
        IRegistryOfficeService registryOfficeService,
        IDocumentTypeService documentTypeService,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<MigrationsController> logger)
    {
        _migrationService = migrationService;
        _registryOfficeService = registryOfficeService;
        _documentTypeService = documentTypeService;
        _backgroundTaskQueue = backgroundTaskQueue;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MigrationSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Migration Monitoring";
        ViewData["ActiveMenu"] = MenuKeys.MigrationMonitoring;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        if (!TryValidateDateRange(request, out _))
        {
            return View(new PagedResult<MigrationListItemDto> { PageNumber = 1, PageSize = 25 });
        }

        // The landing page (and the Clear button, which resolves to this same unfiltered state)
        // always shows the full Migration Monitoring table, exactly like any other list page in
        // this app - Entry Number is only mandatory for the Search button's own click, enforced
        // client-side in list-page.js, never for simply viewing/browsing the table.
        var result = await _migrationService.SearchAsync(request, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] MigrationSearchRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryValidateDateRange(request, out var errorMessage))
        {
            return BadRequest(errorMessage);
        }

        var result = await _migrationService.SearchAsync(request, cancellationToken);
        return PartialView("_Results", result);
    }

    // Date range validity (task section 5) is checked here in addition to the client-side
    // min/max date restriction in migration-monitoring.js, per the task's explicit "perform
    // server-side validation even if client-side validation is implemented." Entry Number's own
    // mandatory check is NOT a hard error here (see Index/Results above) - Clear must always be
    // able to resolve to the blank/initial state without tripping a validation error, and the
    // exact-text requirement is fully satisfied by the client-side check on an actual Search click.
    private static bool TryValidateDateRange(MigrationSearchRequestDto request, out string? errorMessage)
    {
        if (request.DateFrom.HasValue && request.DateTo.HasValue && request.DateFrom.Value.Date > request.DateTo.Value.Date)
        {
            errorMessage = "Migration Date From must be on or before Migration Date To.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.GetByIdAsync(id, cancellationToken);
            ViewData["Title"] = "Migration Details";
            ViewData["ActiveMenu"] = MenuKeys.MigrationMonitoring;
            ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);
            ViewData["DocumentTypes"] = await _documentTypeService.GetAllActiveAsync(cancellationToken);
            return View(detail);
        }
        catch (NotFoundException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
            return RedirectToAction("Index");
        }
    }

    // Lazy-loaded data for one row's expandable panel on the Migration Monitoring list page -
    // reuses GetByIdAsync's already-built Associated Title Records / Supporting Documents data
    // (the same data the Details page shows) rather than a separate query path.
    [HttpGet]
    public async Task<IActionResult> Expand(int id, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.GetByIdAsync(id, cancellationToken);
            return Json(new { titleRecords = detail.TitleRecords, documents = detail.Documents });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    // Re-fetches just the Action History table fragment after Save/Insert as New Image/Confirm
    // Duplicate, none of which reload the page - so the user sees the entry they just wrote
    // without one (spec section 13's "keep Action History updated after successful operations").
    [HttpGet]
    public async Task<IActionResult> ActionHistoryInline(int id, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.GetByIdAsync(id, cancellationToken);
            return PartialView("_ActionHistoryTable", detail.ActionHistory);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    // Streams a supporting document's actual source (DCT-SD) image straight from disk - see
    // MigrationService.GetDocumentImagePathAsync. There is no equivalent endpoint for the
    // PHILARIS-RD (target) side: DestinationPath is a path on that external system, which this
    // app has no filesystem access to serve bytes from.
    [HttpGet]
    public async Task<IActionResult> DocumentImage(int id, int documentId, CancellationToken cancellationToken)
    {
        var imagePath = await _migrationService.GetDocumentImagePathAsync(id, documentId, cancellationToken);
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

    // Manual retry for a record stuck in Failed - nothing retries it automatically, regardless of
    // origin (no batch/background processing - client requirement, 2026-11-12). AJAX (JSON) -
    // called from both the Migration Details page and the Migration Monitoring list row action;
    // either caller reloads its own page on completion rather than this action re-rendering
    // anything itself, since Reprocess can change the Migration Status, Reason for Failing,
    // Supporting Documents, and Action History all at once.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprocess(int id, string remarks, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.ReprocessAsync(id, remarks, cancellationToken);
            return Json(new
            {
                success = true,
                message = detail.ProcessingStatus == "Completed"
                    ? "This Entry Record was reprocessed successfully."
                    : "Reprocess attempt did not complete - see Reason for Failing.",
            });
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException or ConflictException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Insert as New Image / Confirm Duplicate (spec sections 7-8) - both AJAX (JSON), not a
    // full-page redirect, so the Supporting Documents table and viewer update immediately without
    // a reload (spec sections 7.3/8.2). Replaces the old Overwrite action entirely.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InsertAsNew(int id, int documentId, string remarks, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.InsertAsNewDocumentAsync(id, documentId, remarks, cancellationToken);
            return Json(new { success = true, message = "The supporting document image has been inserted as a new image.", documents = detail.Documents, sdStatus = detail.SdStatus });
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException or BusinessValidationException)
        {
            // InsertAsNewDocumentAsync already persisted ReasonForFailing on this document before
            // throwing (spec: "Display the failure reason in the Reason for Failing column of the
            // Supporting Document List") - re-fetch so the client can show it without a page
            // reload, same as the success branch above already does.
            var documents = await TryGetDocumentsAsync(id, cancellationToken);
            return Json(new { success = false, message = "Unable to insert the image as a new image. Please review the failure reason.", detail = ex.Message, documents });
        }
        // Renaming the image on disk before resubmitting it - see MigrationService
        // .InsertAsNewDocumentAsync - can fail the same way ManualValidationController.Save's
        // identical catch anticipates for its own Document Type rename.
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Migration Details Insert as New Image failed for record {RecordId} / document {DocumentId} due to a file system error.", id, documentId);
            return Json(new { success = false, message = "Unable to insert the image as a new image because the file could not be renamed on disk. Please try again, and contact support if this keeps happening." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDuplicate(int id, int documentId, string remarks, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.ConfirmDuplicateAsync(id, documentId, remarks, cancellationToken);
            return Json(new { success = true, message = "This image has been confirmed as a duplicate.", documents = detail.Documents, sdStatus = detail.SdStatus });
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException or BusinessValidationException)
        {
            return Json(new { success = false, message = "Unable to confirm the image as a duplicate. Please review the failure reason.", detail = ex.Message });
        }
    }

    // Manual corrections Save (spec section 4) - AJAX (JSON), matching Manual Validation's own
    // Save convention.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, SaveMigrationCorrectionsRequestDto model, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.SaveCorrectionsAsync(id, model, cancellationToken);
            return Json(new
            {
                success = true,
                message = "Saved Successfully.",
                rdCode = detail.RdCode,
                rdName = detail.RdName,
                isTargetRdNotIdentified = detail.IsTargetRdNotIdentified,
                canMarkReadyForMigration = detail.CanMarkReadyForMigration,
                canTriggerReadyForMigration = detail.CanTriggerReadyForMigration,
                documents = detail.Documents,
            });
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException)
        {
            return Json(new { success = false, message = ex.Message });
        }
        // A Document Type correction renames a physical file on disk - see
        // ManualValidationController.Save's identical catch for the same reasoning.
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Migration Details Save failed for record {RecordId} due to a file system error.", id);
            return Json(new { success = false, message = "Unable to save changes because a supporting document file could not be renamed on disk. Please try again, and contact support if this keeps happening." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadyForMigration(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _migrationService.MarkReadyForMigrationAsync(id, cancellationToken);
            return Json(new { success = true, message = "This Entry Record has been placed in the migration queue." });
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessValidationException or ConflictException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // New "Ready for Migration" trigger (client requirement, 2026-11-27) - distinct from the
    // legacy ReadyForMigration action above. Shown only when this Entry Record's Supporting
    // Document Status is "Supporting Documents Migrated with N/A" and its Migration Status isn't
    // already "Migrated" (see MigrationService.GetByIdAsync's canTriggerReadyForMigration). Mirrors
    // ManualValidationController.ReadyForMigration's enqueue pattern exactly - resolves
    // IMigrationService from the queued work item's own DI scope, never injected directly, reusing
    // the same ProcessEntryNumbersSequentiallyAsync driver so migration logic is never duplicated.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerReadyForMigration(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _migrationService.TriggerReadyForMigrationAsync(id, cancellationToken);
            _backgroundTaskQueue.Enqueue(async (services, ct) =>
            {
                var migrationService = services.GetRequiredService<IMigrationService>();
                var logger = services.GetRequiredService<ILogger<MigrationsController>>();
                try
                {
                    await migrationService.ProcessEntryNumbersSequentiallyAsync([id], ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background migration processing failed for MigrationRecord {Id} triggered from Migration Monitoring.", id);
                }
            });

            return Json(new { success = true, message = "This Entry Record has been marked Ready for Migration." });
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Best-effort re-fetch for a failure response that still needs to show a just-persisted
    // change (e.g. InsertAsNew's ReasonForFailing) - never lets a secondary failure here mask the
    // original error being returned to the client.
    private async Task<IReadOnlyList<MigrationDocumentDto>?> TryGetDocumentsAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _migrationService.GetByIdAsync(id, cancellationToken);
            return detail.Documents;
        }
        catch
        {
            return null;
        }
    }
}
