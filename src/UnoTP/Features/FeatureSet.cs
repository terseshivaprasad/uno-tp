using Microsoft.Extensions.Options;

namespace UnoTP.Features;

/// <summary>
/// The features in force for one request: the appsettings defaults with any
/// demo override layered on top. Overrides ride in a cookie so they survive
/// navigation - useful for showing a flow with and without a feature in one sitting.
///
///   ?ff=pis:off             turn one off
///   ?ff=pis:off,admin:on    set several at once
///   ?ff=reset               drop the overrides, back to appsettings
/// </summary>
public sealed class FeatureSet(FeatureFlags flags, IReadOnlyList<string>? overrides = null)
{
    public const string CookieName = "unotp_ff";
    public const string ItemKey = "unotp.features";
    public const string QueryKey = "ff";

    /// <summary>Short name used in ?ff= and the cookie -> the flag it sets.</summary>
    public static readonly IReadOnlyDictionary<string, Action<FeatureFlags, bool>> Switches =
        new Dictionary<string, Action<FeatureFlags, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["new-fd"] = (f, on) => f.NewFd = on,
            ["pis"] = (f, on) => f.PisGeneration = on,
            ["view-app"] = (f, on) => f.ViewApplication = on,
            ["short-url"] = (f, on) => f.ShortUrl = on,
            ["app-status"] = (f, on) => f.ApplicationStatus = on,
            ["renew"] = (f, on) => f.RenewFd = on,
            ["admin"] = (f, on) => f.Admin = on,
            ["demo"] = (f, on) => f.DemoData = on,
        };

    public FeatureFlags Flags { get; } = flags;

    /// <summary>The overrides in force, as "pis:off" pairs - empty when running on defaults.</summary>
    public IReadOnlyList<string> Overrides { get; } = overrides ?? [];

    public bool IsOverridden => Overrides.Count > 0;

    /// <summary>
    /// Builds the request's feature set and, when ?ff= is present, persists the new
    /// override set to the cookie. Returns null for "reset" so the caller can clear it.
    /// </summary>
    public static FeatureSet Resolve(HttpContext ctx, FeatureFlags defaults, out bool clearCookie)
    {
        clearCookie = false;

        var fromCookie = ctx.Request.Cookies.TryGetValue(CookieName, out var cookie) ? cookie : null;
        var fromQuery = ctx.Request.Query.TryGetValue(QueryKey, out var q) ? q.ToString() : null;

        if (string.Equals(fromQuery?.Trim(), "reset", StringComparison.OrdinalIgnoreCase))
        {
            clearCookie = true;
            return new FeatureSet(defaults.Clone());
        }

        // The query adds to whatever the cookie already carries, so you can flip one
        // feature at a time without restating the others.
        var pairs = Parse(fromCookie).Concat(Parse(fromQuery)).ToList();

        var flags = defaults.Clone();
        List<string> applied = [];
        foreach (var (name, on) in pairs)
        {
            if (!Switches.TryGetValue(name, out var set)) continue;
            set(flags, on);
            applied.RemoveAll(a => a.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase));
            applied.Add($"{name.ToLowerInvariant()}:{(on ? "on" : "off")}");
        }

        return new FeatureSet(flags, applied);
    }

    /// <summary>Parses "pis:off,admin:on" into name/state pairs, skipping anything malformed.</summary>
    private static IEnumerable<(string Name, bool On)> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;

        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', 2, StringSplitOptions.TrimEntries);
            var name = parts[0];
            if (name.Length == 0) continue;

            // "pis" on its own reads as "turn it on".
            var state = parts.Length == 2 ? parts[1] : "on";
            if (state is "on" or "1" or "true") yield return (name, true);
            else if (state is "off" or "0" or "false") yield return (name, false);
        }
    }

    public string ToCookieValue() => string.Join(',', Overrides);
}
