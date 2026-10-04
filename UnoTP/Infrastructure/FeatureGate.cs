using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using UnoTP.Models;

namespace UnoTP.Infrastructure;

/// <summary>
/// Names the console feature a controller or action belongs to (the backend's
/// features, see <see cref="ConsoleBoard.Features"/>), so it closes whenever the feature's tile
/// is greyed out. Given more than one, it serves each of them, and closes only when
/// every one is off.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresFeatureAttribute(params string[] keys) : Attribute
{
    public IReadOnlyList<string> Keys { get; } = keys;
}

/// <summary>
/// Closes the pages of a feature that is switched off in appsettings, or inside a
/// window that takes it off. A tile that is greyed out would otherwise still open
/// from a bookmark or a typed address, and a form posted from a page left open
/// would go through. Either way the dashboard is shown, saying why.
/// </summary>
public sealed class FeatureGate : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // The action's own attribute stands over its controller's.
        var keys = context.ActionDescriptor is ControllerActionDescriptor action
            ? (action.MethodInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true)
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true))
                .Cast<RequiresFeatureAttribute>()
                .FirstOrDefault()?.Keys)
            : null;

        if (keys is { Count: > 0 })
        {
            var services = context.HttpContext.RequestServices;
            var features = services.GetRequiredService<FeatureSet>();
            var board = await services.GetRequiredService<ConsoleState>().BoardAsync();
            if (keys.All(key => board.OffLabel(key, features.Flags) is not null))
            {
                context.Result = new RedirectToActionResult("Index", "Dashboard", new { off = keys[0] });
                return;
            }
        }

        await next();
    }
}
