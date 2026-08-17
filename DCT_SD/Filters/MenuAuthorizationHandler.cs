using DCT_SD.Models;
using Microsoft.AspNetCore.Authorization;

namespace DCT_SD.Filters;

public class MenuAuthorizationHandler : AuthorizationHandler<MenuRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MenuRequirement requirement)
    {
        if (context.User.IsInRole(RoleNames.Administrator))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasMenuClaim = context.User.Claims.Any(c =>
            c.Type == AppClaimTypes.Menu && c.Value == requirement.MenuKey);

        if (hasMenuClaim)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
