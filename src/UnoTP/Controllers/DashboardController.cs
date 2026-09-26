using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// The classic dashboard: the partner's own work waiting on them, beside the tiles
/// that start new work. <c>off</c> names a feature FeatureGate closed on the way here.
/// </summary>
public class DashboardController(
    FeatureSet features,
    ConsoleState console,
    IApplicationApi applications,
    ILinkApi links,
    IPayInSlipApi slips,
    Lookups lookups,
    ILogger<DashboardController> log) : Controller
{
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Index(string? off)
    {
        var board = console.BoardAsync();
        var work = WorkAsync();
        return View(new DashboardViewModel(features, await board, off) { Work = await work });
    }

    // What is waiting on the partner, from the lists the backend keeps: drafts to
    // finish, investors yet to pay or accept, slips to generate, applications close
    // to cancelling. Only what the partner's features open; a list the backend does
    // not answer is left out rather than failing the page.
    private async Task<IReadOnlyList<WorkItem>> WorkAsync()
    {
        var on = features.Flags;
        var config = await lookups.ConfigAsync();
        var today = DateTime.Today;
        var drafts = on.NewFd ? Try(() => applications.DraftsAsync()) : Skip<DraftSummary>();
        var pending = on.ShortUrl ? Try(() => links.PendingAsync()) : Skip<PendingRecord>();
        var slipRows = on.PisGeneration ? Try(() => slips.SlipsAsync()) : Skip<SlipRecord>();
        var apps = on.ViewApplication ? Try(() => applications.ListAsync()) : Skip<ApplicationRecord>();
        var work = new List<WorkItem>();

        if (await drafts is { Count: > 0 } d)
            work.Add(new WorkItem("drafts", "Drafts to finish", d.Count,
                $"Latest: {d[0].Name} · {Money.Rupees(d[0].Amount)}", "InvestorIdentification", "amber"));

        if (await pending is { Count: > 0 } p)
        {
            var pay = p.Count(x => x.Due == "payment");
            work.Add(new WorkItem("links", "Waiting on the investor", p.Count,
                string.Join(" · ", new[] { (pay, "to pay"), (p.Count - pay, "to accept") }.Where(x => x.Item1 > 0).Select(x => $"{x.Item1} {x.Item2}")),
                "ShortUrl", "blue"));
        }

        if (await slipRows is { } s && s.Count(x => x.State == "pending" && (!x.Digital || x.Accepted)) is var toMake and > 0)
            work.Add(new WorkItem("slips", "Pay-in slips to generate", toMake, "Cheques and DDs to pay in at Axis", "PayInSlip", "blue"));

        if (await apps is { } a)
        {
            var closing = a.Where(x => x.State is not ("booked" or "cancelled"))
                .Select(x => (App: x, Left: config.CancellationDays - (today - x.Applied.Date).Days))
                .Where(x => x.Left is >= 0 and <= 3)
                .OrderBy(x => x.Left).ToList();
            if (closing.Count > 0)
                work.Add(new WorkItem("closing", "Close to auto-cancel", closing.Count,
                    $"Soonest: {closing[0].App.AppNo}, {(closing[0].Left <= 1 ? "today" : $"in {closing[0].Left} days")}",
                    "ViewApplication", "red"));
        }
        return work;
    }

    private static Task<IReadOnlyList<T>> Skip<T>() => Task.FromResult<IReadOnlyList<T>>([]);

    private async Task<IReadOnlyList<T>> Try<T>(Func<Task<IReadOnlyList<T>>> ask)
    {
        try
        {
            return await ask();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(e, "A work list for the dashboard could not be read; it is left out.");
            return [];
        }
    }
}
