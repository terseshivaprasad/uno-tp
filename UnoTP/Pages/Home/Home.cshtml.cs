using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>
/// Back to the portal's dashboard, with the encrypted values the portal sent in, so it
/// knows who is back. Without them it is the plain dashboard address.
/// </summary>
[AllowWithoutSession]
public class PortalHomeModel(AppUrls apps, IOptions<PortalOptions> portal) : PageModel
{
    public IActionResult OnGet()
    {
        var home = portal.Value.Home;
        if (home.Length == 0) home = apps.Url("/Classic");

        var values = HttpContext.Session.PortalValues();
        if (values is null) return Redirect(home);

        var (userId, sysCode) = values.Value;
        var separator = home.Contains('?') ? "&" : "?";
        return Redirect($"{home}{separator}UserId={Uri.EscapeDataString(userId)}&SysCode={Uri.EscapeDataString(sysCode)}");
    }
}
