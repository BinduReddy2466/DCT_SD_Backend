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
