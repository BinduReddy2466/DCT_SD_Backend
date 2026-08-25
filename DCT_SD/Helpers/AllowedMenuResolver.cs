using DCT_SD.Models;

namespace DCT_SD.Helpers;

public static class AllowedMenuResolver
{
    public static IReadOnlyList<string> Resolve(string roleName, IEnumerable<string> baseMenuKeys, IEnumerable<string> explicitGrantKeys)
    {
        if (roleName == RoleNames.Administrator)
        {
            return baseMenuKeys.Concat(MenuKeys.RestrictedMenus).Distinct().ToArray();
        }

        if (roleName == RoleNames.SubAdmin)
        {
            // Unlike every other role, a Sub-Admin does not get the DCT_SD pages
            // (baseMenuKeys) automatically. "DCT_SD" is a parent-level Assign Tab permission:
            // granting it unlocks all of RD Configuration/Migration Monitoring/Manual
            // Validation/Empty Entry Folders as one bundle; leaving it unchecked hides and
            // blocks all four. The 3 restricted pages remain individually grantable as before.
            var grants = explicitGrantKeys.ToHashSet();
            var allowed = grants.Where(k => k != MenuKeys.DctSd);
            if (grants.Contains(MenuKeys.DctSd))
            {
                allowed = allowed.Concat(baseMenuKeys);
            }

            return allowed.Distinct().ToArray();
        }

        return baseMenuKeys.Concat(explicitGrantKeys).Distinct().ToArray();
    }
}
