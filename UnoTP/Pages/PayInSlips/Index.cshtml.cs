using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// Pay In Slip Generation: the applications paying on paper, from the backend.
/// Generating a slip and sending the acceptance link are the backend's to do;
/// each post says what came of it once, on the page it redirects to.
/// </summary>
[RequiresFeature("pis")]
public class PayInSlipsModel(IPayInSlipApi slips, ILinkApi links, Lookups lookups) : PageModel
{
    /// <summary>What the page shows.</summary>
    public PayInSlipsViewModel View { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Toast"] = TempData["toast"];
        View = new PayInSlipsViewModel(await slips.SlipsAsync(), (await lookups.ConfigAsync()).CancellationDays);
        return Page();
    }

    /// <summary>Issues the application's slip, or a fresh one for a reprint.</summary>
    public async Task<IActionResult> OnPostGenerateAsync(string appNo)
    {
        TempData["toast"] = await slips.GenerateAsync(appNo) is { } slip
            ? $"Pay-in slip {slip.SlipNo} generated for {slip.Branch}."
            : Messages.Lists.NoSlip;
        return RedirectToPage();
    }

    /// <summary>Sends the investor the link to accept a digital application, which a slip waits for.</summary>
    public async Task<IActionResult> OnPostSendAcceptanceAsync(string appNo)
    {
        TempData["toast"] = await links.SendAsync(appNo, "acceptance") is { } link
            ? $"Acceptance link sent to {SentTo.Both(link.Mobile, link.Email)}. The slip can be generated once the investor accepts."
            : Messages.Lists.AcceptanceLinkNotSent;
        return RedirectToPage();
    }
}
