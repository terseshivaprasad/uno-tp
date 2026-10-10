using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>Ends the session here and goes to the portal's logout page, which signs the user out there.</summary>
[AllowWithoutSession]
public class LogOutModel(AppUrls apps, IOptions<PortalOptions> portal) : PageModel
{
    public IActionResult OnGet()
    {
        HttpContext.Session.Clear();
        var logout = portal.Value.Logout;
        if (logout.Length == 0) logout = apps.Url("/");
        return Redirect(logout);
    }
}
