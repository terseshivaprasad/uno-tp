using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.Infrastructure;

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
        CachedOrAsk("backend:reference", () => reference.ReferenceAsync(ct));

    /// <summary>The limits and rules the pages check against.</summary>
    public Task<AppConfig> ConfigAsync(CancellationToken ct = default) =>
        CachedOrAsk("backend:config", () => reference.ConfigAsync(ct));

    /// <summary>The cached answer for the key, or asks the backend and keeps the answer for a while.</summary>
    private Task<T> CachedOrAsk<T>(string key, Func<Task<T>> ask) => cache.KeptAsync(key, KeptFor, ask);
}
