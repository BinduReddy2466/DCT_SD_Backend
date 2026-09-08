using Microsoft.AspNetCore.Http;

namespace DCT_SD.Helpers;

// Centralizes how the JWT access/refresh tokens are written to and cleared from the browser.
// Both cookies are HttpOnly (unreachable from JavaScript, so an XSS payload cannot read the
// token off document.cookie), SameSite=Strict (never sent on a cross-site navigation/request,
// which also rules out CSRF against these cookies), and Secure whenever the request itself
// arrived over HTTPS - never localStorage/sessionStorage, which any injected script can read
// outright. Secure tracks the live request instead of being hardcoded true so a plain-HTTP
// local/dev deployment (no TLS termination in front of Kestrel) still gets a cookie the
// browser will actually store; behind IIS/HTTPS in production, IsHttps is true and the cookie
// stays HTTPS-only exactly as before.
public static class AuthCookieHelper
{
    public const string AccessTokenCookieName = "access_token";
    public const string RefreshTokenCookieName = "refresh_token";

    public static void SetAccessTokenCookie(HttpResponse response, string token, DateTime expiresAtUtc)
    {
        response.Cookies.Append(AccessTokenCookieName, token, BuildOptions(response, expiresAtUtc));
    }

    public static void SetRefreshTokenCookie(HttpResponse response, string token, DateTime expiresAtUtc)
    {
        response.Cookies.Append(RefreshTokenCookieName, token, BuildOptions(response, expiresAtUtc));
    }

    public static void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenCookieName);
        response.Cookies.Delete(RefreshTokenCookieName);
    }

    private static CookieOptions BuildOptions(HttpResponse response, DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = response.HttpContext.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Expires = expiresAtUtc,
        Path = "/",
    };
}
