using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

[RequiresFeature("short-url")]
public class ShortUrlController(ILinkApi links) : Controller
{
    [HttpGet("Apps/UnoTp/Classic/ShortUrl")]
    public async Task<IActionResult> Index() =>
        View(new ShortUrlViewModel(await links.SentAsync(), await links.PendingAsync()));
}
