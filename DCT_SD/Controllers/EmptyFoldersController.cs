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

    public EmptyFoldersController(IEmptyFolderService emptyFolderService)
    {
        _emptyFolderService = emptyFolderService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] EmptyFolderSearchRequestDto request, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Empty Entry Folders";
        ViewData["ActiveMenu"] = MenuKeys.EmptyFolders;

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
