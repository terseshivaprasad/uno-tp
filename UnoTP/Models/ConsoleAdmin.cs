using Microsoft.Extensions.Logging;
using UnoTP.Services;
using UnoTP.Infrastructure;

namespace UnoTP.Models;

/// <summary>
/// A feature of the console that can be taken off the air for a while, as the
/// backend lists it (reference: features).
/// </summary>
/// <param name="Key">What a window names when it disables the feature, and the menu key that opens it.</param>
/// <param name="Name">The feature as the dashboard labels it.</param>
/// <param name="Group">Where the partner meets it.</param>
/// <param name="Detail">What stops working while it is off.</param>
/// <param name="OffReason">What its tile says while it is switched off in the
/// "Features" section of appsettings, e.g. while it is rebuilt. A feature that is
/// switched off takes no window.</param>
/// <param name="Tile">False for one switched like a feature with no tile of its own (Console Admin).</param>
public record ConsoleFeature(string Key, string Name, string Group, string Detail, string OffReason = "Unavailable", bool Tile = true)
{
    /// <summary>A console feature from its reference-data entry.</summary>
    public static ConsoleFeature From(FeatureOption f) => new(f.Code, f.Name, f.Group, f.Detail, f.OffReason, f.Tile);
}

/// <summary>
/// One stretch of time in which the features it names are off. A window with a
/// notice on it also tells partners, in the top bar's bell, until it ends; a
/// short one inside working hours is usually left without one, and then only
/// the greyed tile says anything.
/// </summary>
public record FeatureWindow(
    string Id,
    IReadOnlyList<string> Features,
    DateTime From,
    DateTime To,
    // The one line partners are shown. Empty when the window is not announced.
    string Notice,
    string SetBy,
    DateTime SetOn)
{
    public bool Live => DateTime.Now >= From && DateTime.Now < To;

    public bool Ended => DateTime.Now >= To;

    public bool Announced => Notice.Length > 0;

    public string State => Live ? "live" : Ended ? "ended" : "scheduled";

    // "Sun 27 Sep, 11:00 PM – Mon 28 Sep, 2:00 AM", or just the closing time
    // when the window opens and closes on one day.
    public string When => From.Date == To.Date
        ? $"{ConsoleAdmin.Stamp(From)} – {ConsoleAdmin.Clock(To)}"
        : $"{ConsoleAdmin.Stamp(From)} – {ConsoleAdmin.Stamp(To)}";

    public string Away => Live ? "on now" : ConsoleAdmin.Away(From);

    // How long it lasts, said the way the announcement says it.
    public string Length
    {
        get
        {
            var span = To - From;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes";
            if (span.TotalHours < 24)
            {
                var hours = span.TotalHours;
                return hours == 1 ? "an hour" : $"{(hours % 1 == 0 ? ((int)hours).ToString() : hours.ToString("0.#"))} hours";
            }
            var days = span.TotalDays;
            return days == 1 ? "a day" : $"{days:0.#} days";
        }
    }
}

/// <summary>
/// How the console's screens write the times that go with its windows and
/// notices. The features themselves, and their names, come from the backend
/// (see <see cref="ConsoleBoard"/>).
/// </summary>
public static class ConsoleAdmin
{
    private static DateTime Today => DateTime.Today;

    public static string Stamp(DateTime t) => t.ToString("ddd d MMM, h:mm tt");

    public static string Clock(DateTime t) => t.ToString("h:mm tt");

    // How far off something is, counted in whole days the way a calendar counts
    // them, so a window late tomorrow night is "tomorrow" and not "in 2 days".
    public static string Away(DateTime at)
    {
        var days = (at.Date - Today).Days;
        if (days < 0) return "passed";
        if (days == 1) return "tomorrow";
        if (days > 1) return $"in {days} days";

        var off = at - DateTime.Now;
        if (off <= TimeSpan.Zero) return "on now";
        return off.TotalHours < 1
            ? $"in {Math.Max(1, (int)off.TotalMinutes)} minutes"
            : $"in {(int)off.TotalHours} hours";
    }

    // Which colour the bell writes a notice in. A window is always a downtime:
    // the tile it names is off for as long as it runs.
    public static string Tone(string kind) => kind switch
    {
        "Downtime" => "text-danger",
        "Maintenance" => "text-amber",
        _ => "text-success",
    };
}

/// <summary>
/// What the console's administrator has scheduled, as the backend holds it: the
/// windows that take features off, and the notices that stand on their own. The
/// bell in the top bar is written from here, so an announcement and the window it
/// belongs to can never drift apart.
/// </summary>
public sealed class ConsoleBoard(IReadOnlyList<ConsoleFeature> features, IReadOnlyList<FeatureWindow> windows, IReadOnlyList<AnnouncementRecord> announcements)
{
    /// <summary>The console's features, in the order the dashboard lays them out.</summary>
    public IReadOnlyList<ConsoleFeature> Features { get; } = features;

