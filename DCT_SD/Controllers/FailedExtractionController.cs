using DCT_SD.Models;
using DCT_SD.Models.Dtos.FailedExtraction;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.FailedExtraction}")]
public class FailedExtractionController : Controller
{
    private readonly IFailedExtractionService _failedExtractionService;
    private readonly IRegistryOfficeService _registryOfficeService;

    public FailedExtractionController(IFailedExtractionService failedExtractionService, IRegistryOfficeService registryOfficeService)
    {
        _failedExtractionService = failedExtractionService;
        _registryOfficeService = registryOfficeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FailedExtractionSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Failed Extraction";
        ViewData["ActiveMenu"] = MenuKeys.FailedExtraction;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        var model = await BuildResultsAsync(request, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] FailedExtractionSearchRequestDto request, CancellationToken cancellationToken)
    {
        var model = await BuildResultsAsync(request, cancellationToken);
        return PartialView("_Results", model);
    }

    private async Task<FailedExtractionResultsViewModel> BuildResultsAsync(FailedExtractionSearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _failedExtractionService.SearchAsync(request, cancellationToken);
        var hasAnyRecords = result.TotalCount > 0 || await _failedExtractionService.AnyRecordsExistAsync(cancellationToken);

        return new FailedExtractionResultsViewModel { Result = result, HasAnyRecords = hasAnyRecords };
    }
}
