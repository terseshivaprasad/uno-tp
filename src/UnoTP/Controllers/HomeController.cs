using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>The pages that belong to no feature: the error page, and the drawio download.</summary>
public class HomeController : Controller
{
    [HttpGet("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>A standalone page to download the Unotp search page drawio file.</summary>
    [HttpGet("DrawioDownload")]
    public IActionResult DrawioDownload() => View();
}