    /// <summary>The features with a tile of their own on the dashboard.</summary>
    public IEnumerable<ConsoleFeature> Tiles => Features.Where(f => f.Tile);

    public ConsoleFeature? Feature(string key) => Features.FirstOrDefault(f => f.Key == key);

    /// <summary>The name a feature goes by; its key when the backend lists no such feature.</summary>
    public string NameOf(string key) => Feature(key)?.Name ?? key;

    // The features a window names, written out as a sentence lists them.
    public string Names(IReadOnlyList<string> keys)
    {
        var names = keys.Select(NameOf).ToList();
        return names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
    }

    // What the bell says under a window's notice: the screens that stop working,
    // which the window knows and the line partners were shown may not say.
    public string AffectsOf(FeatureWindow w) => "Affects " + Names(w.Features) + ".";

    public IReadOnlyList<FeatureWindow> Windows { get; } = windows;

    public IReadOnlyList<AnnouncementRecord> Announcements { get; } = announcements;

    /// <summary>The admin board: every feature with the windows and notices scheduled for it.</summary>
    public static ConsoleBoard From(IReadOnlyList<ConsoleFeature> features, ConsoleSchedule schedule) =>
        new(features, schedule.Windows.Select(w => new FeatureWindow(w.Id, w.Features, w.From, w.To, w.Notice, w.SetBy, w.SetOn)).ToList(),
            schedule.Announcements);

    // The window a feature is in now, or the next one it is in. A feature that is
    // off indefinitely has none.
    public FeatureWindow? WindowFor(string key) =>
        Windows.Where(w => w.Features.Contains(key) && !w.Ended)
            .OrderBy(w => w.From)
            .FirstOrDefault();

    // "available", "off" for one switched off in appsettings, "disabled" while a
    // window is running, "scheduled" when one is on the books.
    public string StateOf(string key, FeatureFlags flags)
    {
        if (!flags.IsOn(key)) return "off";
        var window = WindowFor(key);
        if (window is null) return "available";
        return window.Live ? "disabled" : "scheduled";
    }

    // What a dashboard tile says in place of being clickable, or null while the
    // feature is on. A window that has not started yet takes nothing away. The
    // pages behind a feature are closed on the same answer (see FeatureGate).
    public string? OffLabel(string key, FeatureFlags flags)
    {
        var feature = Feature(key);
        if (!flags.IsOn(key)) return feature?.OffReason ?? "Unavailable";
        var window = feature is null ? null : WindowFor(key);
        return window is { Live: true } ? $"Back at {ConsoleAdmin.Clock(window.To)}" : null;
    }

    // What the bell shows: the announced windows and the standalone notices
    // together, soonest first. Anything already past drops off by itself.
    public List<Notice> Bell() =>
        Windows.Where(w => w.Announced && !w.Ended)
            .Select(w => (At: w.From, Notice: new Notice("Downtime", ConsoleAdmin.Tone("Downtime"), w.Notice, w.When, w.Away, AffectsOf(w), w.Id, w.From)))
            .Concat(Announcements
                .Where(a => a.At.Date >= DateTime.Today)
                .Select(a => (At: a.At, Notice: new Notice(a.Kind, ConsoleAdmin.Tone(a.Kind), a.Title, ConsoleAdmin.Stamp(a.At), ConsoleAdmin.Away(a.At), a.Detail, a.Id, a.At))))
            .OrderBy(x => x.At)
            .Select(x => x.Notice)
            .ToList();
}

/// <summary>
/// The console's schedule for this request, read from the backend once however
/// many places ask - the gate, the tiles and the bell all do. Every page shows the
/// bell, so a backend that cannot be reached leaves it empty and the tiles open
/// rather than taking every page down with it.
/// </summary>
public sealed class ConsoleState(IConsoleApi api, UnoTP.Infrastructure.Lookups lookups, ILogger<ConsoleState> log)
{
    private Task<ConsoleBoard>? board;

    public Task<ConsoleBoard> BoardAsync() => board ??= Load();

    private async Task<ConsoleBoard> Load()
    {
        IReadOnlyList<ConsoleFeature> features;
        try
        {
            features = ((await lookups.ReferenceAsync()).Features ?? []).Select(ConsoleFeature.From).ToList();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Data.Common.DbException)
        {
            log.LogWarning(e, "The console's features could not be read; the dashboard shows no tiles.");
            features = [];
        }
        try
        {
            return ConsoleBoard.From(features, await api.ScheduleAsync());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Data.Common.DbException)
        {
            log.LogWarning(e, "The console schedule could not be read; showing no windows or notices.");
            return new ConsoleBoard(features, [], []);
        }
    }
}
