using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace UnoTP.Infrastructure;

/// <summary>
/// The application a wizard step works on travels in the page's address -
/// UploadInvestorDocuments/{appNo}, InvestorInformation/{appNo} and so on - never in the server's session: the
/// application and everything on it are the backend's, kept there for audit.
/// </summary>
public static class ApplicationUrls
{
    /// <summary>The pages whose addresses carry the application's number.</summary>
    public static readonly IReadOnlySet<string> Steps =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Documents/Index", "/Investor/Index", "/Payment/Index", "/Deposit/Index", "/Review/Index", "/Submitted/Index",
        };

    /// <summary>The application the page is on, from its address; null off the wizard.</summary>
    public static string? CurrentApplication(this HttpContext http) => http.GetRouteValue("appNo") as string;

    /// <summary>
    /// The address of one of this page's own handlers: on Upload Documents,
    /// Url.Handler("Upload") is UploadInvestorDocuments/{appNo}/Upload.
    /// </summary>
    public static string Handler(this IUrlHelper url, string handler, object? values = null) =>
        url.Page(pageName: null, pageHandler: handler, values: values)!;
}

/// <summary>
/// Hands out URL helpers that carry the application's number into every link and
/// redirect to another wizard step, as the address the page was reached by holds
/// it: a link, form or redirect names the step, never the application. Endpoint
/// routing does not carry a route value over to another page by itself.
/// </summary>
public sealed class ApplicationUrlHelperFactory(IUrlHelperFactory inner) : IUrlHelperFactory
{
    public IUrlHelper GetUrlHelper(ActionContext context) => new ApplicationUrlHelper(inner.GetUrlHelper(context));

    private sealed class ApplicationUrlHelper(IUrlHelper inner) : IUrlHelper
    {
        public ActionContext ActionContext => inner.ActionContext;

        public string? Action(UrlActionContext context) => inner.Action(context);
        public string? Content(string? contentPath) => inner.Content(contentPath);
        public bool IsLocalUrl(string? url) => inner.IsLocalUrl(url);
        public string? Link(string? routeName, object? values) => inner.Link(routeName, values);

        // Every link, form and redirect to a page comes through here (Url.Page,
        // asp-page, RedirectToPage), with the page it names among its values.
        public string? RouteUrl(UrlRouteContext context)
        {
            var here = ActionContext.RouteData.Values;
            var values = new RouteValueDictionary(context.Values);
            if (values["page"] is string page && ApplicationUrls.Steps.Contains(page) && here["appNo"] is string appNo && !values.ContainsKey("appNo"))
            {
                values["appNo"] = appNo;
                context.Values = values;
            }
            return inner.RouteUrl(context);
        }
    }
}
