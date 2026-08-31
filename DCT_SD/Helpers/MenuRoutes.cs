using DCT_SD.Models;

namespace DCT_SD.Helpers;

// Single source of truth for "which controller route does this menu key open" - shared by
// the sidebar (_Layout.cshtml) and HomeController's post-login landing-page redirect, so the
// two can never disagree about which menus are actually built vs still "coming soon".
public static class MenuRoutes
{
    public static readonly IReadOnlyDictionary<string, string> Routes = new Dictionary<string, string>
    {
        [MenuKeys.RdConfig] = "/RdConfig",
        [MenuKeys.MigrationMonitoring] = "/Migrations",
        [MenuKeys.ManualValidation] = "/ManualValidation",
        [MenuKeys.EmptyFolders] = "/EmptyFolders",
        [MenuKeys.Reports] = "/Reports",
        [MenuKeys.FailedExtraction] = "/FailedExtraction",
        [MenuKeys.UserManagement] = "/Users",
        [MenuKeys.Roles] = "/Roles",
        [MenuKeys.Settings] = "/Settings",
    };
}
