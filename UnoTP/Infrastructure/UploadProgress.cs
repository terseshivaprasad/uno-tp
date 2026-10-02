using System.Collections.Concurrent;

namespace UnoTP.Infrastructure;

/// <summary>
/// What an upload is doing right now, so the wait on the screen can say it. One
/// upload is several outside services asked one after another - identification,
/// OCR, the issuer or NSDL, the PAN-Aadhaar link, the face match, then filing - and
/// each can take seconds. The model says which it is on as it goes; the page asks
/// once a second while it waits (see partial-forms.js) and shows the answer.
/// Kept in memory, by session and application: it is a line of text, gone when the
/// upload ends.
/// </summary>
public sealed class UploadProgress
{
    /// <summary>A stage older than this is taken as left behind by a request that ended badly, not as under way.</summary>
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, (string Stage, DateTime At)> stages = new();

    /// <summary>Says what the upload is doing now.</summary>
    public void Say(string session, string appNo, string stage) => stages[Key(session, appNo)] = (stage, DateTime.UtcNow);

    /// <summary>What the upload is doing now; null when none is under way.</summary>
    public string? Of(string session, string appNo)
    {
        if (!stages.TryGetValue(Key(session, appNo), out var now)) return null;
        if (DateTime.UtcNow - now.At > Stale) return null;
        return now.Stage;
    }

    /// <summary>The upload is over: there is nothing more to say.</summary>
    public void Done(string session, string appNo) => stages.TryRemove(Key(session, appNo), out _);

    /// <summary>Whose upload it is: the session the backend started, else the user.</summary>
    public static string SessionOf(ISession session) => session.BackendSession() ?? session.Owner();

    private static string Key(string session, string appNo) => $"{session}|{appNo}";
}
