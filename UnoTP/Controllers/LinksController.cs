using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Short URL: the links sent to investors, and the applications that can carry
/// one, from the backend. Sending - or regenerating - a link is the backend's to
/// do; the post says what came of it once, on the page it redirects to.
/// </summary>
[RequiresFeature("short-url")]
[Route("ShortUrl")]
public class LinksController(ILinkApi links, Lookups lookups, IApplicationApi applications, PaymentLinkSender paymentLinks) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Toast"] = TempData["toast"];
        return View(new LinksViewModel(await links.SentAsync(), await links.PendingAsync(), await lookups.ConfigAsync()));
    }

    /// <summary>Sends a link, replacing any sent before, which stops working.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(string appNo, string purpose)
    {
        // An application submitted with Try later has no payment link yet: its first
        // is made here, shortened and put on record, before it is listed as sent.
        if (purpose == "payment" && (await applications.FindAsync(appNo))?.Submitted is { LinkSent: false })
        {
            var (sent, _) = await paymentLinks.SendAsync(appNo, HttpContext.RequestAborted);
            TempData["toast"] = sent is null
                ? Messages.ShortUrl.NoLinkSent
                : $"Link sent to {SentTo.Both(sent.LinkSentTo, sent.LinkEmailedTo)}.";
            return RedirectToAction(nameof(Index));
        }
        TempData["toast"] = await links.SendAsync(appNo, purpose) is { } link
            ? $"Link sent to {SentTo.Both(link.Mobile, link.Email)} — any link sent before stops working."
            : Messages.ShortUrl.NoLinkSent;
        return RedirectToAction(nameof(Index));
    }
}
