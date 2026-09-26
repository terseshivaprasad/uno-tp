using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace UnoTP.Features;

/// <summary>
/// The application a wizard step works on travels in the page's address -
/// Apps/UnoTp/Application/{appNo}/... - never in the server's session: the
/// application and everything on it are the backend's, kept there for audit.
/// </summary>
public static class ApplicationUrls
{
    /// <summary>The controllers whose addresses carry the application's number.</summary>
    public static readonly IReadOnlySet<string> Steps =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UploadDocuments", "InvestorInfo", "Application" };

    /// <summary>The application the page is on, from its address; null off the wizard.</summary>
    public static string? CurrentApplication(this HttpContext http) => http.GetRouteValue("appNo") as string;
}

/// <summary>
/// Hands out URL helpers that carry the application's number into every link and
/// redirect to another wizard step, as the address the page was reached by holds
/// it: a link, form or redirect names the step, never the application. Endpoint
/// routing does not carry a route value over to another action by itself.
/// </summary>
public sealed class ApplicationUrlHelperFactory(IUrlHelperFactory inner) : IUrlHelperFactory
{
    public IUrlHelper GetUrlHelper(ActionContext context) => new ApplicationUrlHelper(inner.GetUrlHelper(context));

    private sealed class ApplicationUrlHelper(IUrlHelper inner) : IUrlHelper
    {
        public ActionContext ActionContext => inner.ActionContext;

        public string? Action(UrlActionContext context)
        {
            var here = ActionContext.RouteData.Values;
            var controller = context.Controller ?? here["controller"] as string;
            if (controller is not null && ApplicationUrls.Steps.Contains(controller) && here["appNo"] is string appNo)
            {
                var values = new RouteValueDictionary(context.Values);
                if (!values.ContainsKey("appNo"))
                {
                    values["appNo"] = appNo;
                    context.Values = values;
                }
            }
            return inner.Action(context);
        }

        public string? Content(string? contentPath) => inner.Content(contentPath);
        public bool IsLocalUrl(string? url) => inner.IsLocalUrl(url);
        public string? Link(string? routeName, object? values) => inner.Link(routeName, values);
        public string? RouteUrl(UrlRouteContext routeContext) => inner.RouteUrl(routeContext);
    }
}
