using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// The dashboard: the partner's own work waiting on them, beside the tiles
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
        // The partner's own applications opened and left before submitting, to pick
        // up again from here: only while New FD is open, as that is where they go.
        var drafts = features.Flags.NewFd ? OrEmptyWhenUnavailable(() => applications.DraftsAsync()) : NoWorkList<DraftSummary>();
        var work = WorkAsync();
        return View(new DashboardViewModel(features, await board, off) { Work = await work, Drafts = await drafts });
    }

    // What is waiting on the partner, from the lists the backend keeps: investors
    // yet to pay or accept, slips to generate, applications close to cancelling.
    // Only what the partner's features open; a list the backend does not answer is
    // left out rather than failing the page.
    private async Task<IReadOnlyList<WorkItem>> WorkAsync()
    {
        var on = features.Flags;
        var config = await lookups.ConfigAsync();
        var today = DateTime.Today;
        var pending = on.ShortUrl ? OrEmptyWhenUnavailable(() => links.PendingAsync()) : NoWorkList<PendingRecord>();
        var slipRows = on.PisGeneration ? OrEmptyWhenUnavailable(() => slips.SlipsAsync()) : NoWorkList<SlipRecord>();
        var apps = on.ViewApplication ? OrEmptyWhenUnavailable(() => applications.ListAsync()) : NoWorkList<ApplicationRecord>();
        var work = new List<WorkItem>();

        if (await pending is { Count: > 0 } p)
        {
            var pay = p.Count(x => x.Due == "payment");
            work.Add(new WorkItem("links", "Waiting on the investor", p.Count,
                string.Join(" · ", new[] { (pay, "to pay"), (p.Count - pay, "to accept") }.Where(x => x.Item1 > 0).Select(x => $"{x.Item1} {x.Item2}")),
                "Links", "blue"));
        }

        if (await slipRows is { } s && s.Count(x => x.State == "pending" && (!x.Digital || x.Accepted)) is var toMake and > 0)
            work.Add(new WorkItem("slips", "Pay-in slips to generate", toMake, "Cheques and DDs to pay in at Axis", "PayInSlips", "blue"));

        if (await apps is { } a)
        {
            var closing = a.Where(x => x.State is not ("booked" or "cancelled"))
                .Select(x => (App: x, Left: config.CancellationDays - (today - x.Applied.Date).Days))
                .Where(x => x.Left >= 0 && x.Left <= config.CloseToCancelDays)
                .OrderBy(x => x.Left).ToList();
            if (closing.Count > 0)
                work.Add(new WorkItem("closing", "Close to auto-cancel", closing.Count,
                    $"Soonest: {closing[0].App.AppNo}, {(closing[0].Left <= 1 ? "today" : $"in {closing[0].Left} days")}",
                    "Applications", "red"));
        }
        return work;
    }

    /// <summary>An empty list, for a work list that is switched off.</summary>
    private static Task<IReadOnlyList<T>> NoWorkList<T>() => Task.FromResult<IReadOnlyList<T>>([]);

    /// <summary>The list, or an empty one when the backend or database cannot be reached (the dashboard still shows).</summary>
    private async Task<IReadOnlyList<T>> OrEmptyWhenUnavailable<T>(Func<Task<IReadOnlyList<T>>> ask)
    {
        try
        {
            return await ask();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Data.Common.DbException)
        {
            log.LogWarning(e, "A work list for the dashboard could not be read; it is left out.");
            return [];
        }
    }
}
