using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>Where a request over its limit is sent (see <see cref="RateLimits"/>).</summary>
[AllowWithoutSession]
public class TooManyRequestsModel : PageModel
{
    public IActionResult OnGet()
    {
        Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return Page();
    }
}
