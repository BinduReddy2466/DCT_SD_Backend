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

        return baseMenuKeys.Concat(explicitGrantKeys).Distinct().ToArray();
    }
}
