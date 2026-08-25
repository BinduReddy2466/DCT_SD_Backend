using System.Security.Claims;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Roles;
using DCT_SD.Models.Dtos.Users;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.UserManagement}")]
public class UsersController : Controller
{
    private static readonly HashSet<string> NonSubAdminRoles = new() { "Encoder", "LARES QA", "LRA QA" };

    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IMenuService _menuService;

    public UsersController(IUserService userService, IRoleService roleService, IMenuService menuService)
    {
        _userService = userService;
        _roleService = roleService;
        _menuService = menuService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UserIndexQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "User Management";
        ViewData["ActiveMenu"] = MenuKeys.UserManagement;

        var result = await SearchAsync(query, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Results([FromQuery] UserIndexQuery query, CancellationToken cancellationToken)
    {
        var result = await SearchAsync(query, cancellationToken);
        return PartialView("_Results", result);
    }

    private async Task<Models.PagedResult<UserListItemDto>> SearchAsync(UserIndexQuery query, CancellationToken cancellationToken)
    {
        int? roleId = null;
        if (!string.IsNullOrWhiteSpace(query.RoleName))
        {
            var roles = await _roleService.GetAllAsync(cancellationToken);
            roleId = roles.FirstOrDefault(r => r.Name == query.RoleName)?.Id;
        }

        return await _userService.SearchAsync(new UserSearchRequestDto
        {
            SearchTerm = query.SearchTerm,
            RoleId = roleId,
            Status = query.Status,
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
        }, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new UserFormViewModel
        {
            RoleOptions = await GetRoleOptionsAsync(CurrentRole, editingUserRole: null, cancellationToken),
            Menus = await _menuService.GetAllAsync(cancellationToken),
        };
        return PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken cancellationToken)
    {
        ValidateCreateFields(model);

        if (!ModelState.IsValid)
        {
            model.RoleOptions = await GetRoleOptionsAsync(CurrentRole, null, cancellationToken);
            model.Menus = await _menuService.GetAllAsync(cancellationToken);
            return PartialView("_Form", model);
        }

        try
        {
            await _userService.CreateAsync(new CreateUserRequestDto
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Username = model.Username.Trim(),
                Password = model.Password ?? string.Empty,
                RoleId = model.RoleId ?? 0,
                AssignedMenuIds = model.AssignedMenuIds,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is ConflictException or ForbiddenAppException or BusinessValidationException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.RoleOptions = await GetRoleOptionsAsync(CurrentRole, null, cancellationToken);
            model.Menus = await _menuService.GetAllAsync(cancellationToken);
            return PartialView("_Form", model);
        }

        return Json(new { success = true, message = "Account successfully created." });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);

        // The Edit button is hidden entirely for Administrator rows - this guards the direct
        // URL too (e.g. someone navigating straight to /Users/Edit/{id}).
        if (user.Role == RoleNames.Administrator)
        {
            return StatusCode(403, "Administrator accounts cannot be edited through User Management.");
        }

        var model = new UserFormViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Password = user.PasswordHash,
            RoleId = user.RoleId,
            Status = user.Status,
            AssignedMenuIds = user.AssignedMenuIds.ToList(),
            IsEditing = true,
            RoleDisabled = IsRoleDisabled(user.Role, user.Username),
            RoleOptions = await GetRoleOptionsAsync(CurrentRole, user.Role, cancellationToken),
            Menus = await _menuService.GetAllAsync(cancellationToken),
        };
        return PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        model.IsEditing = true;

        ValidateEditFields(model);

        if (!ModelState.IsValid)
        {
            await RepopulateEditFormAsync(model, id, cancellationToken);
            return PartialView("_Form", model);
        }

        try
        {
            await _userService.UpdateAsync(id, new UpdateUserRequestDto
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                RoleId = model.RoleId ?? 0,
                Status = model.Status,
                AssignedMenuIds = model.AssignedMenuIds,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is ConflictException or ForbiddenAppException or BusinessValidationException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await RepopulateEditFormAsync(model, id, cancellationToken);
            return PartialView("_Form", model);
        }

        return Json(new { success = true, message = "Account successfully updated." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _userService.DeleteAsync(id, cancellationToken);
            TempData["ToastMessage"] = "Account successfully deleted.";
            TempData["ToastVariant"] = "success";
        }
        catch (Exception ex) when (ex is ForbiddenAppException or BusinessValidationException or NotFoundException)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "error";
        }

        return RedirectToAction("Index");
    }

    private void ValidateCreateFields(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Username is required.");
        }
        else if (!UserValidation.IsValidUsername(model.Username.Trim()))
        {
            ModelState.AddModelError(nameof(model.Username),
                "The username you entered is invalid. It must be a valid email using only letters, numbers, periods (.) and underscores (_), and cannot start or end with a special character.");
        }

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }
        else if (!UserValidation.IsValidPassword(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password),
                "The password does not meet the minimum requirements: 8-32 characters with at least one uppercase, one lowercase, one number, and one special character (!,@#$%^&*_-+=).");
        }

        if (model.RoleId is null or 0)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Please select a role.");
        }
    }

    private void ValidateEditFields(UserFormViewModel model)
    {
        // Password is a disabled, display-only field on Edit (it shows the stored hash, not
        // an editable value) - there is nothing to validate here.
        if (model.RoleId is null or 0)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Please select a role.");
        }
    }

    private string? CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value;

    // Role ID 1 always means Administrator and that can never change - the dropdown is locked
    // for every Administrator account, not just when the viewer is editing themselves, so one
    // Administrator can't demote another through this form either. A Sub-Admin account's level
    // is likewise locked for anyone editing it.
    private bool IsRoleDisabled(string editingUserRole, string editingUsername) =>
        editingUserRole == RoleNames.SubAdmin || editingUserRole == RoleNames.Administrator;

    private async Task<IReadOnlyList<RoleDto>> GetRoleOptionsAsync(string? currentRole, string? editingUserRole, CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetAllAsync(cancellationToken);
        var options = roles.Where(r => r.Name != RoleNames.Administrator);

        if (currentRole == RoleNames.SubAdmin)
        {
            options = options.Where(r => r.Name != RoleNames.SubAdmin);
        }

        if (editingUserRole != null && NonSubAdminRoles.Contains(editingUserRole))
        {
            options = options.Where(r => r.Name != RoleNames.SubAdmin);
        }

        var result = options.ToList();

        // The dropdown is disabled (see IsRoleDisabled) whenever editingUserRole is
        // Administrator or Sub-Admin, but those two roles are excluded from the assignable
        // list built above. Without this, a disabled dropdown for such an account would show
        // no selected option at all instead of the account's actual current role.
        if (editingUserRole != null && result.All(r => r.Name != editingUserRole))
        {
            var currentRoleDto = roles.FirstOrDefault(r => r.Name == editingUserRole);
            if (currentRoleDto != null)
            {
                result.Insert(0, currentRoleDto);
            }
        }

        return result;
    }

    private async Task RepopulateEditFormAsync(UserFormViewModel model, int id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        model.Password = user.PasswordHash;
        model.RoleDisabled = IsRoleDisabled(user.Role, user.Username);
        model.RoleOptions = await GetRoleOptionsAsync(CurrentRole, user.Role, cancellationToken);
        model.Menus = await _menuService.GetAllAsync(cancellationToken);
    }
}

public class UserIndexQuery
{
    public string? SearchTerm { get; set; }
    public string? RoleName { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
