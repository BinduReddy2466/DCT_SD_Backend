using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Settings;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.Settings}")]
public class SettingsController : Controller
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Settings";
        ViewData["ActiveMenu"] = MenuKeys.Settings;

        ViewData["Branding"] = await _settingsService.GetBrandingAsync(cancellationToken);
        ViewData["EmailTemplates"] = await _settingsService.GetEmailTemplatesAsync(cancellationToken);

        var session = await _settingsService.GetSessionSettingsAsync(cancellationToken);
        return View(session);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSession(UpdateSessionSettingsRequestDto model, CancellationToken cancellationToken)
    {
        try
        {
            await _settingsService.UpdateSessionSettingsAsync(model, cancellationToken);
            TempData["ToastMessage"] = "Session settings saved.";
            TempData["ToastVariant"] = "success";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBrandingImage(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _settingsService.UpdateBrandingImageAsync(file, cancellationToken);
            return Json(new { success = true, message = "Login background image updated.", imageUrl = result.ImageUrl });
        }
        catch (BusinessValidationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBrandingImage(CancellationToken cancellationToken)
    {
        await _settingsService.RemoveBrandingImageAsync(cancellationToken);
        return Json(new { success = true, message = "Login background image removed." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmailTemplate(string key, UpdateEmailTemplateRequestDto model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _settingsService.UpdateEmailTemplateAsync(key, model, cancellationToken);
            return Json(new { success = true, message = "Email template saved.", template = result });
        }
        catch (NotFoundException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreEmailTemplateDefault(string key, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _settingsService.RestoreEmailTemplateDefaultAsync(key, cancellationToken);
            return Json(new { success = true, message = "Restored default template.", template = result });
        }
        catch (NotFoundException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
