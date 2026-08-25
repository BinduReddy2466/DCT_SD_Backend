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
        new() { Id = 1, Key = MenuKeys.RdConfig, Label = "RD Configuration", IsBaseMenu = true },
        new() { Id = 2, Key = MenuKeys.MigrationMonitoring, Label = "Migration Monitoring", IsBaseMenu = true },
        new() { Id = 4, Key = MenuKeys.ManualValidation, Label = "Manual Validation", IsBaseMenu = true },
        new() { Id = 5, Key = MenuKeys.EmptyFolders, Label = "Empty Entry Folders", IsBaseMenu = true },
        new() { Id = 6, Key = MenuKeys.UserManagement, Label = "User Management", IsBaseMenu = false },
        new() { Id = 7, Key = MenuKeys.Roles, Label = "Roles", IsBaseMenu = false },
        new() { Id = 8, Key = MenuKeys.Settings, Label = "Settings", IsBaseMenu = false },
        new() { Id = 9, Key = MenuKeys.DctSd, Label = "DCT_SD", IsBaseMenu = false },
    ];

    // Base menus (the individual DCT_SD pages) are never individually assignable - a Sub-Admin
    // gets all of them at once, or none, via the single "DCT_SD" parent toggle (see
    // AllowedMenuResolver). So the Assign Tab picker's list, its pre-checked state, and its
    // "N selected" count all stay scoped to this same set: DCT_SD plus the 3 restricted pages.
    private static readonly MenuDto[] AssignableMenus = FixedMenus.Where(m => !m.IsBaseMenu).ToArray();

    public Task<IReadOnlyList<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MenuDto>>(AssignableMenus);

    public IReadOnlyList<string> ResolveKeys(IEnumerable<int> menuIds) =>
        AssignableMenus.Where(m => menuIds.Contains(m.Id)).Select(m => m.Key).ToArray();

    public IReadOnlyList<int> ResolveIds(IEnumerable<string> menuKeys) =>
        AssignableMenus.Where(m => menuKeys.Contains(m.Key)).Select(m => m.Id).ToArray();
}
