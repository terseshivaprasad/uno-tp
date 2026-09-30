namespace UnoTP.Infrastructure;

/// <summary>
/// The headers every response carries, so a browser holds the pages to what they
/// are: scripts, styles, fonts and images only from the app itself (every one is
/// served locally - Bootstrap, the Georama font, the pages' own files), documents
/// previewed only from it, forms posted only to it, and the pages framed by no other
/// site unless Security:FrameAncestors names it (the portal, if it ever frames them).
/// Inline styles are allowed - a few attributes and the required-documents rules
/// set them; inline script is not, and the pages have none.
/// </summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IConfiguration config)
    {
        var ancestors = string.Join(' ', new[] { "'self'" }.Concat(
            (config["Security:FrameAncestors"] ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)));
        var policy = string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob:",
            "font-src 'self'",
            "connect-src 'self'",
            "frame-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            $"frame-ancestors {ancestors}");
        var sameOriginOnly = ancestors == "'self'";

        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = policy;
                headers.XContentTypeOptions = "nosniff";
                // frame-ancestors says it for current browsers; this for older ones.
                if (sameOriginOnly) headers.XFrameOptions = "SAMEORIGIN";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers.Remove("X-Powered-By");
                return Task.CompletedTask;
            });
            await next();
        });
    }
}
