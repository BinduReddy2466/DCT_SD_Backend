using DCT_SD.Models;
using DCT_SD.Models.Dtos.Menus;

namespace DCT_SD.Services;

// PROVISIONAL: the new DCT_SD schema has no Menus table - Users.MenuPermissionsCsv stores
// menu keys directly as a CSV string. This exposes the same fixed menu list the old
// MenuConfiguration.HasData seed used (same ids, keys, labels, base/restricted split) so the
// User form's menu picker and Id<->Key conversion keep working without a backing table.
public class MenuService : IMenuService
{
    private static readonly MenuDto[] FixedMenus =
    [
        new() { Id = 1, Key = MenuKeys.RdConfig, Label = "Fetching Management", IsBaseMenu = true },
        new() { Id = 2, Key = MenuKeys.MigrationMonitoring, Label = "Migration Monitoring", IsBaseMenu = true },
        new() { Id = 4, Key = MenuKeys.ManualValidation, Label = "Manual Validation", IsBaseMenu = true },
        new() { Id = 12, Key = MenuKeys.Dashboard, Label = "Dashboard", IsBaseMenu = true },
        new() { Id = 5, Key = MenuKeys.EmptyFolders, Label = "Empty Folders", IsBaseMenu = true },
        new() { Id = 10, Key = MenuKeys.Reports, Label = "Reports", IsBaseMenu = true },
        new() { Id = 11, Key = MenuKeys.FailedExtraction, Label = "Failed Extraction", IsBaseMenu = true },
        new() { Id = 6, Key = MenuKeys.UserManagement, Label = "User Management", IsBaseMenu = false },
        new() { Id = 7, Key = MenuKeys.Roles, Label = "Roles", IsBaseMenu = false },
        new() { Id = 8, Key = MenuKeys.Settings, Label = "Settings", IsBaseMenu = false },
        new() { Id = 9, Key = MenuKeys.DctSd, Label = "DCT_SD", IsBaseMenu = false },
    ];

    // The Assign Tab picker's list, its pre-checked state, and its "N selected" count all stay
    // scoped to this same set: the individual DCT_SD base menus, each grantable on its own -
    // User Management/Roles/Settings are never offered here. AllowedMenuResolver's separate
    // "dct-sd" all-or-nothing bundle key stays supported at login time (unchanged) purely so any
    // account that already has it saved keeps its existing access; it's just no longer selectable
    // from this picker going forward.
    private static readonly MenuDto[] AssignableMenus = FixedMenus.Where(m => m.IsBaseMenu).ToArray();

    public Task<IReadOnlyList<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MenuDto>>(AssignableMenus);

    public IReadOnlyList<string> ResolveKeys(IEnumerable<int> menuIds) =>
        AssignableMenus.Where(m => menuIds.Contains(m.Id)).Select(m => m.Key).ToArray();

    public IReadOnlyList<int> ResolveIds(IEnumerable<string> menuKeys) =>
        AssignableMenus.Where(m => menuKeys.Contains(m.Key)).Select(m => m.Id).ToArray();
}
