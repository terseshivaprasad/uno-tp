using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>Where a page goes when nobody has come in from the portal, or their session has ended.</summary>
[AllowWithoutSession]
public class SessionExpiredModel : PageModel
{
    public IActionResult OnGet()
    {
        HttpContext.Session.Clear();
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Page();
    }
}
