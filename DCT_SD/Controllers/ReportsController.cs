using DCT_SD.Models;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.Reports}")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        ViewData["ActiveMenu"] = MenuKeys.Reports;
        ViewData["ReportTypes"] = ReportTypes.Labels;
        return View();
    }

    // AJAX-loaded whenever the Report Type dropdown changes, so the filter fields shown always
    // match the newly selected report and never linger from the previous selection.
    [HttpGet]
    public async Task<IActionResult> Filters(string reportType, CancellationToken cancellationToken)
    {
        var fields = await _reportService.GetFilterFieldsAsync(reportType, cancellationToken);
        return PartialView("_Filters", fields);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(string reportType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportType) || !ReportTypes.Labels.ContainsKey(reportType))
        {
            TempData["ToastMessage"] = "Report Type is required.";
            TempData["ToastVariant"] = "error";
            return RedirectToAction("Index");
        }

        var filters = Request.Form
            .Where(kv => kv.Key != "reportType" && kv.Key != "__RequestVerificationToken")
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());

        var result = await _reportService.GenerateAsync(reportType, filters, cancellationToken);

        if (!result.HasRecords)
        {
            TempData["ToastMessage"] = "No records found.";
            TempData["ToastVariant"] = "error";
            return RedirectToAction("Index");
        }

        return File(result.FileBytes!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
    }
}
