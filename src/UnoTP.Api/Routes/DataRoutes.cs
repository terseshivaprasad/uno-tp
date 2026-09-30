using UnoTP.Backend;

namespace UnoTP.Api.Routes;

/// <summary>
/// Everything but the applications: entry, the lists and rules, investors,
/// renewals, the registers and the console (docs/backend-api.md). Each answers
/// from whatever Program.cs registers for it: the database for entry, the lists
/// and rules, the partner, the registers and the console; the mock for what has
/// no source yet.
/// </summary>
public static class DataRoutes
{
    private sealed record SessionBody(string UserId, string SysCode);

    private sealed record RenewalBody(string DepositNumber);

    private sealed record LinkBody(string AppNo, string Purpose);

    public static void MapData(this IEndpointRouteBuilder app)
    {
        // ----- Entry ---------------------------------------------------------
        app.MapPost("sessions", async (SessionBody body, ISessionApi sessions, CancellationToken ct) =>
            await sessions.StartAsync(body.UserId, body.SysCode, ct) is { } session ? Results.Ok(session) : Results.Unauthorized());
        app.MapGet("menu", async (ISessionApi sessions, CancellationToken ct) => Results.Ok(await sessions.MenuAsync(ct)));

        // ----- Lists, rules and the partner -------------------------------------
        app.MapGet("reference", async (IReferenceApi reference, CancellationToken ct) => Results.Ok(await reference.ReferenceAsync(ct)));
        app.MapGet("config", async (IReferenceApi reference, CancellationToken ct) => Results.Ok(await reference.ConfigAsync(ct)));
        app.MapGet("me", async (IPartnerApi partners, CancellationToken ct) => Results.Ok(await partners.MeAsync(ct)));
        app.MapPost("deposits/quote", async (QuoteRequest body, IDepositApi deposits, CancellationToken ct) =>
            Results.Ok(await deposits.QuoteAsync(body, ct)));
        app.MapGet("ifsc/{code}", async (string code, IDepositApi deposits, CancellationToken ct) =>
            Found(await deposits.BranchAsync(code, ct)));
        app.MapGet("ifsc", async (string? q, IDepositApi deposits, CancellationToken ct) =>
            Results.Ok(await deposits.SearchBranchesAsync(q ?? "", ct)));
        app.MapGet("pincodes/{pin}", async (string pin, IPlaceApi places, CancellationToken ct) => Found(await places.PinCodeAsync(pin, ct)));
        // A live backend has no test data to show: the pages leave their Test data cards out.
        app.MapGet("demo/cases", () => Results.NotFound());
        app.MapGet("demo/banks", () => Results.NotFound());

        // ----- Investors -------------------------------------------------------
        app.MapGet("investors/folios", async (string? pan, IInvestorApi investors, CancellationToken ct) =>
            Results.Ok(await investors.FoliosByPanAsync(pan ?? "", ct)));
        app.MapGet("investors/folios/{folio}", async (string folio, IInvestorApi investors, CancellationToken ct) =>
            Found(await investors.FolioAsync(folio, ct)));

        // ----- Renewals ----------------------------------------------------------
        app.MapGet("deposits", async (string? pan, string? dob, IRenewalApi renewals, CancellationToken ct) =>
            Found(await renewals.DepositsByPanAsync(pan ?? "", dob ?? "", ct)));
        app.MapGet("deposits/{number}", async (string number, IRenewalApi renewals, CancellationToken ct) =>
            Found(await renewals.DepositAsync(number, ct)));
        app.MapGet("folios/{folio}/deposits", async (string folio, IRenewalApi renewals, CancellationToken ct) =>
            Found(await renewals.DepositsByFolioAsync(folio, ct)));
        app.MapPost("renewals", async (RenewalBody body, IRenewalApi renewals, CancellationToken ct) =>
            await renewals.StartAsync(body.DepositNumber, ct) is { } opened ? Results.Ok(opened) : Results.Conflict());

        // ----- Registers ---------------------------------------------------------
        app.MapGet("sourcing/brokers", async (string? q, ISourcingApi sourcing, CancellationToken ct) =>
            Results.Ok(q is null ? await sourcing.BrokersAsync(ct) : await sourcing.SearchBrokersAsync(q, ct)));
        app.MapGet("sourcing/staff", async (string? q, ISourcingApi sourcing, CancellationToken ct) =>
            Results.Ok(q is null ? await sourcing.StaffAsync(ct) : await sourcing.SearchStaffAsync(q, ct)));
        app.MapGet("payin-slips", async (IPayInSlipApi slips, CancellationToken ct) => Results.Ok(await slips.SlipsAsync(ct)));
        app.MapPost("payin-slips/{appNo}", async (string appNo, IPayInSlipApi slips, CancellationToken ct) =>
            Found(await slips.GenerateAsync(appNo, ct)));
        app.MapGet("links", async (ILinkApi links, CancellationToken ct) => Results.Ok(await links.SentAsync(ct)));
        app.MapPost("links", async (LinkBody body, ILinkApi links, CancellationToken ct) =>
            Found(await links.SendAsync(body.AppNo, body.Purpose, ct)));
        app.MapGet("links/pending", async (ILinkApi links, CancellationToken ct) => Results.Ok(await links.PendingAsync(ct)));

        // ----- Console -----------------------------------------------------------
        // Everyone reads the schedule; only a partner whose menu opens Console Admin changes it.
        app.MapGet("console/schedule", async (IConsoleApi console, CancellationToken ct) => Results.Ok(await console.ScheduleAsync(ct)));
        var admin = app.MapGroup("console").AddEndpointFilter(async (context, next) =>
            await context.HttpContext.RequestServices.GetRequiredService<UnoTP.Api.Data.SqlPartners>().HasFeatureAsync("admin", context.HttpContext.RequestAborted)
                ? await next(context)
                : Results.StatusCode(StatusCodes.Status403Forbidden));
        admin.MapPost("windows", async (NewWindow body, IConsoleApi console, CancellationToken ct) =>
            Results.Ok(await console.AddWindowAsync(body, ct)));
        admin.MapPost("announcements", async (NewAnnouncement body, IConsoleApi console, CancellationToken ct) =>
            Results.Ok(await console.AddAnnouncementAsync(body, ct)));
        admin.MapPost("windows/{id}/end", async (string id, IConsoleApi console, CancellationToken ct) =>
            await console.EndWindowAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        admin.MapDelete("announcements/{id}", async (string id, IConsoleApi console, CancellationToken ct) =>
            await console.RemoveAnnouncementAsync(id, ct) ? Results.NoContent() : Results.NotFound());
    }

    private static IResult Found<T>(T? value) => value is null ? Results.NotFound() : Results.Ok(value);
}
