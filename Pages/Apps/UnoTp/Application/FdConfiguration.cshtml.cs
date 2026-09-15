using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Application;

public class FdConfigurationModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("/Apps/UnoTp/Application/ReviewSummary");
    }
}
