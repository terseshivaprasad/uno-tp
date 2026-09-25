using Microsoft.Extensions.Options;

namespace UnoTP.Features;

public static class FeatureMiddleware
{
    /// <summary>
    /// Resolves the request's features before the page runs, so a ?ff= override is
    /// already in force for this render and its cookie goes out with this response.
    /// </summary>
    public static IApplicationBuilder UseFeatureOverrides(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var defaults = ctx.RequestServices.GetRequiredService<IOptions<FeatureFlags>>().Value;
            var features = FeatureSet.Resolve(ctx, defaults, out var clearCookie);
            // What the user's menu does not open stays shut, whatever appsettings or ?ff= say.
            if (ctx.Session.Menu() is { } menu) features = features.WithMenu(menu);
            ctx.Items[FeatureSet.ItemKey] = features;

            if (clearCookie)
            {
                ctx.Response.Cookies.Delete(FeatureSet.CookieName, new CookieOptions { Path = CookiePath(ctx) });
            }
            else if (ctx.Request.Query.ContainsKey(FeatureSet.QueryKey))
            {
                ctx.Response.Cookies.Append(FeatureSet.CookieName, features.ToCookieValue(), new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    // Under a virtual directory the overrides belong to this app only.
                    Path = CookiePath(ctx),
                });
            }

            await next();
        });

    private static string CookiePath(HttpContext ctx) =>
        ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value! : "/";
}
