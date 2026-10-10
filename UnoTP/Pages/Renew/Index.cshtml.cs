using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>
/// Renew FD, the old RenewalDashboard. The investor is found on Investor
/// Identification, as for a new deposit, and their deposits are listed there: this
/// address opens that page. Renew on a deposit that is due opens an application like
/// a new deposit's, which the backend fills with the deposit's holders, repayment
/// account and amount (<see cref="IRenewalApi.StartAsync"/>); from there it goes
/// through the same steps, and the investor accepts it the same way.
/// </summary>
[RequiresFeature("renew")]
public class RenewModel(IRenewalApi renewals) : PageModel
{
    /// <summary>What a post here has to say, on Investor Identification after it.</summary>
    internal const string SaidKey = "said";

    /// <summary>The old address: the search is Investor Identification's.</summary>
    public IActionResult OnGet() => ToSearch();

    /// <summary>Renew: the application opened from the deposit, and the partner taken to its first step.</summary>
    public async Task<IActionResult> OnPostStartAsync(string number)
    {
        if (await renewals.StartAsync(number) is { } app)
        {
            // The search is done with, as when a new deposit's application opens.
            TempData.Remove(NewApplicationModel.SearchKey);
            return RedirectToPage("/Documents/Index", new { appNo = app.AppNo });
        }
        TempData[SaidKey] = Messages.RenewFd.CannotRenewNow(number);
        return ToSearch();
    }

    /// <summary>Cancels the renewal request for a deposit: its application goes, and the deposit is due for renewal again.</summary>
    public async Task<IActionResult> OnPostCancelAsync(string number)
    {
        if (await renewals.CancelAsync(number))
            TempData[SaidKey] = $"The renewal request for deposit {number} is cancelled. It is due for renewal again.";
        else
            TempData[SaidKey] = Messages.RenewFd.NoRenewalToCancel(number);
        return ToSearch();
    }

    // Investor Identification, which holds the search and lists the deposits.
    private RedirectToPageResult ToSearch() => RedirectToPage("/NewApplication/Index");
}
