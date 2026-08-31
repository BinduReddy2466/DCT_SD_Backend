using DCT_SD.Models;
using DCT_SD.Models.Dtos.Reports;
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

    // Live preview table: loaded automatically right after a Report Type is selected (with no
    // filters yet, so it shows every record), and again whenever Search or a pagination link is
    // used - always reflecting exactly what Generate Report would currently download.
    [HttpGet]
    public async Task<IActionResult> Results(string reportType, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportType) || !ReportTypes.Labels.ContainsKey(reportType))
        {
            return PartialView("_ResultsTable", new ReportPreviewDto());
        }

        var filters = Request.Query
            .Where(kv => kv.Key is not ("reportType" or "pageNumber" or "pageSize"))
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());

        var preview = await _reportService.GetPreviewAsync(reportType, filters, pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 25 : pageSize, cancellationToken);
        return PartialView("_ResultsTable", preview);
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
