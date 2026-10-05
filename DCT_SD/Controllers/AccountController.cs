using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models.Dtos.Auth;
using DCT_SD.Models.Dtos.Users;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ISettingsService _settingsService;
    private readonly IManualValidationService _manualValidationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserService _userService;
    private readonly IEmailSenderService _emailSenderService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService authService,
        ITokenService tokenService,
        ISettingsService settingsService,
        IManualValidationService manualValidationService,
        ICurrentUserService currentUserService,
        IUserService userService,
        IEmailSenderService emailSenderService,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _tokenService = tokenService;
        _settingsService = settingsService;
        _manualValidationService = manualValidationService;
        _currentUserService = currentUserService;
        _userService = userService;
        _emailSenderService = emailSenderService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var branding = await _settingsService.GetBrandingAsync(cancellationToken);
        ViewData["LoginBackgroundUrl"] = branding.ImageUrl;

        // The inactivity-timer script (wwwroot/js/inactivity-timer.js) redirects here with this
        // query string after it force-ends a session on the client's own 15-minute clock - the
        // session itself was already ended server-side (via Logout) before the redirect, this is
        // purely what tells the user why they landed back here.
        if (reason == "inactivity")
        {
            TempData["ToastMessage"] = "Your session has expired due to inactivity. Please log in again.";
            TempData["ToastVariant"] = "error";
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var branding = await _settingsService.GetBrandingAsync(cancellationToken);
        ViewData["LoginBackgroundUrl"] = branding.ImageUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await _authService.LoginAsync(model.Username, model.Password, cancellationToken);
            var allowedMenus = await _authService.ResolveAllowedMenusAsync(user, cancellationToken);

            var (accessToken, accessExpiresAt) = _tokenService.CreateAccessToken(user.Id, user.Username, user.RoleName, allowedMenus);
            var (refreshToken, refreshExpiresAt) = await _authService.IssueRefreshTokenAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

            AuthCookieHelper.SetAccessTokenCookie(Response, accessToken, accessExpiresAt);
            AuthCookieHelper.SetRefreshTokenCookie(Response, refreshToken, refreshExpiresAt);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (UnauthorizedAppException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // Release any Manual Validation review locks this user is holding - otherwise logging out
        // without first clicking Close leaves those records reported as locked to them until the
        // 15-minute LockTimeout elapses, blocking other users from opening them in the meantime.
        if (_currentUserService.UserId is { } userId)
        {
            await _manualValidationService.ReleaseLocksForUserAsync(userId, cancellationToken);
        }

        var refreshToken = Request.Cookies[AuthCookieHelper.RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }

        AuthCookieHelper.ClearAuthCookies(Response);
        return RedirectToAction("Login");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userService.FindByUsernameAsync(model.Email.Trim(), cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Email), "The email address provided is not valid.");
            return View(model);
        }

        await SendPasswordResetEmailAsync(user, cancellationToken);

        TempData["ToastMessage"] = "A password reset email has been sent to the provided email address";
        TempData["ToastVariant"] = "success";
        return RedirectToAction("ForgotPassword");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string token)
    {
        // Validated up front purely so the page can show the right invalid/expired state
        // immediately instead of making the user fill out the form first - the POST handler
        // below validates the token again regardless, since that's the actual security check.
        var validation = string.IsNullOrWhiteSpace(token)
            ? new PasswordResetTokenValidation(false, false, null, null)
            : _tokenService.ValidatePasswordResetToken(token);

        if (validation.IsExpired)
        {
            ViewData["TokenExpired"] = true;
            return View(new ResetPasswordViewModel());
        }

        if (!validation.IsValid)
        {
            ViewData["TokenInvalid"] = true;
            return View(new ResetPasswordViewModel());
        }

        return View(new ResetPasswordViewModel { Token = token, Email = validation.Email ?? string.Empty });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        var validation = _tokenService.ValidatePasswordResetToken(model.Token);
        if (validation.IsExpired)
        {
            ViewData["TokenExpired"] = true;
            return View(model);
        }

        if (!validation.IsValid)
        {
            ViewData["TokenInvalid"] = true;
            return View(model);
        }

        model.Email = validation.Email ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.NewPassword) || string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            ModelState.AddModelError(string.Empty, "All fields are required. Please complete the form before submitting.");
        }
        else if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "The new password and the re-entered password do not match.");
        }
        else if (!UserValidation.IsValidPassword(model.NewPassword))
        {
            ModelState.AddModelError(nameof(model.NewPassword),
                "The password you entered does not meet the minimum security requirements described below:\n" +
                "Password must be between 8 to 32 characters long;\n" +
                "Password must contain at least one uppercase letter;\n" +
                "Password must contain at least one lowercase letter;\n" +
                "Password must contain at least one number;\n" +
                "Password must contain at least one special character (!,@#$%^&*_-+=).");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _userService.ResetPasswordAsync(validation.UserId!.Value, model.NewPassword, cancellationToken);

        TempData["ToastMessage"] = "Your password has been reset. Please sign in with your new password.";
        TempData["ToastVariant"] = "success";
        return RedirectToAction("Login");
    }

    private async Task SendPasswordResetEmailAsync(UserDetailDto user, CancellationToken cancellationToken)
    {
        try
        {
            var templates = await _settingsService.GetEmailTemplatesAsync(cancellationToken);
            var template = templates.FirstOrDefault(t => t.Key == "password_reset");
            if (template is null)
            {
                _logger.LogWarning("No 'password_reset' email template is configured - Forgot Password email for user {UserId} was not sent.", user.Id);
                return;
            }

            var (resetToken, _) = _tokenService.CreatePasswordResetToken(user.Id, user.Username);
            var resetLink = Url.Action("ResetPassword", "Account", new { token = resetToken }, Request.Scheme)
                ?? string.Empty;

            var values = new Dictionary<string, string>
            {
                ["{{FirstName}}"] = user.FirstName,
                ["{{LastName}}"] = user.LastName,
                ["{{Email}}"] = user.Username,
                ["{{ResetPasswordLink}}"] = resetLink,
                ["{{ExpiryMinutes}}"] = _tokenService.PasswordResetMinutes.ToString(),
                ["{{CurrentDate}}"] = DateTime.UtcNow.ToLocalDisplay().ToString("MM-dd-yyyy"),
            };

            var subject = EmailPlaceholders.FillWithValues(template.Subject, values);
            var body = EmailPlaceholders.FillWithValues(template.Body, values);

            await _emailSenderService.SendAsync(user.Username, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            // Mirrors UsersController.SendRegistrationEmailAsync - an SMTP failure here must
            // never change what the Forgot Password page tells the (anonymous, unauthenticated)
            // visitor, since surfacing mail-server details to that page would be an information
            // disclosure risk. Logged for diagnosis on the server side instead.
            _logger.LogError(ex, "Failed to send the password reset email for user {UserId}.", user.Id);
        }
    }
}
