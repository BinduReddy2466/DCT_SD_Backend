using DCT_SD.Models;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.Dashboard}")]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Dashboard";
        ViewData["ActiveMenu"] = MenuKeys.Dashboard;

        var rows = await _dashboardService.GetStatsAsync(cancellationToken);
        return View(rows);
    }
}
