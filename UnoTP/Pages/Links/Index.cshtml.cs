using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// Short URL: the links sent to investors, and the applications that can carry
/// one, from the backend. Sending - or regenerating - a link is the backend's to
/// do; the post says what came of it once, on the page it redirects to.
/// </summary>
[RequiresFeature("short-url")]
public class LinksModel(ILinkApi links, Lookups lookups, IApplicationApi applications, PaymentLinkSender paymentLinks) : PageModel
{
    /// <summary>What the page shows.</summary>
    public LinksViewModel View { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Toast"] = TempData["toast"];
        View = new LinksViewModel(await links.SentAsync(), await links.PendingAsync(), await lookups.ConfigAsync());
        return Page();
    }

    /// <summary>
    /// Send link, or Resend: an application with no link yet gets its first; one with a link
    /// is sent the same short link again, open for 24 hours.
    /// </summary>
    public Task<IActionResult> OnPostSendAsync(string appNo, string purpose) => SendAsync(appNo, purpose, newLink: false);

    /// <summary>New link: the application's old link goes out of use, and a fresh one is made and sent.</summary>
    public Task<IActionResult> OnPostNewLinkAsync(string appNo, string purpose) => SendAsync(appNo, purpose, newLink: true);

    private async Task<IActionResult> SendAsync(string appNo, string purpose, bool newLink)
    {
        // A payment link is a page the investor pays on: it is made here, shortened and
        // put on record. The first one, for an application submitted with Try later;
        // or a new one, in place of the one it had.
        var noLinkYet = (await applications.FindAsync(appNo))?.Submitted is { LinkSent: false };
        if (purpose == "payment" && (noLinkYet || newLink))
        {
            var (sending, _) = await paymentLinks.SendAsync(appNo, LinkPage.ShortUrl, HttpContext.RequestAborted);
            TempData["toast"] = sending.Sent is { } first
                ? $"Link sent to {SentTo.Both(first.LinkSentTo, first.LinkEmailedTo)}."
                : sending.Refused ?? Messages.ShortUrl.NoLinkSent;
            return RedirectToPage();
        }
        var sent = await links.SendAsync(appNo, purpose, LinkPage.ShortUrl, newLink);
        TempData["toast"] = sent.Link is { } link
            ? $"Link sent to {SentTo.Both(link.Mobile, link.Email)}."
            : sent.Refused ?? Messages.ShortUrl.NoLinkSent;
        return RedirectToPage();
    }
}
