using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UnoTP.Backend;

namespace UnoTP.Features;

/// <summary>
/// The backend's lists and rules, as every page reads them. They change seldom and
/// every page load needs them, so they are kept for a few minutes
/// (<c>Backend:ReferenceCacheMinutes</c>, default 10) rather than asked for each time.
/// A failure is not kept: the next request asks again.
/// </summary>
public sealed class Lookups(IReferenceApi reference, IMemoryCache cache, IOptions<BackendOptions> backend)
{
    private TimeSpan KeptFor => TimeSpan.FromMinutes(Math.Max(0, backend.Value.ReferenceCacheMinutes));

    /// <summary>Every list the pages offer.</summary>
    public Task<ReferenceData> ReferenceAsync(CancellationToken ct = default) =>
        Kept("backend:reference", () => reference.ReferenceAsync(ct));

    /// <summary>The limits and rules the pages check against.</summary>
    public Task<AppConfig> ConfigAsync(CancellationToken ct = default) =>
        Kept("backend:config", () => reference.ConfigAsync(ct));

    private async Task<T> Kept<T>(string key, Func<Task<T>> ask)
    {
        if (cache.TryGetValue(key, out T? kept) && kept is not null) return kept;
        var answer = await ask();
        if (KeptFor > TimeSpan.Zero) cache.Set(key, answer, KeptFor);
        return answer;
    }
}
