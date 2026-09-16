using DCT_SD.Models;
using DCT_SD.Models.Dtos.EmptyFolders;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.EmptyFolders}")]
public class EmptyFoldersController : Controller
{
    private readonly IEmptyFolderService _emptyFolderService;
    private readonly IRegistryOfficeService _registryOfficeService;

    public EmptyFoldersController(IEmptyFolderService emptyFolderService, IRegistryOfficeService registryOfficeService)
    {
        _emptyFolderService = emptyFolderService;
        _registryOfficeService = registryOfficeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] EmptyFolderSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Empty Folders";
        ViewData["ActiveMenu"] = MenuKeys.EmptyFolders;
        ViewData["RegistryOffices"] = await _registryOfficeService.GetAllActiveAsync(cancellationToken);

        var result = await _emptyFolderService.SearchAsync(request, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] EmptyFolderSearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _emptyFolderService.SearchAsync(request, cancellationToken);
        return PartialView("_Results", result);
    }
}
