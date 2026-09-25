using Microsoft.Extensions.Logging;
using UnoTP.Backend;
using UnoTP.Features;

namespace UnoTP.Models;

/// <summary>
/// A feature of the classic console that can be taken off the air for a while.
/// </summary>
/// <param name="Key">What a window names when it disables the feature.</param>
/// <param name="Name">The feature as the dashboard labels it.</param>
/// <param name="Group">Where the partner meets it.</param>
/// <param name="Detail">What stops working while it is off.</param>
/// <param name="OffReason">What its tile says while it is switched off in the
/// "Features" section of appsettings, e.g. while it is rebuilt. A feature that is
/// switched off takes no window.</param>
public record ConsoleFeature(string Key, string Name, string Group, string Detail, string OffReason = "Unavailable");

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

    // What the bell says under the notice: the screens that stop working, which
    // the window knows and the line partners were shown may not say.
    public string Affects =>
        "Affects " + ConsoleAdmin.Names(Features) + ".";

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
/// The console's features, and how the screens write the times and names that
/// go with them. What is scheduled against the features comes from the backend
/// (see <see cref="ConsoleBoard"/>).
/// </summary>
public static class ConsoleAdmin
{
    // The classic dashboard's own tiles, in the order it lays them out. Nothing
    // else belongs here: a service behind the wizard is not something this
    // screen can switch, and is told about with a notice instead. Whether each is
    // switched on at all comes from the "Features" section of appsettings; the
    // backend's windows only take a switched-on feature off for a while.
    public static readonly ConsoleFeature[] Features =
    {
        new("new-fd", "Create New FD", "Apply for a new FD",
            "The booking wizard end to end, from investor search to submission."),
        new("pis", "PIS Generation — Axis", "Apply for a new FD",
            "Making and reprinting Axis pay-in slips. A slip already printed stays valid."),
        new("view-app", "View existing application", "Apply for a new FD",
            "Looking an application up by number, folio or date."),
        new("short-url", "Short URL", "Apply for a new FD",
            "The payment and acceptance links sent to investors. A link already sent stops opening."),
        new("app-status", "Application status", "FD Services",
            "Where an application stands, holder by holder.", OffReason: "Under revamp"),
        new("renew", "Renew FD", "FD Services",
            "Rolling a maturing deposit over into a new one.", OffReason: "Coming soon"),
    };

    public static ConsoleFeature Feature(string key) => Features.First(f => f.Key == key);

    // The name a feature goes by, including the admin screen, which is switched
    // like a feature but is not one of the partner's tiles.
    public static string NameOf(string key) =>
        Features.FirstOrDefault(f => f.Key == key)?.Name ?? (key == "admin" ? "Console Admin" : key);

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

    // The features a window names, written out as a sentence lists them.
    public static string Names(IReadOnlyList<string> keys)
    {
        var names = keys.Select(k => Feature(k).Name).ToList();
        return names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
    }
}

/// <summary>
/// What the console's administrator has scheduled, as the backend holds it: the
/// windows that take features off, and the notices that stand on their own. The
/// bell in the top bar is written from here, so an announcement and the window it
/// belongs to can never drift apart.
/// </summary>
public sealed class ConsoleBoard(IReadOnlyList<FeatureWindow> windows, IReadOnlyList<AnnouncementRecord> announcements)
{
    public static readonly ConsoleBoard Empty = new([], []);

    public IReadOnlyList<FeatureWindow> Windows { get; } = windows;

    public IReadOnlyList<AnnouncementRecord> Announcements { get; } = announcements;

    public static ConsoleBoard From(ConsoleSchedule schedule) =>
        new(schedule.Windows.Select(w => new FeatureWindow(w.Id, w.Features, w.From, w.To, w.Notice, w.SetBy, w.SetOn)).ToList(),
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
        var feature = ConsoleAdmin.Features.FirstOrDefault(f => f.Key == key);
        if (!flags.IsOn(key)) return feature?.OffReason ?? "Unavailable";
        var window = feature is null ? null : WindowFor(key);
        return window is { Live: true } ? $"Back at {ConsoleAdmin.Clock(window.To)}" : null;
    }

    // What the bell shows: the announced windows and the standalone notices
    // together, soonest first. Anything already past drops off by itself.
    public List<Notice> Bell() =>
        Windows.Where(w => w.Announced && !w.Ended)
            .Select(w => (At: w.From, Notice: new Notice("Downtime", ConsoleAdmin.Tone("Downtime"), w.Notice, w.When, w.Away, w.Affects, w.Id, w.From)))
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
public sealed class ConsoleState(IConsoleApi api, ILogger<ConsoleState> log)
{
    private Task<ConsoleBoard>? board;

    public Task<ConsoleBoard> BoardAsync() => board ??= Load();

    private async Task<ConsoleBoard> Load()
    {
        try
        {
            return ConsoleBoard.From(await api.ScheduleAsync());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(e, "The console schedule could not be read; showing no windows or notices.");
            return ConsoleBoard.Empty;
        }
    }
}
