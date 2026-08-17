using DCT_SD.Helpers;
using DCT_SD.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

public class HomeController : Controller
{
    // Lands on the first menu (in MenuKeys.All order - RD Configuration first) the current
    // user is allowed to see, mirroring the legacy prototype's firstAllowed lookup.
    public IActionResult Index()
    {
        foreach (var key in MenuKeys.All)
        {
            var isAllowed = User.IsInRole(RoleNames.Administrator) || User.HasClaim(AppClaimTypes.Menu, key);
            if (isAllowed && MenuRoutes.Routes.TryGetValue(key, out var route))
            {
                return Redirect(route);
            }
        }

        return RedirectToAction("AccessDenied", "Account");
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
