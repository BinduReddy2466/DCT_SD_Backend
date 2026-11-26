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
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmptyFolderService, EmptyFolderService>();
        services.AddScoped<IRdConfigService, RdConfigService>();
        services.AddScoped<IRemoteFolderBrowserService, RemoteFolderBrowserService>();
        services.AddScoped<IRegistryOfficeService, RegistryOfficeService>();
        services.AddScoped<IDocumentTypeService, DocumentTypeService>();
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddScoped<IManualValidationService, ManualValidationService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IFailedExtractionService, FailedExtractionService>();
        services.AddScoped<IEmailSenderService, EmailSenderService>();

        // Backs the Add/Edit User "Save" actions' notification emails - see UsersController's
        // EnqueueRegistrationEmail/EnqueueStatusChangeEmail. Singleton queue + a hosted service
        // that drains it in its own DI scope, so the controller's response returns immediately
        // after the DB write instead of waiting on a real SMTP round-trip.
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedTaskHostedService>();

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
            // Bounded, not Timeout.InfiniteTimeSpan: HttpClient.Timeout only bounds the whole
            // request+body-read when using the default HttpCompletionOption.ResponseContentRead.
            // POST /fetch/start (StartFetchStreamAsync) explicitly uses ResponseHeadersRead, so
            // this timeout only covers getting the initial response/headers for that call - the
            // actual long-running SSE body read afterward is governed entirely by the controller's
            // own CancellationToken (HttpContext.RequestAborted), same as before, completely
            // unaffected by this value. What this DOES fix: every other call on this same typed
            // client (GetFetchRunDetailsAsync, UpdateRootPathAsync, ReprocessFailedExtractionAsync,
            // MergeEntryAsync) reads its response normally (no ResponseHeadersRead), so with
            // InfiniteTimeSpan those calls had no HttpClient-level cutoff at all - if the external
            // service accepted the connection but then hung before responding, they would await
            // indefinitely (notably GetFetchRunDetailsAsync's post-stream reconciliation call in
            // RdConfigController, which intentionally uses CancellationToken.None so it still
            // completes even if the browser disconnects, and so has no other cutoff either).
            client.Timeout = TimeSpan.FromSeconds(60);
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

        // The second external HTTP dependency in this app - PHILARIS-RD's SD Auto Conversion
        // service (multipart/form-data, X-API-Key header auth). Base URL is config
        // (SDAutoConversion:BaseUrl), never hardcoded. The API key lives only in user secrets /
        // the SDAutoConversion__ApiKey environment variable - never committed, same convention as
        // RdFetchApi's credentials and the DB encryption key above.
        var sdAutoConversionBaseUrl = configuration["SDAutoConversion:BaseUrl"]
            ?? throw new InvalidOperationException("SDAutoConversion:BaseUrl is not configured.");
        var sdAutoConversionApiKey = configuration["SDAutoConversion:ApiKey"]
            ?? throw new InvalidOperationException("SDAutoConversion:ApiKey is missing. Set it via the SDAutoConversion__ApiKey environment variable or user secrets.");

        services.AddHttpClient<ISdAutoConversionClient, SdAutoConversionClient>(client =>
        {
            client.BaseAddress = new Uri(sdAutoConversionBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // multipart uploads of several supporting-document images - allow more time than the JSON-only RdFetchApi client
            client.DefaultRequestHeaders.Add("X-API-Key", sdAutoConversionApiKey);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
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

    // ConnectionStrings:DefaultConnection in appsettings.json can hold either a plain connection
    // string, or one whose Server/Database/User Id/Password values are individually AES-GCM
    // ciphertext (safe to commit that way) decrypted here using a key that lives only in an
    // environment variable or user secrets (ConfigProtection:Key / ConfigProtection__Key), never
    // in source control. ConfigProtection:Key is optional: when it's not set, the connection
    // string is used exactly as written in appsettings.json (assumed already plain), for
    // environments that don't want the encryption step at all.
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var key = configuration["ConfigProtection:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            return connectionString;
        }

        return ConfigProtector.DecryptConnectionString(connectionString, key);
    }
}
