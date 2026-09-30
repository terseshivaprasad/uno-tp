using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using UnoTP.Backend;

namespace UnoTP.Infrastructure;

/// <summary>Marks the pages that open without a session: the way in, the pages that say why not, and the error page.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWithoutSessionAttribute : Attribute;

/// <summary>
/// Checks every request for a session: a user the portal sent in through Home,
/// whose session has not ended - in the browser's sign-in, and in the session the
/// backend keeps, so ending that one or taking the partner out of use signs them
/// out within <see cref="Recheck"/>. It runs before anything else on the page.
/// Without one, the demo signs its own user in and comes back to the page; anywhere
/// else the partner is shown Session Expired, and opens the app from the portal again.
/// </summary>
public sealed class SessionAuthenticationFilter : IAsyncAuthorizationFilter
{
    /// <summary>How long a session found open is taken as open without asking again.</summary>
    public static readonly TimeSpan Recheck = TimeSpan.FromSeconds(30);

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var action = context.ActionDescriptor as ControllerActionDescriptor;
        var open = action is not null
            && (action.MethodInfo.IsDefined(typeof(AllowWithoutSessionAttribute), true)
                || action.ControllerTypeInfo.IsDefined(typeof(AllowWithoutSessionAttribute), true));
        var http = context.HttpContext;
        if (open || http.Session.SignedIn() && await StillOpenAsync(http)) return;

        var demo = http.RequestServices.GetRequiredService<FeatureSet>().Flags.DemoData;
        if (demo && HttpMethods.IsGet(http.Request.Method))
        {
            // Back to this very page once the demo user is in.
            var back = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            context.Result = new RedirectToActionResult("Index", "Entry", new { returnUrl = back.ToString() });
            return;
        }
        http.Session.Clear();
        context.Result = new RedirectToActionResult("SessionExpired", "Entry", null);
    }

    // Only an open session is remembered: one found closed is asked about again,
    // as is one the backend could not answer for.
    private static async Task<bool> StillOpenAsync(HttpContext http)
    {
        if (http.Session.BackendSession() is not { } id) return false;
        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        var key = "session-open:" + id;
        if (cache.TryGetValue(key, out _)) return true;
        if (!await http.RequestServices.GetRequiredService<ISessionApi>().IsOpenAsync(http.RequestAborted)) return false;
        cache.Set(key, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Recheck, Size = 1 });
        return true;
    }
}
