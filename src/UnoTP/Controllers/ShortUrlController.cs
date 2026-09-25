using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Short URL: the links sent to investors, and the applications that can carry
/// one, from the backend. Sending - or regenerating - a link is the backend's to
/// do; the post says what came of it once, on the page it redirects to.
/// </summary>
[RequiresFeature("short-url")]
[Route("Apps/UnoTp/Classic/ShortUrl")]
public class ShortUrlController(ILinkApi links, Lookups lookups) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Toast"] = TempData["toast"];
        return View(new ShortUrlViewModel(await links.SentAsync(), await links.PendingAsync(), await lookups.ConfigAsync()));
    }

    /// <summary>Sends a link, replacing any sent before, which stops working.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(string appNo, string purpose)
    {
        TempData["toast"] = await links.SendAsync(appNo, purpose) is { } link
            ? $"Link sent to {link.Contact} — any link sent before stops working."
            : "No link could be sent for this application.";
        return RedirectToAction(nameof(Index));
    }
}
