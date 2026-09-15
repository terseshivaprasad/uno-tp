using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Application;

public class UploadDocumentsModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("/Apps/UnoTp/Application/InvestorInfo");
    }
}
