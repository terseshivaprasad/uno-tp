using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Application;

public class InvestorInfoModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("/Apps/UnoTp/Application/BankDetails");
    }
}
