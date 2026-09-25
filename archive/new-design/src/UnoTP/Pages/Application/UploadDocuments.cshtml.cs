using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTP.Pages.Application;

public class UploadDocumentsModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("/Application/InvestorInfo");
    }
}
