using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using UnoTP.Models;

namespace UnoTP.Infrastructure;

/// <summary>Marks the pages that open without a session: the way in, the pages that say why not, and the error page.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AllowWithoutSessionAttribute : Attribute;

/// <summary>
/// Checks every request for a session: a user the portal sent in through Home,
/// whose session has not ended - in the browser's sign-in, and in the session the
/// backend keeps, so ending that one or taking the partner out of use signs them
/// out within <see cref="Recheck"/>. It runs before anything else on the page.
/// Without one the partner is shown Session Expired, and opens the app from the portal again.
/// </summary>
public sealed class SessionAuthenticationFilter : IAsyncAuthorizationFilter
{
    /// <summary>How long a session found open is taken as open without asking again.</summary>
    public static readonly TimeSpan Recheck = TimeSpan.FromSeconds(30);

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var page = context.ActionDescriptor as CompiledPageActionDescriptor;
        var open = page is not null && page.HandlerTypeInfo.IsDefined(typeof(AllowWithoutSessionAttribute), true);
        var http = context.HttpContext;
        if (open || http.Session.SignedIn() && await StillOpenAsync(http)) return;

        http.Session.Clear();
        context.Result = new RedirectToPageResult("/Home/SessionExpired");
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
