using Microsoft.Extensions.Configuration;

namespace UnoTP.Backend;

/// <summary>
/// Where the backend API is served from, from the "Backend" section of appsettings
/// (or Backend__BaseUrl in the environment). Left blank, the app runs on the mock
/// backend instead, which holds everything in memory.
///
/// Each outside service is reached through the backend at external/{service}/
/// unless Backend:External:{service} gives it an address of its own.
/// </summary>
public sealed class BackendOptions
{
    public const string Section = "Backend";

    public string BaseUrl { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Addresses for outside services, by name, where one is not reached through the backend.</summary>
    public Dictionary<string, string> External { get; set; } = [];

    /// <summary>True while no backend is configured and the mock stands in for it.</summary>
    public bool IsMock => string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>Where an outside service answers, closing slash included so routes stay relative to it.</summary>
    public Uri UrlOf(string service) =>
        new((External.TryGetValue(service, out var own) && !string.IsNullOrWhiteSpace(own)
            ? own
            : $"{BaseUrl.TrimEnd('/')}/external/{service.ToLowerInvariant()}").TrimEnd('/') + "/");

    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[$"{Section}:{nameof(BaseUrl)}"]);
}
