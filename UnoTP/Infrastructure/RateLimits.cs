using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace UnoTP.Infrastructure;

/// <summary>
/// How many requests one caller may make in a minute, from the "RateLimits" section
/// of appsettings. 0 takes a limit off.
/// </summary>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    /// <summary>The way in from the portal, from one address.</summary>
    public int EntryPerMinute { get; set; } = 60;

    /// <summary>
    /// Tries at one holder's document of one type - their PAN copy, their proof of
    /// address, the cheque - on one application, in one session. Each try may be charged for.
    /// </summary>
    public int PerDocumentPerMinute { get; set; } = 3;

    /// <summary>
    /// Minutes a document waits once its copies have been refused the most times in a
    /// row the rules allow (maxAttempts), before it may be tried again.
    /// </summary>
    public int RefusedWaitMinutes { get; set; } = 15;
}

/// <summary>
/// The limit on the way in, counted by the caller's address. A request over it is
/// sent to the Too Many Requests page: there is no page of the app to say it on yet.
/// The limit on documents is <see cref="DocumentLimiter"/>.
/// </summary>
public static class RateLimits
{
    public const string Entry = "entry";

    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RateLimitOptions>(config.GetSection(RateLimitOptions.Section));
        services.AddSingleton<DocumentLimiter>();

        var limits = config.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Entry, context => PerMinute(ClientAddress(context), limits.EntryPerMinute));

            options.OnRejected = (rejected, ct) =>
            {
                var http = rejected.HttpContext;
                http.Response.Redirect(http.Request.PathBase + "/Home/TooManyRequests");
                return ValueTask.CompletedTask;
            };
        });
        return services;
    }

    /// <summary>One window a minute for each caller; nothing waits in a queue.</summary>
    public static RateLimitPartition<string> PerMinute(string caller, int perMinute)
    {
        if (perMinute <= 0) return RateLimitPartition.GetNoLimiter(caller);
        return RateLimitPartition.GetFixedWindowLimiter(caller, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = perMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }

    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>
/// The limit on documents, counted by holder and document type within one session
/// and one application, a minute at a time. A holder's PAN copy tried too often does
/// not stop their proof of address, or another holder's PAN copy, or another
/// application, or anybody else's session. The page says a refusal in a popup, and
/// the try costs nothing: it reaches no outside service.
/// </summary>
public sealed class DocumentLimiter(IOptions<RateLimitOptions> options) : IDisposable
{
    private readonly int perMinute = options.Value.PerDocumentPerMinute;

    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(
        counted => RateLimits.PerMinute(counted, options.Value.PerDocumentPerMinute));

    /// <summary>
    /// Whether one more try at a holder's document of this type is within this
    /// minute's limit for the session and the application. A try allowed is counted.
    /// </summary>
    /// <param name="holder">01 the investor, 02 the second holder, 03 the third.</param>
    /// <param name="documentType">pan, poa, photo, mail, payment ... or the name of a check typed by hand.</param>
    public bool Allows(string session, string appNo, string holder, string documentType)
    {
        using var lease = limiter.AttemptAcquire($"{session}|{appNo}|{holder}|{documentType}");
        return lease.IsAcquired;
    }

    /// <summary>How long a document refused too many times in a row waits before it may be tried again.</summary>
    public TimeSpan RefusedWait { get; } = TimeSpan.FromMinutes(Math.Max(0, options.Value.RefusedWaitMinutes));

    /// <summary>What the page says when a try is over the limit.</summary>
    public string Said =>
        $"That is more than {perMinute} tries in a minute. Wait a minute, then do it again. Nothing was sent this time, and no attempt was used.";

    public void Dispose() => limiter.Dispose();
}
