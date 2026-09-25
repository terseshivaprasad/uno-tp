namespace UnoTP.Backend.Mock;

/// <summary>What the console's administrator has on the books.</summary>
public sealed class MockConsole : IConsoleApi
{
    private const string Administrator = "Shivaprasad Terse";

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

    public Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        // A window ended while on runs to the moment it was ended; one cancelled before it began is gone.
        var windows = Windows().Concat(AddedWindows)
            .Select(w => Ended.TryGetValue(w.Id, out var at) ? (at > w.From ? w with { To = at } : null) : w)
            .OfType<WindowRecord>().ToList();
        var announcements = Announcements().Concat(AddedAnnouncements).Where(a => !Removed.ContainsKey(a.Id)).ToList();
        return Task.FromResult(new ConsoleSchedule(windows, announcements));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> Ended = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Removed = new();

    public async Task<bool> EndWindowAsync(string id, CancellationToken ct = default) =>
        (await ScheduleAsync(ct)).Windows.Any(w => w.Id == id && w.To > DateTime.Now) && Ended.TryAdd(id, DateTime.Now);

    public async Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default) =>
        (await ScheduleAsync(ct)).Announcements.Any(a => a.Id == id) && Removed.TryAdd(id, true);

    // What was scheduled while the app runs, kept beside what the mock opens with,
    // and set by the partner signed in.
    private static readonly List<WindowRecord> AddedWindows = [];
    private static readonly List<AnnouncementRecord> AddedAnnouncements = [];
    private static int addedSeq = 10;

    public async Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default)
    {
        var by = (await new MockPartner().MeAsync(ct)).Name;
        lock (AddedWindows)
        {
            var record = new WindowRecord($"W-{window.From:ddMM}-{++addedSeq:D2}", window.Features, window.From, window.To, window.Notice, by, DateTime.Now);
            AddedWindows.Add(record);
            return record;
        }
    }

    public async Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default)
    {
        var by = (await new MockPartner().MeAsync(ct)).Name;
        lock (AddedAnnouncements)
        {
            var record = new AnnouncementRecord($"N-{announcement.At:ddMM}-{++addedSeq:D2}", announcement.Kind, announcement.Title, announcement.At, announcement.Detail, by, DateTime.Now);
            AddedAnnouncements.Add(record);
            return record;
        }
    }

    // The windows on the books. One feature is in at most one of them at a time,
    // so a feature's state is always one window's doing.
    private static List<WindowRecord> Windows() =>
    [
        // Renew FD is on again: this window has run its course, so the tile is
        // clickable and the admin screen keeps it as a window that has ended.
        new("W-2609-01", ["renew"],
            Anchor.AddDays(-1).AddHours(-2), Anchor.AddDays(-1).AddHours(1),
            // Short, inside working hours and on one tile, so the bell was left
            // alone: the tile said it was off and when it was back.
            "",
            "R. Deshpande", Anchor.AddDays(-2)),

        new("W-2709-01", ["new-fd", "short-url"],
            Today.AddDays(4).AddHours(23), Today.AddDays(5).AddHours(2),
            $"New FD booking and investor links are unavailable on {Today.AddDays(4).ToString("dddd", System.Globalization.CultureInfo.InvariantCulture)} night",
            Administrator, Today.AddDays(-2).AddHours(11)),

        new("W-0310-01", ["pis"],
            Today.AddDays(10).AddHours(6), Today.AddDays(10).AddHours(7),
            "Axis pay-in slip generation is down for an hour",
            Administrator, Today.AddDays(-4).AddHours(9)),
    ];

    private static List<AnnouncementRecord> Announcements() =>
    [
        new("N-2909-01", "Maintenance", "DMS document upload and view pause",
            Today.AddDays(6).AddHours(22),
            "Uploads made during the window queue in the browser and are filed once DMS is back. Nothing has to be uploaded again.",
            "R. Deshpande", Today.AddDays(-1).AddHours(16)),

        new("N-0110-01", "Rate change", "FD rates change from 1 October",
            Today.AddDays(8),
            "Samruddhi 36-month moves from 7.85% to 8.05%, and the senior citizen top-up stays at 0.25%. An application booked and paid before the change keeps the old rate card.",
            Administrator, Today.AddDays(-6).AddHours(15)),
    ];
}
