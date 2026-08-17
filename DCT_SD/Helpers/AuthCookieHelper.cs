using Microsoft.AspNetCore.Http;

namespace DCT_SD.Helpers;

// Centralizes how the JWT access/refresh tokens are written to and cleared from the browser.
// Both cookies are HttpOnly (unreachable from JavaScript, so an XSS payload cannot read the
// token off document.cookie), Secure (HTTPS-only transport), and SameSite=Strict (never sent
// on a cross-site navigation/request, which also rules out CSRF against these cookies) -
// never localStorage/sessionStorage, which any injected script can read outright.
public static class AuthCookieHelper
{
    public const string AccessTokenCookieName = "access_token";
    public const string RefreshTokenCookieName = "refresh_token";

    public static void SetAccessTokenCookie(HttpResponse response, string token, DateTime expiresAtUtc)
    {
        response.Cookies.Append(AccessTokenCookieName, token, BuildOptions(expiresAtUtc));
    }

    public static void SetRefreshTokenCookie(HttpResponse response, string token, DateTime expiresAtUtc)
    {
        response.Cookies.Append(RefreshTokenCookieName, token, BuildOptions(expiresAtUtc));
    }

    public static void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenCookieName);
        response.Cookies.Delete(RefreshTokenCookieName);
    }

    private static CookieOptions BuildOptions(DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = expiresAtUtc,
        Path = "/",
    };
}
