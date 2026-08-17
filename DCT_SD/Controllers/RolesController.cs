using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Roles;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.Roles}")]
public class RolesController : Controller
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Roles";
        ViewData["ActiveMenu"] = MenuKeys.Roles;

        var roles = await _roleService.GetAllAsync(cancellationToken);
        return View(roles);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return PartialView("_Form", new RoleFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_Form", model);
        }

        try
        {
            await _roleService.CreateAsync(new CreateRoleRequestDto { Name = model.Name, Description = model.Description }, cancellationToken);
        }
        catch (ConflictException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return PartialView("_Form", model);
        }

        return Json(new { success = true, message = "The role has been successfully added." });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetByIdAsync(id, cancellationToken);
        return PartialView("_Form", new RoleFormViewModel
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystemDefined = role.IsSystemDefined,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RoleFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_Form", model);
        }

        try
        {
            await _roleService.UpdateAsync(id, new UpdateRoleRequestDto { Name = model.Name, Description = model.Description }, cancellationToken);
        }
        catch (Exception ex) when (ex is ConflictException or ForbiddenAppException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return PartialView("_Form", model);
        }

        return Json(new { success = true, message = "The role has been successfully updated." });
    }
}
