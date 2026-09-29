using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UnoTP.Backend;

namespace UnoTP.Features;

/// <summary>
/// The backend's slow-changing answers - registers, bank branches, quotes, the
/// console's schedule, who the partner is - kept in memory for as long as each
/// stays true, around whichever client answers (the HTTP backend or the mock).
/// Every page reads them through the same interfaces, so none of them knows.
///
/// Nothing is kept that is not found, and no failure is kept: the next request asks
/// again. Every entry counts one against the cache's size limit (Program.cs), so
/// searches typed by partners cannot grow it without end.
/// </summary>
public static class CachedBackend
{
    /// <summary>Wraps the backend's clients, registered already, in their caches.</summary>
    public static IServiceCollection AddBackendCaching(this IServiceCollection services)
    {
        services.Decorate<ISourcingApi>((inner, sp) => new CachedSourcingApi(inner, Cache(sp), Minutes(sp)));
        services.Decorate<IDepositApi>((inner, sp) => new CachedDepositApi(inner, Cache(sp)));
        services.Decorate<IConsoleApi>((inner, sp) => new CachedConsoleApi(inner, Cache(sp)));
        services.Decorate<IDemoApi>((inner, sp) => new CachedDemoApi(inner, Cache(sp), Minutes(sp)));
        services.Decorate<IPartnerApi>((inner, sp) => new CachedPartnerApi(inner, Cache(sp), sp.GetRequiredService<IPartner>()));
        return services;
    }

    private static IMemoryCache Cache(IServiceProvider sp) => sp.GetRequiredService<IMemoryCache>();

    // Backend:ReferenceCacheMinutes, as the lists and rules are kept (see Lookups).
    private static TimeSpan Minutes(IServiceProvider sp) =>
        TimeSpan.FromMinutes(Math.Max(0, sp.GetRequiredService<IOptions<BackendOptions>>().Value.ReferenceCacheMinutes));

    /// <summary>What is kept under <paramref name="key"/>, or the answer asked for and kept - unless it is null.</summary>
    public static async Task<T> KeptAsync<T>(this IMemoryCache cache, object key, TimeSpan keptFor, Func<Task<T>> ask)
    {
        if (cache.TryGetValue(key, out T? kept) && kept is not null) return kept;
        var answer = await ask();
        if (answer is not null && keptFor > TimeSpan.Zero)
            cache.Set(key, answer, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = keptFor, Size = 1 });
        return answer;
    }

    // Puts a wrapper around the service as it is registered, keeping its lifetime.
    private static void Decorate<T>(this IServiceCollection services, Func<T, IServiceProvider, T> wrap) where T : class
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not registered, so it cannot be cached.");
        services.Remove(original);
        services.Add(new ServiceDescriptor(typeof(T), sp => wrap((T)Inner(sp, original), sp), original.Lifetime));
    }

    private static object Inner(IServiceProvider sp, ServiceDescriptor d) =>
        d.ImplementationInstance ?? d.ImplementationFactory?.Invoke(sp)
        ?? ActivatorUtilities.CreateInstance(sp, d.ImplementationType!);
}

/// <summary>The brokers and staff registers: the same for everyone, changed a few times a day.</summary>
internal sealed class CachedSourcingApi(ISourcingApi inner, IMemoryCache cache, TimeSpan keptFor) : ISourcingApi
{
    public Task<IReadOnlyList<Party>> BrokersAsync(CancellationToken ct = default) =>
        cache.KeptAsync("backend:brokers", keptFor, () => inner.BrokersAsync(ct));

    public Task<IReadOnlyList<Party>> StaffAsync(CancellationToken ct = default) =>
        cache.KeptAsync("backend:staff", keptFor, () => inner.StaffAsync(ct));
}

/// <summary>
/// Bank branches, the branch search and deposit quotes. A bank or branch added to
/// the backend shows at once: an IFSC not found, and a search that finds nothing,
/// are never kept. What is found is kept only briefly - a branch an IFSC names an
/// hour, a search's suggestions five minutes - so a change to a known branch shows
/// within that. A quote is the card rate for the amount, tenure, payout and category
/// - nothing about the investor - kept a minute and never past the day's rate card.
/// </summary>
internal sealed class CachedDepositApi(IDepositApi inner, IMemoryCache cache) : IDepositApi
{
    public Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default) =>
        cache.KeptAsync(("backend:quote", DateTime.Today, request), TimeSpan.FromMinutes(1), () => inner.QuoteAsync(request, ct));

    public Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default) =>
        cache.KeptAsync(("backend:ifsc", ifsc.Trim().ToUpperInvariant()), TimeSpan.FromHours(1), () => inner.BranchAsync(ifsc, ct));

    public async Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default)
    {
        var key = ("backend:banks", query.Trim().ToLowerInvariant());
        if (cache.TryGetValue(key, out IReadOnlyList<BankBranch>? kept) && kept is not null) return kept;
        var found = await inner.SearchBranchesAsync(query, ct);
        // Nothing found is not kept: a bank added a moment later is found by the next search.
        if (found.Count > 0)
            cache.Set(key, found, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), Size = 1 });
        return found;
    }
}

/// <summary>
/// The console's schedule, read for the bell and the tiles on every page. Kept for
/// half a minute, and dropped the moment this app changes it, so an administrator
/// sees their own change at once and other partners within the half minute.
/// </summary>
internal sealed class CachedConsoleApi(IConsoleApi inner, IMemoryCache cache) : IConsoleApi
{
    private const string Key = "backend:console";

    public Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default) =>
        cache.KeptAsync(Key, TimeSpan.FromSeconds(30), () => inner.ScheduleAsync(ct));

    public async Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default)
    {
        try { return await inner.AddWindowAsync(window, ct); } finally { cache.Remove(Key); }
    }

    public async Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default)
    {
        try { return await inner.AddAnnouncementAsync(announcement, ct); } finally { cache.Remove(Key); }
    }

    public async Task<bool> EndWindowAsync(string id, CancellationToken ct = default)
    {
        try { return await inner.EndWindowAsync(id, ct); } finally { cache.Remove(Key); }
    }

    public async Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default)
    {
        try { return await inner.RemoveAnnouncementAsync(id, ct); } finally { cache.Remove(Key); }
    }
}

/// <summary>The demo's test records, which change only with a deploy.</summary>
internal sealed class CachedDemoApi(IDemoApi inner, IMemoryCache cache, TimeSpan keptFor) : IDemoApi
{
    public Task<DemoCases?> CasesAsync(CancellationToken ct = default) =>
        cache.KeptAsync("backend:demo-cases", keptFor, () => inner.CasesAsync(ct));

    public Task<DemoBanks?> BanksAsync(CancellationToken ct = default) =>
        cache.KeptAsync("backend:demo-banks", keptFor, () => inner.BanksAsync(ct));
}

/// <summary>
/// Who the partner is (GET me), for the top bar on every page. Kept five minutes
/// for their own session only - the key is the user and the session the backend
/// started - so it goes with a new session, and is never another user's.
/// </summary>
internal sealed class CachedPartnerApi(IPartnerApi inner, IMemoryCache cache, IPartner partner) : IPartnerApi
{
    public Task<PartnerProfile> MeAsync(CancellationToken ct = default) =>
        partner.SessionId is { Length: > 0 } session
            ? cache.KeptAsync(("backend:me", partner.Id, session), TimeSpan.FromMinutes(5), () => inner.MeAsync(ct))
            : inner.MeAsync(ct);
}
