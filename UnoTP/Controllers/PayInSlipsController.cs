using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Pay In Slip Generation: the applications paying on paper, from the backend.
/// Generating a slip and sending the acceptance link are the backend's to do;
/// each post says what came of it once, on the page it redirects to.
/// </summary>
[RequiresFeature("pis")]
[Route("PayInSlip")]
public class PayInSlipsController(IPayInSlipApi slips, ILinkApi links, Lookups lookups) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Toast"] = TempData["toast"];
        return View(new PayInSlipsViewModel(await slips.SlipsAsync(), (await lookups.ConfigAsync()).CancellationDays));
    }

    /// <summary>Issues the application's slip, or a fresh one for a reprint.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(string appNo)
    {
        TempData["toast"] = await slips.GenerateAsync(appNo) is { } slip
            ? $"Pay-in slip {slip.SlipNo} generated for {slip.Branch}."
            : "No slip could be generated for this application.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sends the investor the link to accept a digital application, which a slip waits for.</summary>
    [HttpPost("acceptance")]
    public async Task<IActionResult> SendAcceptance(string appNo)
    {
        TempData["toast"] = await links.SendAsync(appNo, "acceptance") is { } link
            ? $"Acceptance link sent to {SentTo.Both(link.Mobile, link.Email)}. The slip can be generated once the investor accepts."
            : "The acceptance link could not be sent for this application.";
        return RedirectToAction(nameof(Index));
    }
}
