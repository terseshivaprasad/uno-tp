using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>
/// Document utilities: a copy turned into a JPEG, a PDF turned into a JPEG for each
/// of its pages, and two images put together as one. Everything is done in the
/// browser by utilities.js: no file is sent to the server, and nothing is kept.
/// </summary>
[RequiresFeature("utilities")]
public class UtilitiesModel : PageModel
{
    public void OnGet()
    {
    }
}
