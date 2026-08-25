namespace DCT_SD.Models;

public static class MenuKeys
{
    public const string RdConfig = "rd-config";
    public const string MigrationMonitoring = "migration-monitoring";
    public const string ManualValidation = "manual-validation";
    public const string EmptyFolders = "empty-folders";
    public const string UserManagement = "user-management";
    public const string Roles = "roles";
    public const string Settings = "settings";

    // Not a real page/route - a parent-level Assign Tab permission that, for a Sub-Admin only,
    // gates access to every key in BaseMenus as a single all-or-nothing bundle (see
    // AllowedMenuResolver). Deliberately excluded from BaseMenus/RestrictedMenus/All: those
    // arrays drive sidebar navigation and per-page [Authorize(Policy="Menu:...")] registration,
    // neither of which this key should ever be treated as.
    public const string DctSd = "dct-sd";

    public static readonly string[] BaseMenus =
    {
        RdConfig, MigrationMonitoring, ManualValidation, EmptyFolders
    };

    public static readonly string[] RestrictedMenus =
    {
        UserManagement, Roles, Settings
    };

    public static readonly string[] All = BaseMenus.Concat(RestrictedMenus).ToArray();
}
