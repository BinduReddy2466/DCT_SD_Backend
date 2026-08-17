using Microsoft.AspNetCore.Authorization;

namespace DCT_SD.Filters;

public class MenuRequirement : IAuthorizationRequirement
{
    public MenuRequirement(string menuKey)
    {
        MenuKey = menuKey;
    }

    public string MenuKey { get; }
}
