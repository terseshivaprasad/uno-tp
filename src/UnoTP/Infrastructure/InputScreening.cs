namespace UnoTP.Infrastructure;

/// <summary>
/// Screens every request's input before a page sees it, and refuses the request
/// (400) when it is not what a page of this app would send:
///   - a query or form parameter given more than once (parameter pollution) - except
///     a checkbox and the hidden "false" beside it, which post true and false together;
///   - a query or form value carrying a character no field of this app takes:
///     angle brackets, quotes, semicolons, backslashes, braces and the like;
///   - a value longer than any field of this app takes (a remark, the longest, is 200).
/// The pages check each field's own shape as well (InputRules); this is the backstop
/// against input that did not come from a page.
/// </summary>
public sealed class InputScreening(RequestDelegate next, ILogger<InputScreening> log)
{
    /// <summary>Characters no typed value may carry, whatever the field.</summary>
    private const string NeverAllowed = "<>\"';\\{}%$#!*()[]^~|`";

    /// <summary>The longest any value may be: a remark's 200 characters (a query value may carry a path).</summary>
    private const int LongestValue = 200;
    private const int LongestQueryValue = 500;

    public async Task InvokeAsync(HttpContext context)
    {
        var refused = ScreenQuery(context) ?? await ScreenFormAsync(context);
        if (refused is null)
        {
            await next(context);
            return;
        }

        log.LogWarning("Request refused, {Method} {Path}: {Why}", context.Request.Method, context.Request.Path, refused);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("The request carried input this page does not take.");
    }

    private static string? ScreenQuery(HttpContext context)
    {
        foreach (var (name, values) in context.Request.Query)
        {
            if (values.Count > 1) return $"query parameter '{name}' given {values.Count} times";
            foreach (var value in values)
            {
                if (HasNeverAllowed(value)) return $"query parameter '{name}' carries a character not allowed";
                if ((value ?? "").Length > LongestQueryValue) return $"query parameter '{name}' is too long";
            }
        }
        return null;
    }

    private static async Task<string?> ScreenFormAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType) return null;
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        foreach (var (name, values) in form)
        {
            if (name == "__RequestVerificationToken") continue;
            if (values.Count > 1 && !IsCheckboxPair(values)) return $"form field '{name}' given {values.Count} times";
            foreach (var value in values)
            {
                if (HasNeverAllowed(value)) return $"form field '{name}' carries a character not allowed";
                if ((value ?? "").Length > LongestValue) return $"form field '{name}' is too long";
            }
        }
        return null;
    }

    // A checkbox posts "true" beside the hidden "false" under it: the one repeat a page makes.
    private static bool IsCheckboxPair(Microsoft.Extensions.Primitives.StringValues values)
    {
        if (values.Count != 2) return false;
        var first = values[0] ?? "";
        var second = values[1] ?? "";
        return first == "true" && second == "false" || first == "false" && second == "true";
    }

    private static bool HasNeverAllowed(string? value)
    {
        if (value is null) return false;
        foreach (var c in value)
        {
            if (NeverAllowed.Contains(c)) return true;
        }
        return false;
    }
}
