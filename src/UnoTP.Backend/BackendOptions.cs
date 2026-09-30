using Microsoft.Extensions.Configuration;

namespace UnoTP.Backend;

/// <summary>
/// The "Backend" section of appsettings: how long the lists and rules are kept,
/// and where each outside service answers (Backend:External:{service}).
/// </summary>
public sealed class BackendOptions
{
    public const string Section = "Backend";

    /// <summary>Seconds an outside service is given to answer: 55, as for every outside service. Nothing is retried.</summary>
    public int TimeoutSeconds { get; set; } = 55;

    /// <summary>Minutes the lists and rules (reference, config) are kept for. 0 asks every time.</summary>
    public int ReferenceCacheMinutes { get; set; } = 10;

    /// <summary>Each outside service's address, by name.</summary>
    public Dictionary<string, string> External { get; set; } = [];

    /// <summary>Where an outside service answers, closing slash included so routes stay relative to it.</summary>
    public Uri UrlOf(string service) =>
        External.TryGetValue(service, out var url) && !string.IsNullOrWhiteSpace(url)
            ? new Uri(url.TrimEnd('/') + "/")
            : throw new InvalidOperationException($"Backend:External:{service} is not set.");

    /// <summary>Whether Backend:External:{service} gives the service an address.</summary>
    public static bool HasAddress(IConfiguration config, string service) =>
        !string.IsNullOrWhiteSpace(config[$"{Section}:External:{service}"]);
}
