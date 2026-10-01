using Microsoft.Extensions.Options;

namespace UnoTP.Infrastructure;

public static class FeatureMiddleware
{
    /// <summary>
    /// Works out the request's features before the page runs: the appsettings
    /// switches, with what the user's menu does not open switched off.
    /// </summary>
    public static IApplicationBuilder UseFeatures(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var defaults = ctx.RequestServices.GetRequiredService<IOptions<FeatureFlags>>().Value;
            var features = new FeatureSet(defaults.Clone());
            // What the user's menu does not open stays shut, whatever appsettings says.
            if (ctx.Session.Menu() is { } menu) features = features.WithMenu(menu);
            ctx.Items[FeatureSet.ItemKey] = features;

            await next();
        });
}
