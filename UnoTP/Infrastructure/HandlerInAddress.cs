using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UnoTP.Infrastructure;

/// <summary>
/// An address that names a handler the page does not have opens nothing: 404.
/// Left alone, Razor Pages would fall back to the page's plain handler - a post to
/// InvestorInformation/{appNo}/anything would run Proceed - or draw the page with
/// no handler run at all.
/// </summary>
public sealed class HandlerInAddress : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var asked = context.RouteData.Values["handler"] as string ?? context.HttpContext.Request.Query["handler"].ToString();
        var chosen = context.HandlerMethod;
        if (chosen is null || !string.Equals(chosen.Name ?? "", asked, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
