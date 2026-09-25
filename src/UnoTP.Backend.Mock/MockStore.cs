using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace UnoTP.Backend.Mock;

/// <summary>
/// The mock backend's applications, held in this process's memory. It is lost on
/// a restart and not shared between instances, so it stands in for the backend
/// only while there is none. An application nobody has touched for
/// Upload:KeepHours is let go, so memory cannot grow without bound.
///
/// Each application is kept as the JSON the real backend would send, and every
/// read hands out a fresh copy: a page that changes what it read changes nothing
/// here until it saves, exactly as over HTTP.
/// </summary>
public sealed class MockStore(IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed class Entry(string owner, Application app)
    {
        public string Owner { get; } = owner;
        public object Gate { get; } = new();
        public string Json { get; set; } = JsonSerializer.Serialize(app, MockStore.Json);
        public int Version { get; set; } = app.Version;
        public DateTime Seen { get; set; } = DateTime.UtcNow;
        public Dictionary<string, UploadFile> Copies { get; } = [];
    }

    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan keep = TimeSpan.FromHours(config.GetValue("Upload:KeepHours", 12.0));
    private long lastSweep;

    // A refused copy is kept aside under its own reference; the numbers run on
    // across every application, as the real backend's would.
    private int rejectSeq = 884200;

    /// <summary>Adds the application for its owner. False when the number is taken.</summary>
    public bool TryAdd(string owner, Application app)
    {
        Sweep();
        return entries.TryAdd(app.AppNo, new Entry(owner, app));
    }

    /// <summary>The owner's application under this number, opened from <paramref name="open"/> if it is new.</summary>
    public Application? GetOrAdd(string owner, string appNo, Func<Application> open)
    {
        Sweep();
        var entry = entries.GetOrAdd(appNo, _ => new Entry(owner, open()));
        return Read(entry, owner);
    }

    public Application? Get(string owner, string appNo)
    {
        Sweep();
        return entries.TryGetValue(appNo, out var entry) ? Read(entry, owner) : null;
    }

    public int? SaveUpload(string owner, string appNo, int version, UploadState upload) =>
        Save(owner, appNo, version, app => app.Upload = upload)?.Version;

    /// <summary>
    /// Changes the owner's application, if it is still at <paramref name="version"/>,
    /// and moves it on a version. The application as saved, or null when it changed
    /// in between and nothing was saved.
    /// </summary>
    public Application? Save(string owner, string appNo, int version, Action<Application> change)
    {
        if (!entries.TryGetValue(appNo, out var entry) || entry.Owner != owner) return null;
        lock (entry.Gate)
        {
            if (entry.Version != version) return null;
            var app = JsonSerializer.Deserialize<Application>(entry.Json, Json)!;
            change(app);
            app.Version = ++entry.Version;
            entry.Json = JsonSerializer.Serialize(app, Json);
            entry.Seen = DateTime.UtcNow;
            return JsonSerializer.Deserialize<Application>(entry.Json, Json);
        }
    }

    public void File(string owner, string appNo, string slot, UploadFile copy)
    {
        if (!entries.TryGetValue(appNo, out var entry) || entry.Owner != owner) return;
        lock (entry.Gate) entry.Copies[slot] = copy;
    }

    public void Delete(string owner, string appNo, string slot)
    {
        if (!entries.TryGetValue(appNo, out var entry) || entry.Owner != owner) return;
        lock (entry.Gate) entry.Copies.Remove(slot);
    }

    public UploadFile? Copy(string owner, string appNo, string slot)
    {
        if (!entries.TryGetValue(appNo, out var entry) || entry.Owner != owner) return null;
        lock (entry.Gate) return entry.Copies.GetValueOrDefault(slot);
    }

    public string NextRejectRef() => "REJ-" + Interlocked.Increment(ref rejectSeq);

    // An application is only ever handed to the partner who opened it.
    private static Application? Read(Entry entry, string owner)
    {
        if (entry.Owner != owner) return null;
        lock (entry.Gate)
        {
            entry.Seen = DateTime.UtcNow;
            return JsonSerializer.Deserialize<Application>(entry.Json, Json);
        }
    }

    // At most once a minute, whoever asks next clears out the idle ones.
    private void Sweep()
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref lastSweep);
        if (now - last < TimeSpan.TicksPerMinute || Interlocked.CompareExchange(ref lastSweep, now, last) != last) return;
        var cutoff = DateTime.UtcNow - keep;
        foreach (var (appNo, entry) in entries)
        {
            if (entry.Seen < cutoff) entries.TryRemove(appNo, out _);
        }
    }
}
