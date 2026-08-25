using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
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

    public AccountController(IAuthService authService, ITokenService tokenService, ISettingsService settingsService)
    {
        _authService = authService;
        _tokenService = tokenService;
        _settingsService = settingsService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var branding = await _settingsService.GetBrandingAsync(cancellationToken);
        ViewData["LoginBackgroundUrl"] = branding.ImageUrl;

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
}
