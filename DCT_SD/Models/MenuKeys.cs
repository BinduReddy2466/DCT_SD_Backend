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
