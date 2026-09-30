using UnoTP.Api.Data;
using UnoTP.Backend;

namespace UnoTP.Api.Routes;

/// <summary>
/// The applications and their documents (docs/backend-api.md, Applications and
/// Documents). A save carries If-Match: "{version}": 412 when the application has
/// moved on since, 409 once it is submitted, 404 when it is not the partner's.
/// </summary>
public static class ApplicationRoutes
{
    private sealed record OpenBody(Holder Holder);

    private sealed record PageBody(string State);

    private sealed record SubmitBody(PaymentLink? PaymentLink);

    public static void MapApplications(this IEndpointRouteBuilder app)
    {
        var a = app.MapGroup("applications").AddEndpointFilter(RequirePartner);

        a.MapPost("", async (OpenBody body, SqlApplications apps, CancellationToken ct) =>
            Results.Ok(await apps.OpenAsync(body.Holder, ct)));

        a.MapGet("", async (SqlApplications apps, CancellationToken ct) => Results.Ok(await apps.ListAsync(ct)));

        a.MapGet("drafts", async (SqlApplications apps, CancellationToken ct) => Results.Ok(await apps.DraftsAsync(ct)));

        a.MapGet("{appNo}", async (string appNo, SqlApplications apps, CancellationToken ct) =>
            await apps.FindAsync(appNo, ct) is { } found ? Results.Ok(found) : Results.NotFound());

        a.MapPut("{appNo}/upload", (string appNo, UploadState body, HttpRequest req, SqlApplications apps, CancellationToken ct) =>
            Versioned(req, v => apps.SaveUploadOutcomeAsync(appNo, v, body, ct)));

        a.MapPut("{appNo}/details", (string appNo, ApplicationDetails body, HttpRequest req, SqlApplications apps, CancellationToken ct) =>
            Versioned(req, v => apps.SaveDetailsOutcomeAsync(appNo, v, body, ct)));

        a.MapPut("{appNo}/payment", (string appNo, PaymentDetails body, HttpRequest req, SqlApplications apps, CancellationToken ct) =>
            Versioned(req, v => apps.SavePaymentOutcomeAsync(appNo, v, body, ct)));

        a.MapPut("{appNo}/deposit", (string appNo, DepositDetails body, HttpRequest req, SqlApplications apps, CancellationToken ct) =>
            Versioned(req, v => apps.SaveDepositOutcomeAsync(appNo, v, body, ct)));

        a.MapPut("{appNo}/pages/{page}", async (string appNo, string page, PageBody body, SqlApplications apps, CancellationToken ct) =>
            await apps.SavePageAsync(appNo, page, body.State, ct) ? Results.NoContent() : Results.NotFound());

        a.MapPost("{appNo}/submit", async (string appNo, SubmitBody? body, HttpRequest req, SqlApplications apps, CancellationToken ct) =>
        {
            if (VersionOf(req) is not { } version) return Results.StatusCode(StatusCodes.Status428PreconditionRequired);
            var (outcome, submitted) = await apps.SubmitOutcomeAsync(appNo, version, body?.PaymentLink, ct);
            return outcome switch
            {
                SaveOutcome.Saved => Results.Ok(submitted),
                SaveOutcome.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status412PreconditionFailed),
            };
        });

        a.MapPost("{appNo}/resend-link", async (string appNo, SqlApplications apps, CancellationToken ct) =>
            await apps.ResendLinkAsync(appNo, ct) is { } submission ? Results.Ok(submission) : Results.Conflict());

        // ----- Documents (DMS) -----------------------------------------------

        a.MapPost("{appNo}/documents/{holder}/{slot}", async (string appNo, string holder, string slot, HttpRequest req, IDocumentApi dms, CancellationToken ct) =>
        {
            if (await FileOf(req, "file", ct) is not { } file) return Results.BadRequest();
            await dms.FileAsync(appNo, holder, slot, file, ct);
            return Results.NoContent();
        });

        a.MapDelete("{appNo}/documents/{holder}/{slot}", async (string appNo, string holder, string slot, IDocumentApi dms, CancellationToken ct) =>
        {
            await dms.DeleteAsync(appNo, holder, slot, ct);
            return Results.NoContent();
        });

        a.MapPost("{appNo}/documents/{holder}/{slot}/refused", async (string appNo, string holder, string slot, HttpRequest req, IDocumentApi dms, CancellationToken ct) =>
            await FileOf(req, "file", ct) is { } file ? Results.Ok(await dms.KeepRefusedAsync(appNo, holder, slot, file, ct)) : Results.BadRequest());

        a.MapGet("{appNo}/documents/{holder}/{slot}", async (string appNo, string holder, string slot, IDocumentApi dms, CancellationToken ct) =>
            await dms.CopyAsync(appNo, holder, slot, ct) is { } copy ? Results.File(copy.Bytes, copy.ContentType, copy.FileName) : Results.NotFound());
    }

    // Every application is some partner's: a request that names none is turned away.
    private static async ValueTask<object?> RequirePartner(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.Headers[ApiClient.PartnerHeader].ToString().Length == 0
            ? Results.Unauthorized()
            : await next(context);

    private static async Task<IResult> Versioned(HttpRequest req, Func<int, Task<(SaveOutcome Outcome, int? Version)>> save)
    {
        if (VersionOf(req) is not { } version) return Results.StatusCode(StatusCodes.Status428PreconditionRequired);
        var (outcome, saved) = await save(version);
        return outcome switch
        {
            SaveOutcome.Saved => Results.Ok(new { version = saved }),
            SaveOutcome.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status412PreconditionFailed),
        };
    }

    // If-Match: "7" - the version the page read.
    private static int? VersionOf(HttpRequest req) =>
        int.TryParse(req.Headers.IfMatch.ToString().Trim().Trim('"'), out var version) ? version : null;

    /// <summary>A file posted as multipart form data under <paramref name="name"/>, or null.</summary>
    public static async Task<UploadFile?> FileOf(HttpRequest req, string name, CancellationToken ct)
    {
        if (!req.HasFormContentType) return null;
        var form = await req.ReadFormAsync(ct);
        if (form.Files.GetFile(name) is not { } posted) return null;
        using var buffer = new MemoryStream();
        await posted.CopyToAsync(buffer, ct);
        return new UploadFile(posted.FileName, posted.ContentType ?? "application/octet-stream", buffer.ToArray());
    }
}
