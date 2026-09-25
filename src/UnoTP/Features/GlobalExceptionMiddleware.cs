using System.Diagnostics;
using System.Net;

namespace UnoTP.Features;

/// <summary>
/// Catches whatever a page lets through. Every failure is logged once, with the
/// reference the partner is shown, so support can find it. The backend saying the
/// session has ended (401) sends the partner to Session Expired; anything else
/// shows the error page - or, to a caller that asked for JSON, a problem with the
/// same reference. Nothing of the exception itself reaches the partner.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception e) when (!ctx.RequestAborted.IsCancellationRequested)
        {
            var reference = Activity.Current?.Id ?? ctx.TraceIdentifier;
            if (ctx.Response.HasStarted)
            {
                // Too late to show anything else: the page is already on its way.
                log.LogError(e, "Unhandled error after the response started, {Method} {Path} (ref {Reference}).", ctx.Request.Method, ctx.Request.Path, reference);
                throw;
            }

            if (e is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
            {
                log.LogInformation("The backend ended the session, {Method} {Path} (ref {Reference}).", ctx.Request.Method, ctx.Request.Path, reference);
                ctx.Response.Clear();
                ctx.Response.Redirect($"{ctx.Request.PathBase}/Home/SessionExpired");
                return;
            }

            log.LogError(e, "Unhandled error, {Method} {Path} (ref {Reference}).", ctx.Request.Method, ctx.Request.Path, reference);
            ctx.Response.Clear();
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;

            if (ctx.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Response.WriteAsJsonAsync(
                    new { title = "Something went wrong.", status = 500, traceId = reference },
                    options: null, contentType: "application/problem+json");
                return;
            }

            // The error page, drawn by the app as any page is: the request is sent
            // through again as a GET for /Error, found afresh by routing.
            var (path, method) = (ctx.Request.Path, ctx.Request.Method);
            ctx.Request.Path = "/Error";
            ctx.Request.Method = HttpMethods.Get;
            ctx.SetEndpoint(null);
            ctx.Request.RouteValues.Clear();
            try
            {
                await next(ctx);
            }
            catch (Exception again)
            {
                // The error page could not be drawn either: say so plainly.
                log.LogError(again, "The error page failed too (ref {Reference}).", reference);
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.Clear();
                    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    ctx.Response.ContentType = "text/plain; charset=utf-8";
                    await ctx.Response.WriteAsync($"Uno TP could not finish that. Reference: {reference}");
                }
            }
            finally
            {
                (ctx.Request.Path, ctx.Request.Method) = (path, method);
            }
        }
    }
}
