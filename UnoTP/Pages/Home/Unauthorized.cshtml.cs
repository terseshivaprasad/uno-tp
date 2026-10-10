using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>A page that is not the user's to open.</summary>
[AllowWithoutSession]
public class UnauthorizedModel : PageModel
{
    public string Message => Messages.SignIn.NoAccess;

    public IActionResult OnGet()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Page();
    }
}
