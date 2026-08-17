using DCT_SD.Configuration;
using DCT_SD.Filters;
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
                configuration.GetConnectionString("DefaultConnection"),
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
}
