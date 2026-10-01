using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// The way in from the portal, and the way back to it.
///
/// The portal's dashboard opens /Home/Index?UserId=...&amp;SysCode=... (the app's root
/// leads there too), both values encrypted by the portal. Entry asks the E-Sarathi
/// auth API three things: to decrypt the two values, to start a session for the
/// user, and for the menus they may open. It keeps who the user is and their menus,
/// and sends them to the dashboard, with nothing of either value left in the address.
///
/// Home goes back to the portal's dashboard with the same encrypted values, so the
/// portal knows who is back. Logout ends the session here and goes to the portal's
/// logout page. The controller also serves the session-expired, unauthorized and error
/// pages, and the drawio download.
/// </summary>
[AllowWithoutSession]
public class EntryController(
    IDecryptionService decryption,
    ISessionApi sessions,
    AppUrls apps,
    IOptions<PortalOptions> portal,
    IOptions<MenuOptions> menuPages,
    ILogger<EntryController> log) : Controller
{
    [HttpGet("Home")]
    [HttpGet("Home/Index")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "UserId")] string? userId,
        [FromQuery(Name = "Syscode")] string? sysCode,
        string? returnUrl)
    {
        var fromPortal = !string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(sysCode);

        // Already signed in and not coming in afresh from the portal: straight on.
        if (!fromPortal && HttpContext.Session.SignedIn()) return Onward(returnUrl);

        string user;
        string code;
        if (fromPortal)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sysCode))
            {
                return Refused("The link from the portal is missing its user or system code.");
            }

            // The values are base64. A '+' in them arrives as a space when the link is not
            // URL-encoded, so the spaces are put back before decrypting.
            userId = userId.Replace(' ', '+');
            sysCode = sysCode.Replace(' ', '+');

            string? decryptedUser;
            string? decryptedCode;
            try
            {
                decryptedUser = await decryption.DecryptAsync(userId);
                decryptedCode = await decryption.DecryptAsync(sysCode);
            }
            catch (ExternalServiceException e)
            {
                log.LogWarning(e, "Entry: the decryption service failed.");
                return Refused("The portal's details could not be read just now. Try opening Uno TP from the portal again in a while.");
            }
            if (decryptedUser is null || decryptedCode is null)
            {
                return Refused("The link from the portal could not be read. Open Uno TP from the portal again.");
            }
            user = decryptedUser;
            code = decryptedCode;
        }
        else
        {
            return RedirectToAction(nameof(SessionExpired));
        }

        UserSession? started;
        IReadOnlyList<MenuItem> menu;
        try
        {
            started = await sessions.StartAsync(SessionStarts.For(HttpContext, user, code));
            if (started is null)
            {
                return Refused("The portal did not start a session for you on Uno TP. Ask your administrator for access.");
            }
            menu = await sessions.MenuAsync(user, code);
        }
        catch (ExternalServiceException e)
        {
            log.LogWarning(e, "Entry: the auth API did not start a session or give a menu.");
            return Refused("Uno TP could not start your session just now. Try again in a while.");
        }

        // The pages of the portal's menu, as the console features they open.
        var menuKeys = menuPages.Value.FeatureKeys(menu);
        if (menuKeys.Count == 0)
        {
            return Refused("Your menu does not include Uno TP. Ask your administrator for access.");
        }

        HttpContext.Session.SignIn(started, menuKeys);
        // Kept for the way back to the portal (Home). After SignIn, which clears the session.
        if (fromPortal) HttpContext.Session.KeepPortalValues(userId!, sysCode!);
        return Onward(returnUrl);
    }

    /// <summary>
    /// Back to the portal's dashboard, with the encrypted values the portal sent in, so it
    /// knows who is back. Without them it is the plain dashboard address.
    /// </summary>
    [HttpGet("Home/Home")]
    public IActionResult Home()
    {
        var home = portal.Value.Home;
        if (home.Length == 0) home = apps.Url("/Classic");

        var values = HttpContext.Session.PortalValues();
        if (values is null) return Redirect(home);

        var (userId, sysCode) = values.Value;
        var separator = home.Contains('?') ? "&" : "?";
        return Redirect($"{home}{separator}UserId={Uri.EscapeDataString(userId)}&SysCode={Uri.EscapeDataString(sysCode)}");
    }

    /// <summary>Ends the session here and goes to the portal's logout page, which signs the user out there.</summary>
    [HttpGet("Home/LogOut")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        var logout = portal.Value.Logout;
        if (logout.Length == 0) logout = apps.Url("/");
        return Redirect(logout);
    }

    /// <summary>Where a page goes when nobody has come in from the portal, or their session has ended.</summary>
    [HttpGet("Home/SessionExpired")]
    [HttpGet("Error/Expired")]
    public IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return View();
    }

    /// <summary>A feature the user's menu does not open, reached by its address.</summary>
    [HttpGet("Home/Unauthorized")]
    public async Task<IActionResult> Unauthorized(string? feature, [FromServices] UnoTP.Models.ConsoleState console)
    {
        if (feature is not null && FeatureSet.MenuKeys.Contains(feature))
        {
            var name = (await console.BoardAsync()).NameOf(feature);
            return Refused($"{name} is not in your menu. Ask your administrator if you need it.");
        }
        return Refused("You do not have access to this page.");
    }

    [HttpGet("Home/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>A standalone page to download the Uno TP search page's drawio file.</summary>
    [HttpGet("Home/DrawioDownload")]
    public IActionResult DrawioDownload() => View();

    // Only ever to a page of this app.
    private IActionResult Onward(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return LocalRedirect("~/Dashboard");
    }

    // The way in was refused, or the page is not the user's: said on the Unauthorized page.
    private ViewResult Refused(string message)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("Unauthorized", message);
    }
}
