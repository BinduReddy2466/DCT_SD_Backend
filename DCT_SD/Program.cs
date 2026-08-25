using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DCT_SD.Configuration;
using DCT_SD.Extensions;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDctServices(builder.Configuration);
builder.Services.AddControllersWithViews();

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DCT_SD";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DCT_SD";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            // Complete mediation: signature AND expiration are both checked on every request
            // that reaches an [Authorize]'d action - this is enforced by the framework itself,
            // not by any per-action code - and ClockSkew=Zero means "expired" means expired,
            // with no grace window.
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        // The access token travels only as an HttpOnly cookie, never a bearer header, so pull
        // it from there. If the silent-refresh middleware below just minted a fresh token for
        // this same request, prefer that over the (now stale) cookie value already on the
        // incoming request.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.HttpContext.Items[AuthCookieHelper.AccessTokenCookieName] as string
                    ?? context.Request.Cookies[AuthCookieHelper.AccessTokenCookieName];
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                context.Response.Redirect("/Account/AccessDenied");
                return Task.CompletedTask;
            },
        };
    });

var app = builder.Build();

// When hosted behind IIS (the standard ASP.NET Core Module V2 topology, or IIS acting as a
// reverse proxy in front of Kestrel), IIS is the one terminating TLS; without this, the app
// itself sees every request as plain HTTP, which breaks UseHttpsRedirection()/HSTS scheme
// detection and - more importantly for the JWT cookie flow - has nothing to do with whether the
// Secure cookie itself gets set (the browser only cares that the browser<->IIS hop is HTTPS),
// but does matter for any of the app's own scheme-dependent logic. Must run before anything
// else that inspects Request.Scheme/IsHttps. No effect in local `dotnet run` (no forwarded
// headers present), so this is safe to add unconditionally.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var seedUsername = builder.Configuration["SeedAdmin:Username"];
    var seedPassword = builder.Configuration["SeedAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(seedUsername) && !string.IsNullOrWhiteSpace(seedPassword))
    {
        await DbInitializer.SeedInitialAdministratorAsync(context, seedUsername, seedPassword, logger);
    }
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

using (var settingsScope = app.Services.CreateScope())
{
    await DbInitializer.SeedDefaultSettingsAsync(settingsScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Silent refresh: if the access token cookie is missing/expired but a still-valid refresh
// token cookie is present, rotate it and mint a fresh access token before authentication runs,
// so this same request (and every one after it) authenticates without the user ever having to
// log in again mid-session. Runs before UseAuthentication so the fresh token is what gets read.
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Cookies[AuthCookieHelper.AccessTokenCookieName];
    var refreshToken = context.Request.Cookies[AuthCookieHelper.RefreshTokenCookieName];

    if (!string.IsNullOrEmpty(refreshToken) && IsMissingOrExpired(accessToken))
    {
        var authService = context.RequestServices.GetRequiredService<IAuthService>();
        var tokenService = context.RequestServices.GetRequiredService<ITokenService>();

        try
        {
            var rotated = await authService.RotateRefreshTokenAsync(refreshToken, context.Connection.RemoteIpAddress?.ToString());
            var (newAccessToken, newAccessExpiresAt) = tokenService.CreateAccessToken(
                rotated.User.Id, rotated.User.Username, rotated.User.RoleName, rotated.AllowedMenus);

            AuthCookieHelper.SetAccessTokenCookie(context.Response, newAccessToken, newAccessExpiresAt);
            AuthCookieHelper.SetRefreshTokenCookie(context.Response, rotated.NewRawToken, rotated.NewExpiresAtUtc);
            context.Items[AuthCookieHelper.AccessTokenCookieName] = newAccessToken;
        }
        catch (UnauthorizedAppException)
        {
            AuthCookieHelper.ClearAuthCookies(context.Response);
        }
        catch (Exception ex)
        {
            // A transient failure (DB connectivity blip, etc.) is not proof the session is
            // invalid - clearing the cookies here would log the user out for an infrastructure
            // hiccup unrelated to their credentials. Leave the cookies as they are: this
            // request falls through to normal JWT validation (which will reject the expired
            // access token and challenge to Login), but the still-valid refresh token cookie
            // survives so the very next attempt can succeed without a fresh login.
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Silent token refresh failed unexpectedly; leaving session cookies untouched.");
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static bool IsMissingOrExpired(string? accessToken)
{
    if (string.IsNullOrEmpty(accessToken))
    {
        return true;
    }

    try
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.ValidTo <= DateTime.UtcNow;
    }
    catch
    {
        return true;
    }
}
