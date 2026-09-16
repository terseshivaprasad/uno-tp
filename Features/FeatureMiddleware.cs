using Microsoft.Extensions.Options;

namespace UnoTp.Features;

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
            ctx.Items[FeatureSet.ItemKey] = features;

            if (clearCookie)
            {
                ctx.Response.Cookies.Delete(FeatureSet.CookieName);
            }
            else if (ctx.Request.Query.ContainsKey(FeatureSet.QueryKey))
            {
                ctx.Response.Cookies.Append(FeatureSet.CookieName, features.ToCookieValue(), new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                });
            }

            await next();
        });
}
