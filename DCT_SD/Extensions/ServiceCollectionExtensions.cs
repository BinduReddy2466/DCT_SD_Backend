using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using DCT_SD.Configuration;
using DCT_SD.Filters;
using DCT_SD.Helpers;
using DCT_SD.Models;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDctServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                ResolveConnectionString(configuration),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmptyFolderService, EmptyFolderService>();
        services.AddScoped<IRdConfigService, RdConfigService>();
        services.AddScoped<IRegistryOfficeService, RegistryOfficeService>();
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddScoped<IManualValidationService, ManualValidationService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IFailedExtractionService, FailedExtractionService>();

        // The one external HTTP dependency in this app - a separate RD/fetch service, reachable
        // only over Paradigm WiFi/VPN. Base URL is config (RdFetchApi:BaseUrl), never hardcoded.
        // Username/Password authenticate every request via HTTP Basic Auth and, like the JWT
        // signing key and DB encryption key, live only in user secrets / the
        // RdFetchApi__Username / RdFetchApi__Password environment variables - never committed.
        var rdFetchApiBaseUrl = configuration["RdFetchApi:BaseUrl"]
            ?? throw new InvalidOperationException("RdFetchApi:BaseUrl is not configured.");
        // var rdFetchApiUsername = configuration["RdFetchApi:Username"]
        //     ?? throw new InvalidOperationException("RdFetchApi:Username is missing. Set it via the RdFetchApi__Username environment variable or user secrets.");
        // var rdFetchApiPassword = configuration["RdFetchApi:Password"]
        //     ?? throw new InvalidOperationException("RdFetchApi:Password is missing. Set it via the RdFetchApi__Password environment variable or user secrets.");
        // var rdFetchApiAuthHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rdFetchApiUsername}:{rdFetchApiPassword}"));

        services.AddHttpClient<IRdFetchApiClient, RdFetchApiClient>(client =>
        {
            client.BaseAddress = new Uri(rdFetchApiBaseUrl);
            // No fixed HttpClient-level timeout: POST /fetch/start returns a long-running SSE
            // stream (a full fetch run can take far longer than any reasonable fixed cap), and
            // HttpClient.Timeout bounds the whole request+body-read, not just headers. Each call
            // instead gets bounded by the caller's own CancellationToken (ultimately the
            // browser's request, via HttpContext.RequestAborted).
            client.Timeout = Timeout.InfiniteTimeSpan;
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", rdFetchApiAuthHeader);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // ConnectTimeout is separate from HttpClient.Timeout above - it bounds only the TCP
            // connect phase, not the (deliberately unbounded) body read. Without this, a
            // misconfigured host or a silently unreachable address (packets dropped, no RST/ICMP
            // response - e.g. no network route to it at all) would hang the request indefinitely
            // instead of failing fast with the normal "could not reach the fetch service" error.
            ConnectTimeout = TimeSpan.FromSeconds(15),
        });

        services.AddSingleton<IAuthorizationHandler, MenuAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            // Complete mediation: every endpoint requires an authenticated principal unless it
            // opts out with [AllowAnonymous] (Account/Login, Account/AccessDenied, Home/Error).
            // Menu-specific policies layer the claims check on top of this baseline.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var menuKey in MenuKeys.All)
            {
                options.AddPolicy($"Menu:{menuKey}", policy => policy.Requirements.Add(new MenuRequirement(menuKey)));
            }
        });

        return services;
    }

    // ConnectionStrings:DefaultConnection in appsettings.json keeps its normal "Key=Value;..."
    // shape and is safe to commit: the Server/Database/User Id/Password values are individually
    // AES-GCM ciphertext, decrypted here using a key that lives only in an environment variable
    // or user secrets (ConfigProtection:Key / ConfigProtection__Key), never in source control.
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var key = configuration["ConfigProtection:Key"]
            ?? throw new InvalidOperationException("ConfigProtection:Key is missing. Set it via the ConfigProtection__Key environment variable or user secrets.");

        return ConfigProtector.DecryptConnectionString(connectionString, key);
    }
}
