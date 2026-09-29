using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace UnoTP.Features;

/// <summary>
/// One round trip for a change instead of two. Every post answers with a redirect
/// back to its page, so a refresh never posts twice; the browser then asks for the
/// page again. For a post made by partial-forms.js - a toggle, a drop-down, a file -
/// that second request costs a whole round trip more, which on a slow connection is
/// most of the wait. So when such a post redirects back to the very page it was made
/// from, the redirect is followed here, on the server, and the page goes back in the
/// post's own answer, with the address it stands at in X-Partial-Url.
///
/// A post that moves on to another page keeps its redirect: the browser goes there
/// the ordinary way. A post made without the script is untouched.
/// </summary>
public static class PartialFollow
{
    /// <summary>Sent by partial-forms.js: the path of the page the post was made from.</summary>
    public const string PageHeader = "X-Partial-Page";

    /// <summary>Sent back: the address the page the answer holds stands at.</summary>
    public const string UrlHeader = "X-Partial-Url";

    /// <summary>Runs before routing, so the follow-up is routed and authorised afresh.</summary>
    public static IApplicationBuilder UsePartialFollow(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var page = ctx.Request.Headers[PageHeader].ToString();
            if (!HttpMethods.IsPost(ctx.Request.Method) || page.Length == 0)
            {
                await next();
                return;
            }

            // What the request carried before MVC added its own for the post - the
            // TempData it made and the note that it was saved, and the like - so the
            // follow-up starts from the same place a fresh request would.
            var items = new Dictionary<object, object?>(ctx.Items);

            await next();

            var response = ctx.Response;
            if (response.HasStarted || response.StatusCode is not (StatusCodes.Status302Found or StatusCodes.Status303SeeOther)) return;
            var location = response.Headers.Location.ToString();
            // Only a local address, back to the page the post came from.
            if (!location.StartsWith('/') || location.StartsWith("//")) return;
            var target = new Uri(new Uri("http://local"), location);
            if (!string.Equals(target.AbsolutePath, page, StringComparison.Ordinal)) return;

            // What the post set - TempData, above all - is what the page reads.
            CarryCookies(ctx);

            response.StatusCode = StatusCodes.Status200OK;
            response.Headers.Remove(HeaderNames.Location);
            response.Headers[UrlHeader] = location;

            var request = ctx.Request;
            var pathBase = request.PathBase.Value ?? "";
            var path = target.AbsolutePath;
            request.Method = HttpMethods.Get;
            request.Path = pathBase.Length > 0 && path.StartsWith(pathBase, StringComparison.Ordinal) ? path[pathBase.Length..] : path;
            request.QueryString = new QueryString(target.Query);
            request.Body = Stream.Null;
            request.ContentLength = 0;
            request.ContentType = null;
            ctx.Features.Set<IFormFeature>(null);
            request.RouteValues.Clear();
            ctx.SetEndpoint(null);
            ctx.Items.Clear();
            foreach (var (key, value) in items) ctx.Items[key] = value;

            await next();
        });

    // The cookies the post's answer set, laid over the ones the request brought.
    private static void CarryCookies(HttpContext ctx)
    {
        var set = ctx.Response.Headers.SetCookie;
        if (set.Count == 0) return;
        var cookies = ctx.Request.Cookies.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
        foreach (var header in set)
        {
            if (header is null || !SetCookieHeaderValue.TryParse(header, out var cookie)) continue;
            var name = cookie.Name.ToString();
            if (cookie.Expires is { } expires && expires < DateTimeOffset.UtcNow) cookies.Remove(name);
            else cookies[name] = cookie.Value.ToString();
        }
        ctx.Request.Headers.Cookie = string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
        // The cookies are read once a request; read them afresh.
        ctx.Features.Set<IRequestCookiesFeature>(new RequestCookiesFeature(ctx.Features));
    }
}
