namespace UnoTp.Models;

/// <summary>
/// A feature of the classic console that can be taken off the air for a while.
/// </summary>
/// <param name="Key">What a window names when it disables the feature.</param>
/// <param name="Name">The feature as the dashboard labels it.</param>
/// <param name="Group">Where the partner meets it.</param>
/// <param name="Detail">What stops working while it is off.</param>
/// <param name="Off">Set when the feature is off indefinitely rather than for a
/// window, e.g. while it is rebuilt. Such a feature takes no window.</param>
public record ConsoleFeature(string Key, string Name, string Group, string Detail, string? Off = null);

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
/// What the console's administrator controls: which features are on, and what
/// the partner is told is coming. The bell in the top bar is written from here,
/// so an announcement and the window it belongs to can never drift apart.
/// </summary>
public static class ConsoleAdmin
{
    // No sign-in in this mock, so the console is opened as an administrator.
    // Everything gated on this is hidden from a partner who is not one.
    //
    // Off for now: the dashboard drops its Administration tiles and the admin
    // page sends anyone who asks for it back to the dashboard. Set it to true to
    // bring both back.
    public static bool IsAdmin => false;

    public const string Administrator = "Shivaprasad Terse";

    // The classic dashboard's own tiles, in the order it lays them out. Nothing
    // else belongs here: a service behind the wizard is not something this
    // screen can switch, and is told about with a notice instead. A tile that is
    // off indefinitely carries its own reason and is never given a window.
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
            "Where an application stands, holder by holder.", Off: "Under revamp"),
        new("renew", "Renew FD", "FD Services",
            "Rolling a maturing deposit over into a new one."),
    };

    public static ConsoleFeature Feature(string key) => Features.First(f => f.Key == key);

    private static DateTime Today => DateTime.Today;

    // The half hour the live window is measured from, so it straddles now
    // whenever the mock is opened and still reads as a time somebody set.
    private static DateTime Anchor
    {
        get
        {
            var now = DateTime.Now;
            return now.Date.AddHours(now.Hour).AddMinutes(now.Minute < 30 ? 0 : 30);
        }
    }

    // The windows on the books. One feature is in at most one of them at a time,
    // so a feature's state is always one window's doing.
    public static List<FeatureWindow> Windows { get; } = new()
    {
        // Renew FD is on again: this window has run its course, so the tile is
        // clickable and the admin screen keeps it as a window that has ended.
        new("W-2609-01", new[] { "renew" },
            Anchor.AddDays(-1).AddHours(-2), Anchor.AddDays(-1).AddHours(1),
            // Short, inside working hours and on one tile, so the bell was left
            // alone: the tile said it was off and when it was back.
            "",
            "R. Deshpande", Anchor.AddDays(-2)),

        new("W-2709-01", new[] { "new-fd", "short-url" },
            Today.AddDays(4).AddHours(23), Today.AddDays(5).AddHours(2),
            "New FD booking and investor links are unavailable on Sunday night",
            Administrator, Today.AddDays(-2).AddHours(11)),

        new("W-0310-01", new[] { "pis" },
            Today.AddDays(10).AddHours(6), Today.AddDays(10).AddHours(7),
            "Axis pay-in slip generation is down for an hour",
            Administrator, Today.AddDays(-4).AddHours(9)),
    };

    // A notice with nothing to disable: something the partner should know is
    // coming, told at one moment rather than over a window.
    public record Standalone(string Id, string Kind, string Title, DateTime At, string Detail, string SetBy, DateTime SetOn);

    public static List<Standalone> Announcements { get; } = new()
    {
        new("N-2909-01", "Maintenance", "DMS document upload and view pause",
            Today.AddDays(6).AddHours(22),
            "Uploads made during the window queue in the browser and are filed once DMS is back. Nothing has to be uploaded again.",
            "R. Deshpande", Today.AddDays(-1).AddHours(16)),

        new("N-0110-01", "Rate change", "FD rates change from 1 October",
            Today.AddDays(8),
            "Samruddhi 36-month moves from 7.85% to 8.05%, and the senior citizen top-up stays at 0.25%. An application booked and paid before the change keeps the old rate card.",
            Administrator, Today.AddDays(-6).AddHours(15)),
    };

    // The window a feature is in now, or the next one it is in. A feature that is
    // off indefinitely has none.
    public static FeatureWindow? WindowFor(string key) =>
        Windows.Where(w => w.Features.Contains(key) && !w.Ended)
            .OrderBy(w => w.From)
            .FirstOrDefault();

    // "available", "off" for one that is off indefinitely, "disabled" while a
    // window is running, "scheduled" when one is on the books.
    public static string StateOf(string key)
    {
        if (Feature(key).Off is not null) return "off";
        var window = WindowFor(key);
        if (window is null) return "available";
        return window.Live ? "disabled" : "scheduled";
    }

    // What a dashboard tile says in place of being clickable, or null while the
    // feature is on. A window that has not started yet takes nothing away.
    public static string? OffLabel(string key)
    {
        var feature = Feature(key);
        if (feature.Off is not null) return feature.Off;
        var window = WindowFor(key);
        return window is { Live: true } ? $"Back at {Clock(window.To)}" : null;
    }

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

    // What the bell shows: the announced windows and the standalone notices
    // together, soonest first. Anything already past drops off by itself.
    public static List<Notice> Bell() =>
        Windows.Where(w => w.Announced && !w.Ended)
            .Select(w => (At: w.From, Notice: new Notice("Downtime", Tone("Downtime"), w.Notice, w.When, w.Away, w.Affects, w.Id, w.From)))
            .Concat(Announcements
                .Where(a => a.At.Date >= Today)
                .Select(a => (At: a.At, Notice: new Notice(a.Kind, Tone(a.Kind), a.Title, Stamp(a.At), Away(a.At), a.Detail, a.Id, a.At))))
            .OrderBy(x => x.At)
            .Select(x => x.Notice)
            .ToList();
}
