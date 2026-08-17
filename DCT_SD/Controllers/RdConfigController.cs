using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.RdConfig;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.RdConfig}")]
public class RdConfigController : Controller
{
    private readonly IRdConfigService _rdConfigService;

    public RdConfigController(IRdConfigService rdConfigService)
    {
        _rdConfigService = rdConfigService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildIndexViewModelAsync(cancellationToken);
        return View(model);
    }

    private async Task<RdConfigIndexViewModel> BuildIndexViewModelAsync(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "RD Configuration";
        ViewData["ActiveMenu"] = MenuKeys.RdConfig;

        var rootPath = await _rdConfigService.GetCurrentRootPathAsync(cancellationToken);
        var rootHistory = await _rdConfigService.SearchRootPathHistoryAsync(new RootPathHistorySearchRequestDto { PageNumber = 1, PageSize = 25 }, cancellationToken);
        var fetchHistory = await _rdConfigService.SearchFetchHistoryAsync(new FetchHistorySearchRequestDto { PageNumber = 1, PageSize = 25 }, cancellationToken);

        return new RdConfigIndexViewModel
        {
            CurrentPath = rootPath.CurrentPath,
            LatestUpdate = rootHistory.Items.FirstOrDefault(),
            FetchHistory = fetchHistory,
            RootHistory = rootHistory,
            RootPathForm = new RootPathFormViewModel { RootPath = rootPath.CurrentPath ?? string.Empty },
        };
    }

    [HttpGet]
    public async Task<IActionResult> BrowseFolders(string? path, CancellationToken cancellationToken)
    {
        var result = await _rdConfigService.BrowseDirectoriesAsync(path, cancellationToken);
        return PartialView("_BrowseFolder", result);
    }

    [HttpGet]
    public async Task<IActionResult> FetchHistoryResults([FromQuery] FetchHistorySearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _rdConfigService.SearchFetchHistoryAsync(request, cancellationToken);
        return PartialView("_FetchHistoryResults", result);
    }

    [HttpGet]
    public async Task<IActionResult> RootHistoryResults([FromQuery] RootPathHistorySearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _rdConfigService.SearchRootPathHistoryAsync(request, cancellationToken);
        var hasAppliedFilters = request.DateFrom.HasValue || request.DateTo.HasValue || !string.IsNullOrWhiteSpace(request.ModifiedBy);
        return PartialView("_RootHistoryResults", new RootHistoryResultsViewModel { Result = result, HasAppliedFilters = hasAppliedFilters });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRootPath([Bind(Prefix = "RootPathForm")] RootPathFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildIndexViewModelAsync(cancellationToken);
            invalidModel.RootPathForm = model;
            return View("Index", invalidModel);
        }

        try
        {
            await _rdConfigService.UpdateRootPathAsync(new UpdateRootPathRequestDto { NewPath = model.RootPath, Remarks = model.Remarks }, cancellationToken);
            TempData["ToastMessage"] = "Root Source Path has been updated successfully.";
            TempData["ToastVariant"] = "success";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "default";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartFetch(CancellationToken cancellationToken)
    {
        try
        {
            await _rdConfigService.StartFetchAsync(cancellationToken);
            TempData["ToastMessage"] = "Fetching process started.";
            TempData["ToastVariant"] = "success";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
        }

        return RedirectToAction("Index");
    }
}
