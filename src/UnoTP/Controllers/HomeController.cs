using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UnoTP.Backend;
using UnoTP.Backend.External;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// The way into the app. The portal opens /Home?UserId=...&amp;Syscode=..., both
/// encrypted. They are decrypted by the portal's service, the backend starts a
/// session for the user and says which menus they may open, and the user lands
/// on the dashboard - with nothing of either value left in the address. Also the
/// error page, and the drawio download.
/// </summary>
[AllowWithoutSession]
public class HomeController(
    IDecryptionService decryption,
    ISessionApi sessions,
    FeatureSet features,
    IOptions<EntryOptions> entry,
    ILogger<HomeController> log) : Controller
{
    [HttpGet("Home")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "UserId")] string? userId,
        [FromQuery(Name = "Syscode")] string? sysCode,
        string? returnUrl)
    {
        var fromPortal = !string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(sysCode);
        if (!fromPortal && HttpContext.Session.SignedIn()) return Onward(returnUrl);

        string? user, code;
        if (fromPortal)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sysCode))
                return Refused("The link from the portal is missing its user or system code.");
            try
            {
                user = await decryption.DecryptAsync(userId);
                code = await decryption.DecryptAsync(sysCode);
            }
            catch (ExternalServiceException e)
            {
                log.LogWarning(e, "Entry: the decryption service failed.");
                return Refused("The portal's details could not be read just now. Try opening Uno TP from the portal again in a while.");
            }
            if (user is null || code is null) return Refused("The link from the portal could not be read. Open Uno TP from the portal again.");
        }
        else if (features.Flags.DemoData && entry.Value.DemoUserId.Length > 0)
        {
            // The demo comes in as its own user, through the same session and menu.
            (user, code) = (entry.Value.DemoUserId, entry.Value.DemoSysCode);
        }
        else
        {
            return RedirectToAction(nameof(SessionExpired));
        }

        UserSession? started;
        IReadOnlyList<MenuItem> menu;
        try
        {
            started = await sessions.StartAsync(user, code);
            if (started is null) return Refused("The portal did not start a session for you on Uno TP. Ask your administrator for access.");
            // The menu is asked for on the new session, so it is kept first.
            HttpContext.Session.SignIn(started, []);
            menu = await sessions.MenuAsync();
        }
        catch (HttpRequestException e)
        {
            HttpContext.Session.Clear();
            log.LogWarning(e, "Entry: the backend did not start a session or give a menu.");
            return Refused("Uno TP could not start your session just now. Try again in a while.");
        }

        var keys = menu.Select(m => m.Key).Where(FeatureSet.MenuKeys.Contains).ToList();
        if (keys.Count == 0)
        {
            HttpContext.Session.Clear();
            return Refused("Your menu does not include Uno TP. Ask your administrator for access.");
        }
        HttpContext.Session.SignIn(started, keys);
        return Onward(returnUrl);
    }

    /// <summary>Where a page goes when nobody has come in from the portal, or their session has ended.</summary>
    [HttpGet("Home/SessionExpired")]
    public IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return View();
    }

    /// <summary>Ends the session here and goes back to the portal, which signs the user out there.</summary>
    [HttpGet("Home/Logout")]
    public IActionResult Logout([FromServices] UnoTP.AppUrls apps)
    {
        HttpContext.Session.Clear();
        return Redirect(apps.Url("/"));
    }

    /// <summary>A feature the user's menu does not open, reached by its address.</summary>
    [HttpGet("Home/Unauthorized")]
    public IActionResult Unauthorized(string? feature) =>
        Refused(feature is not null && FeatureSet.MenuKeys.Contains(feature)
            ? $"{UnoTP.Models.ConsoleAdmin.NameOf(feature)} is not in your menu. Ask your administrator if you need it."
            : "You do not have access to this page.");

    [HttpGet("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>A standalone page to download the Unotp search page drawio file.</summary>
    [HttpGet("DrawioDownload")]
    public IActionResult DrawioDownload() => View();

    // Only ever to a page of this app.
    private IActionResult Onward(string? returnUrl) =>
        LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "~/Dashboard");

    // The way in was refused, or the page is not the user's: said on Unauthorized.
    private ViewResult Refused(string message)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("Unauthorized", message);
    }
}
