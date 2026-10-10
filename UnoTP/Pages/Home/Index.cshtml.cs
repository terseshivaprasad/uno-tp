using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Infrastructure;

namespace UnoTP.Pages;

/// <summary>
/// The way in from the portal, at Home and Home/Index.
///
/// The portal's dashboard opens /Home/Index?UserId=...&amp;SysCode=... (the app's root
/// leads there too), both values encrypted by the portal. Entry asks the E-Sarathi
/// auth API three things: to decrypt the two values, to start a session for the
/// user, and for their Uno TP menus - a user with none is refused. It keeps who the user
/// is, and sends them to the dashboard, with nothing of either value left in the address.
/// A way in that is refused draws this page, saying why.
///
/// The way back to the portal, and the pages that say why not, are the other pages
/// of this folder: Home/Home, Home/LogOut, Home/SessionExpired, Home/Unauthorized,
/// Home/TooManyRequests and Home/Error.
/// </summary>
[AllowWithoutSession]
[EnableRateLimiting(RateLimits.Entry)]
public class EntryModel(
    IDecryptionService decryption,
    ISessionApi sessions,
    ILogger<EntryModel> log) : PageModel
{
    /// <summary>Why the way in was refused.</summary>
    public string Message { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(
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
                return Refused(Messages.SignIn.LinkIncomplete);
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
                return Refused(Messages.SignIn.PortalNotReadNow);
            }
            if (decryptedUser is null || decryptedCode is null)
            {
                return Refused(Messages.SignIn.LinkNotRead);
            }
            user = decryptedUser;
            code = decryptedCode;
        }
        else
        {
            return RedirectToPage("/Home/SessionExpired");
        }

        UserSession? started;
        IReadOnlyList<MenuItem> menu;
        try
        {
            started = await sessions.StartAsync(SessionStarts.For(HttpContext, user, code));
            if (started is null)
            {
                return Refused(Messages.SignIn.NoSessionFromPortal);
            }
            menu = await sessions.MenuAsync(started.User.Agency_Usr_Clustered_ID, code);
        }
        catch (ExternalServiceException e)
        {
            log.LogWarning(e, "Entry: the auth API did not start a session or give a menu.");
            return Refused(Messages.SignIn.SessionNotStarted);
        }

        // A user the portal gives no Uno TP menu to has no way in. Which features are on
        // is not the menu's to say: that is the "Features" section of appsettings.
        if (menu.Count == 0)
        {
            return Refused(Messages.SignIn.NotOnMenu);
        }

        HttpContext.Session.SignIn(started);
        // What the last user of this browser left on a page - an investor searched for,
        // a message waiting to be shown - goes with their session.
        TempData.Clear();
        // Kept for the way back to the portal (Home). After SignIn, which clears the session.
        if (fromPortal) HttpContext.Session.KeepPortalValues(userId!, sysCode!);
        return Onward(returnUrl);
    }

    // Only ever to a page of this app.
    private IActionResult Onward(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return LocalRedirect("~/Dashboard");
    }

    // The way in was refused: this page says why.
    private PageResult Refused(string message)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Message = message;
        return Page();
    }
}
