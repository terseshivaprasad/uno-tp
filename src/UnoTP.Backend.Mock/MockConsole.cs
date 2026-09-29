namespace UnoTP.Backend.Mock;

/// <summary>What the console's administrator has on the books.</summary>
public sealed class MockConsole(IPartnerApi partners) : IConsoleApi
{
    public Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default)
    {
        // A window ended while on runs to the moment it was ended; one cancelled before it began is gone.
        var windows = AddedWindows
            .Select(w => Ended.TryGetValue(w.Id, out var at) ? (at > w.From ? w with { To = at } : null) : w)
            .OfType<WindowRecord>().ToList();
        var announcements = Standing.Concat(AddedAnnouncements).Where(a => !Removed.ContainsKey(a.Id)).ToList();
        return Task.FromResult(new ConsoleSchedule(windows, announcements));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> Ended = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Removed = new();

    public async Task<bool> EndWindowAsync(string id, CancellationToken ct = default) =>
        (await ScheduleAsync(ct)).Windows.Any(w => w.Id == id && w.To > DateTime.Now) && Ended.TryAdd(id, DateTime.Now);

    public async Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default) =>
        (await ScheduleAsync(ct)).Announcements.Any(a => a.Id == id) && Removed.TryAdd(id, true);

    // One notice stands while Application Status is off: why, and what to use in
    // the meantime. Told as of today, so it stays in the bell until the
    // administrator takes it down (it comes down with the tile's return).
    private static IEnumerable<AnnouncementRecord> Standing =>
    [
        new("N-STATUS", "Maintenance", "Application Status is being rebuilt", DateTime.Today.AddHours(9),
            "The Application status tile is off while its page is rebuilt on the new design, reading the same statuses from the backend. "
            + "Until it is back, an application's progress is on View Application, which opens each one read-only. This notice comes down when the tile returns.",
            "Uno TP", DateTime.Today),
    ];

    // What was scheduled while the app runs, set by the partner signed in. Beyond
    // the standing notice, the mock opens with nothing on the books: no made-up
    // downtime or notices in the bell.
    private static readonly List<WindowRecord> AddedWindows = [];
    private static readonly List<AnnouncementRecord> AddedAnnouncements = [];
    private static int addedSeq = 10;

    public async Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        lock (AddedWindows)
        {
            var record = new WindowRecord($"W-{window.From:ddMM}-{++addedSeq:D2}", window.Features, window.From, window.To, window.Notice, by, DateTime.Now);
            AddedWindows.Add(record);
            return record;
        }
    }

    public async Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        lock (AddedAnnouncements)
        {
            var record = new AnnouncementRecord($"N-{announcement.At:ddMM}-{++addedSeq:D2}", announcement.Kind, announcement.Title, announcement.At, announcement.Detail, by, DateTime.Now);
            AddedAnnouncements.Add(record);
            return record;
        }
    }
}
