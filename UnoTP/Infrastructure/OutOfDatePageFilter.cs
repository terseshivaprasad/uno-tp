using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using UnoTP.Data;
using UnoTP.Models;

namespace UnoTP.Infrastructure;

/// <summary>
/// A post whose antiforgery token the app cannot accept - a page open since before
/// the app restarted - is turned down by ASP.NET with a 400 and no page, which the
/// browser shows as a blank screen. Here it is
/// taken as an out-of-date page instead: the partner is sent back to the same
/// address, opened afresh, with a line saying so (Messages.Shared.PageOutOfDate),
/// and presses the button again.
/// </summary>
public sealed class OutOfDatePageFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;
        var http = context.HttpContext;
        var tempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        // Said both ways the pages take a one-time line: the step pages read a Flash
        // keyed by the application; the review and list pages read "said".
        tempData["said"] = JsonSerializer.Serialize(new Dictionary<string, string> { ["banner"] = Messages.Shared.PageOutOfDate });
        if (http.CurrentApplication() is { } appNo)
        {
            // The step pages' keys: "flash:" for Upload Documents, "flash-info:" for Investor Information, "flash-deposit:" for FD Configuration.
            var flash = JsonSerializer.Serialize(new Flash { Banner = Messages.Shared.PageOutOfDate });
            tempData["flash:" + appNo] = flash;
            tempData["flash-info:" + appNo] = flash;
            tempData["flash-deposit:" + appNo] = flash;
        }
        tempData.Save();
        context.Result = new LocalRedirectResult(http.Request.PathBase + http.Request.Path);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
