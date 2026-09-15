using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Application;

public class ReviewSummaryModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPostSend()
    {
        // 11A's "Send payment link" action inside the modal moves on to the
        // submitted / payment-pending confirmation (11B).
        return RedirectToPage("/Apps/UnoTp/Application/Submitted");
    }
}
