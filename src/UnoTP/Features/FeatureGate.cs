using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using UnoTP.Models;

namespace UnoTP.Features;

/// <summary>
/// Names the console feature a controller or action belongs to (see
/// <see cref="ConsoleAdmin.Features"/>), so it closes whenever the feature's tile
/// is greyed out.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresFeatureAttribute(string key) : Attribute
{
    public string Key { get; } = key;
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
        var key = context.ActionDescriptor is ControllerActionDescriptor action
            ? (action.MethodInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true)
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true))
                .Cast<RequiresFeatureAttribute>()
                .FirstOrDefault()?.Key)
            : null;

        if (key is not null)
        {
            var services = context.HttpContext.RequestServices;
            var features = services.GetRequiredService<FeatureSet>();
            var board = await services.GetRequiredService<ConsoleState>().BoardAsync();
            if (board.OffLabel(key, features.Flags) is not null)
            {
                context.Result = new RedirectToActionResult("Index", "Dashboard", new { off = key });
                return;
            }
        }

        await next();
    }
}
