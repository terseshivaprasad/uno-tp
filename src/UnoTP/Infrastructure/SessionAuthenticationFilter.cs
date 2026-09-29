using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UnoTP.Features;

/// <summary>Marks the pages that open without a session: the way in, the pages that say why not, and the error page.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWithoutSessionAttribute : Attribute;

/// <summary>
/// Checks every request for a session: a user the portal sent in through Home,
/// whose backend session has not ended. It runs before anything else on the page.
/// Without one, the demo signs its own user in and comes back to the page; anywhere
/// else the partner is shown Session Expired, and opens the app from the portal again.
/// </summary>
public sealed class SessionAuthenticationFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var action = context.ActionDescriptor as ControllerActionDescriptor;
        var open = action is not null
            && (action.MethodInfo.IsDefined(typeof(AllowWithoutSessionAttribute), true)
                || action.ControllerTypeInfo.IsDefined(typeof(AllowWithoutSessionAttribute), true));
        var http = context.HttpContext;
        if (open || http.Session.SignedIn()) return Task.CompletedTask;

        var demo = http.RequestServices.GetRequiredService<FeatureSet>().Flags.DemoData;
        if (demo && HttpMethods.IsGet(http.Request.Method))
        {
            // Back to this very page once the demo user is in.
            var back = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            context.Result = new RedirectToActionResult("Index", "Home", new { returnUrl = back.ToString() });
            return Task.CompletedTask;
        }
        http.Session.Clear();
        context.Result = new RedirectToActionResult("SessionExpired", "Home", null);
        return Task.CompletedTask;
    }
}
