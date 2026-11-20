using DCT_SD.Models;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.Dashboard}")]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IRegistryOfficeService _registryOfficeService;

    public DashboardController(IDashboardService dashboardService, IRegistryOfficeService registryOfficeService)
    {
        _dashboardService = dashboardService;
        _registryOfficeService = registryOfficeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? rdCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Dashboard";
        ViewData["ActiveMenu"] = MenuKeys.Dashboard;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        var stats = await _dashboardService.GetStatsAsync(rdCode, pageNumber, pageSize, cancellationToken);
        return View(stats);
    }

    // AJAX partial-view swap target for the statistics table - used by list-page.js (pagination
    // links, page-size change, RD Code filter Search/Clear) and by the page's own periodic
    // live-refresh poll (dashboard.js), both of which only ever need to redraw the table, never
    // the KPI cards/charts above it.
    [HttpGet]
    public async Task<IActionResult> Results(string? rdCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var stats = await _dashboardService.GetStatsAsync(rdCode, pageNumber, pageSize, cancellationToken);
        return PartialView("_Results", stats.Table);
    }
}
