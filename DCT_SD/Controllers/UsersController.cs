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
    private readonly ISettingsService _settingsService;
    private readonly ITokenService _tokenService;
    private readonly IEmailSenderService _emailSenderService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IRoleService roleService,
        IMenuService menuService,
        ISettingsService settingsService,
        ITokenService tokenService,
        IEmailSenderService emailSenderService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _roleService = roleService;
        _menuService = menuService;
        _settingsService = settingsService;
        _tokenService = tokenService;
        _emailSenderService = emailSenderService;
        _logger = logger;
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

        UserDetailDto createdUser;
        try
        {
            createdUser = await _userService.CreateAsync(new CreateUserRequestDto
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

        // The account is already created at this point - a notification-email failure (bad SMTP
        // config, mail server down, etc.) must never turn a successful account creation into a
        // failure response, so this is fully isolated from the success result below.
        await SendRegistrationEmailAsync(createdUser, model.Password ?? string.Empty, cancellationToken);

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

        // Likewise, the Edit button is hidden for a Sub-Admin viewer looking at any Sub-Admin
        // row - another one's, or their own.
        if (CurrentRole == RoleNames.SubAdmin && user.Role == RoleNames.SubAdmin)
        {
            return StatusCode(403, "A Sub-Admin cannot edit a Sub-Admin account.");
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

        // Read before the update so there's something to compare the new status against - the
        // acceptance criteria only send a status-change email when the status actually changed,
        // never on a save that leaves it the same.
        var previousStatus = (await _userService.GetByIdAsync(id, cancellationToken)).Status;

        UserDetailDto updatedUser;
        try
        {
            updatedUser = await _userService.UpdateAsync(id, new UpdateUserRequestDto
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

        // The update already succeeded at this point - same isolation as the registration email,
        // a notification failure must never turn a successful update into a failure response.
        await SendStatusChangeEmailAsync(updatedUser, previousStatus, cancellationToken);

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
        var missingRequiredField = string.IsNullOrWhiteSpace(model.FirstName)
            || string.IsNullOrWhiteSpace(model.LastName)
            || string.IsNullOrWhiteSpace(model.Username)
            || string.IsNullOrWhiteSpace(model.Password)
            || model.RoleId is null or 0;

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
                "The password you entered does not meet the minimum security requirements described below:\n" +
                "Password must be between 8 to 32 characters long;\n" +
                "Password must contain at least one uppercase letter;\n" +
                "Password must contain at least one lowercase letter;\n" +
                "Password must contain at least one number;\n" +
                "Password must contain at least one special character (!,@#$%^&*_-+=).");
        }

        if (model.RoleId is null or 0)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Please select a role.");
        }

        // Additive to the specific per-field messages above - this is the one general-purpose
        // message the acceptance criteria require whenever any mandatory field is empty or the
        // Role is still "Select", shown alongside (not instead of) the field-level detail.
        if (missingRequiredField)
        {
            ModelState.AddModelError(string.Empty, "Please fill out all required fields.");
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

    private async Task SendRegistrationEmailAsync(UserDetailDto createdUser, string temporaryPassword, CancellationToken cancellationToken)
    {
        try
        {
            var templates = await _settingsService.GetEmailTemplatesAsync(cancellationToken);
            var template = templates.FirstOrDefault(t => t.Key == "user_created");
            if (template is null)
            {
                _logger.LogWarning("No 'user_created' email template is configured - registration email for user {UserId} was not sent.", createdUser.Id);
                return;
            }

            var (resetToken, _) = _tokenService.CreatePasswordResetToken(createdUser.Id, createdUser.Username);
            var resetLink = Url.Action("ResetPassword", "Account", new { token = resetToken }, Request.Scheme)
                ?? string.Empty;

            var values = new Dictionary<string, string>
            {
                ["{{FirstName}}"] = createdUser.FirstName,
                ["{{LastName}}"] = createdUser.LastName,
                ["{{Email}}"] = createdUser.Username,
                ["{{TemporaryPassword}}"] = temporaryPassword,
                ["{{ResetPasswordLink}}"] = resetLink,
                ["{{CurrentDate}}"] = DateTime.UtcNow.ToLocalDisplay().ToString("MM-dd-yyyy"),
            };

            var subject = EmailPlaceholders.FillWithValues(template.Subject, values);
            var body = EmailPlaceholders.FillWithValues(template.Body, values);

            await _emailSenderService.SendAsync(createdUser.Username, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send the registration email for user {UserId}.", createdUser.Id);
        }
    }

    private async Task SendStatusChangeEmailAsync(UserDetailDto updatedUser, string previousStatus, CancellationToken cancellationToken)
    {
        if (string.Equals(previousStatus, updatedUser.Status, StringComparison.OrdinalIgnoreCase))
        {
            // No actual status change - e.g. a Role/Assign-Tab-only edit - so no notification.
            return;
        }

        var templateKey = updatedUser.Status switch
        {
            "Locked" => "user_locked",
            "Active" => "user_activated",
            "Deactivated" => "user_deactivated",
            _ => null,
        };
        if (templateKey is null)
        {
            return;
        }

        try
        {
            var templates = await _settingsService.GetEmailTemplatesAsync(cancellationToken);
            var template = templates.FirstOrDefault(t => t.Key == templateKey);
            if (template is null)
            {
                _logger.LogWarning("No '{TemplateKey}' email template is configured - status-change email for user {UserId} was not sent.", templateKey, updatedUser.Id);
                return;
            }

            var values = new Dictionary<string, string>
            {
                ["{{FirstName}}"] = updatedUser.FirstName,
                ["{{LastName}}"] = updatedUser.LastName,
                ["{{Email}}"] = updatedUser.Username,
                ["{{CurrentDate}}"] = DateTime.UtcNow.ToLocalDisplay().ToString("MM-dd-yyyy"),
            };

            var subject = EmailPlaceholders.FillWithValues(template.Subject, values);
            var body = EmailPlaceholders.FillWithValues(template.Body, values);

            await _emailSenderService.SendAsync(updatedUser.Username, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send the '{TemplateKey}' status-change email for user {UserId}.", templateKey, updatedUser.Id);
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
        // Encoder/LARES QA/LRA QA are no longer offered as a selectable role - Sub-Admin is the
        // only assignable option now. An existing account already in one of those roles still
        // displays correctly (see the editingUserRole fallback below); it just can't be newly
        // assigned to anyone else.
        var options = roles.Where(r => r.Name != RoleNames.Administrator && !NonSubAdminRoles.Contains(r.Name));

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
