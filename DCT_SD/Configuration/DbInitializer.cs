using DCT_SD.Helpers;
using DCT_SD.Models;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Configuration;

// Bootstraps the very first Administrator account. Intentionally NOT baked into a migration,
// so no password (hashed or otherwise) ever lands in source control. Only runs when the Users
// table is empty, and only reads credentials from configuration (user secrets / environment
// variables) - never hardcoded. In Production, provision the first admin out-of-band instead.
public static class DbInitializer
{
    public static async Task SeedInitialAdministratorAsync(ApplicationDbContext context, string username, string password, ILogger logger)
    {
        if (await context.Users.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Administrator);
        if (adminRole is null)
        {
            logger.LogWarning("Administrator role not found; skipping initial admin seed. Apply migrations first.");
            return;
        }

        context.Users.Add(new User
        {
            FirstName = "System",
            LastName = "Administrator",
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            RoleId = adminRole.Id,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();
        logger.LogWarning("Seeded initial Administrator account '{Username}'. Log in and change the password immediately.", username);
    }

    public static async Task SeedDefaultSettingsAsync(ApplicationDbContext context)
    {
        if (!await context.SessionSettings.AnyAsync())
        {
            context.SessionSettings.Add(new SessionSetting { TimeoutMinutes = 15, Action = SessionTimeoutAction.Lock });
        }

        if (!await context.BrandingSettings.AnyAsync())
        {
            context.BrandingSettings.Add(new BrandingSetting());
        }

        if (!await context.EmailTemplates.AnyAsync())
        {
            context.EmailTemplates.AddRange(DefaultEmailTemplates.All.Select(t => new EmailTemplate
            {
                Key = t.Key,
                Label = t.Label,
                Recipients = t.Recipients,
                Subject = t.Subject,
                Body = t.Body,
            }));
        }

        await context.SaveChangesAsync();
    }
}
