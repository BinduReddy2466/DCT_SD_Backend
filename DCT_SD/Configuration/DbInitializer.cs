using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedInitialAdministratorAsync(ApplicationDbContext context, string username, string password, ILogger logger)
    {
        if (await context.Users.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        context.Users.Add(new User
        {
            FirstName = "System",
            LastName = "Administrator",
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            RoleName = RoleNames.Administrator,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();
        logger.LogWarning("Seeded initial Administrator account '{Username}'. Log in and change the password immediately.", username);
    }

    public static async Task SeedDefaultSettingsAsync(ApplicationDbContext context)
    {
        if (!await context.AppSettings.AnyAsync(s => s.Category == AppSettingCategories.Session))
        {
            context.AppSettings.Add(new AppSetting
            {
                Category = AppSettingCategories.Session,
                Label = "Session Timeout Policy",
                DataJson = JsonSerializer.Serialize(new { timeoutMinutes = 15, action = (int)SessionTimeoutAction.Lock }, JsonOptions),
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Category == AppSettingCategories.Branding))
        {
            context.AppSettings.Add(new AppSetting
            {
                Category = AppSettingCategories.Branding,
                Label = "Application Branding",
                DataJson = JsonSerializer.Serialize(new { imagePath = (string?)null }, JsonOptions),
            });
        }

        // Per-KEY, not per-category - EmailTemplate (unlike Session/Branding above) holds several
        // independent rows, one per template Key. A category-wide check would seed every default
        // template only on a truly empty table and then never again, so a template added to
        // DefaultEmailTemplates.All later (e.g. user_auto_locked) would never reach an
        // already-provisioned database - each key is ensured independently instead.
        var existingTemplateKeys = await context.AppSettings
            .Where(s => s.Category == AppSettingCategories.EmailTemplate)
            .Select(s => s.Key)
            .ToListAsync();

        foreach (var t in DefaultEmailTemplates.All)
        {
            if (existingTemplateKeys.Contains(t.Key))
            {
                continue;
            }

            context.AppSettings.Add(new AppSetting
            {
                Category = AppSettingCategories.EmailTemplate,
                Key = t.Key,
                Label = t.Label,
                DataJson = JsonSerializer.Serialize(new { recipients = t.Recipients, subject = t.Subject, body = t.Body }, JsonOptions),
            });
        }

        await context.SaveChangesAsync();
    }
}
