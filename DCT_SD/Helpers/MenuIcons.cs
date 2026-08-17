using DCT_SD.Models;

namespace DCT_SD.Helpers;

// Sidebar nav icons, ported verbatim (same viewBox/paths) from the legacy HTML prototype's
// sidebar markup, keyed by MenuKeys value. Kept as raw SVG strings rather than a Tag Helper
// since they're only ever rendered inline inside _Layout.cshtml's nav loop.
public static class MenuIcons
{
    private const string Prefix = "<svg class=\"nav-icon\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" viewBox=\"0 0 24 24\">";
    private const string Suffix = "</svg>";

    private static readonly Dictionary<string, string> Paths = new()
    {
        [MenuKeys.RdConfig] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-19.5 0v6a2.25 2.25 0 002.25 2.25h15a2.25 2.25 0 002.25-2.25v-6m-19.5 0h19.5M6.75 6h.008M2.25 9.75V6.108c0-1.135.845-2.098 1.976-2.192.373-.03.748-.057 1.123-.08M11.25 2.25v.75c0 .414.336.75.75.75h1.5a.75.75 0 00.75-.75v-.75m-4.5 0h4.5\"/>",
        [MenuKeys.MigrationMonitoring] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M8 7h12m0 0l-4-4m4 4l-4 4M16 17H4m0 0l4 4m-4-4l4-4\"/>",
        [MenuKeys.ManualValidation] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M9 12h3.75M9 15h3.75M9 18h3.75M3.75 4.5h16.5M3.75 4.5v15A2.25 2.25 0 006 21.75h12A2.25 2.25 0 0020.25 19.5v-15M3.75 4.5L6 2.25h12l2.25 2.25\"/>",
        [MenuKeys.EmptyFolders] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M2.25 9.776c.112-.017.227-.026.344-.026h18.812c.117 0 .232.009.344.026m-19.5 0a2.25 2.25 0 00-1.883 2.542l.857 6a2.25 2.25 0 002.227 1.932H19.06a2.25 2.25 0 002.227-1.932l.857-6a2.25 2.25 0 00-1.883-2.542m-19.5 0V6.108c0-1.135.845-2.098 1.976-2.192a48.424 48.424 0 011.876-.113m14.36 2.33V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 00-1.876-.113\"/>",
        [MenuKeys.UserManagement] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M18 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2\"/><circle cx=\"9\" cy=\"7\" r=\"3\"/><path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M23 21v-2a4 4 0 00-3-3.87\"/><path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M16 3.13a4 4 0 010 7.75\"/>",
        [MenuKeys.Roles] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M12 2L4 5v6c0 5.25 3.44 10.74 8 12 4.56-1.26 8-6.75 8-12V5l-8-3z\"/><path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M9 12l2 2 4-4\"/>",
        [MenuKeys.Settings] = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
    };

    public static string Svg(string menuKey) => Prefix + Paths.GetValueOrDefault(menuKey, string.Empty) + Suffix;
}
