using Microsoft.Extensions.Options;

namespace UnoTP.Services;

/// <summary>
/// The "Backend" section of appsettings: the gateway's address, where every backend
/// API answers unless its own section gives it an address of its own; the name this
/// app calls itself by; how long each API is given to answer; and how long the lists
/// and rules are kept. Each API's own section holds its base path under its address,
/// and the path of each call under that.
/// </summary>
public sealed class BackendOptions
{
    public const string Section = "Backend";

    /// <summary>
    /// The API gateway the backend APIs are behind: https://{gateway-host}/ . An API
    /// whose own section sets a BaseUrl is called there instead. appsettings.json
    /// carries this with the host left as a placeholder; the real host is set in the
    /// environment, never in a committed file.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The name this app calls itself by on every API's X-Client-Id header.</summary>
    public string ClientId { get; set; } = "unotp";

    /// <summary>Seconds an API is given to answer: 55, as for every one of them. Nothing is retried.</summary>
    public int TimeoutSeconds { get; set; } = 55;

    /// <summary>Minutes the lists and rules (reference, config) are kept for. 0 asks every time.</summary>
    public int ReferenceCacheMinutes { get; set; } = 10;

    /// <summary>Whether Backend:BaseUrl is a real address: not blank, and not still the placeholder.</summary>
    public static bool Configured(IConfiguration config) => IsAddress(config[$"{Section}:{nameof(BaseUrl)}"]);

    /// <summary>Whether a setting is a real web address, http or https.</summary>
    public static bool IsAddress(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var address)) return false;
        return address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps;
    }
}

/// <summary>
/// Where one API is, as its settings section says: its own address, when it is not
/// behind the gateway, and its base path under whichever it is at.
/// </summary>
public interface IApiAddress
{
    /// <summary>The API's own address, https://{host}/ ; blank for one behind the gateway (<see cref="BackendOptions.BaseUrl"/>).</summary>
    string BaseUrl { get; }

    /// <summary>The API's base path under its address. Every call's path is relative to it.</summary>
    string BasePath { get; }
}

public static class BackendHttpClients
{
    /// <summary>The header every API is told which app is calling it on.</summary>
    public const string ClientIdHeader = "X-Client-Id";

    /// <summary>
    /// An HTTP client for one API: its own address, or the gateway
    /// (<see cref="BackendOptions.BaseUrl"/>) where its section gives none, and its
    /// base path under that. Each call then goes to its own path, relative to that -
    /// no leading slash.
    /// </summary>
    public static IHttpClientBuilder AddApiClient<TService, TClient>(
        this IServiceCollection services, Func<IServiceProvider, IApiAddress> api)
        where TService : class
        where TClient : class, TService =>
        services.AddHttpClient<TService, TClient>((sp, http) => Configure(sp, http, api(sp)));

    /// <summary>The same, for a client the app asks for by its own name rather than through a contract.</summary>
    public static IHttpClientBuilder AddApiClient<TClient>(
        this IServiceCollection services, Func<IServiceProvider, IApiAddress> api)
        where TClient : class =>
        services.AddHttpClient<TClient>((sp, http) => Configure(sp, http, api(sp)));

    private static void Configure(IServiceProvider sp, HttpClient http, IApiAddress api)
    {
        var backend = sp.GetRequiredService<IOptions<BackendOptions>>().Value;
        http.BaseAddress = new Uri(AddressOf(api, backend.BaseUrl));
        http.Timeout = TimeSpan.FromSeconds(backend.TimeoutSeconds);
        http.DefaultRequestHeaders.Add(ClientIdHeader, backend.ClientId);
    }

    /// <summary>
    /// The address an API is called at, ending in a slash: its own address, or the
    /// gateway's where it has none, with its base path under it.
    /// </summary>
    public static string AddressOf(IApiAddress api, string gateway)
    {
        var host = gateway;
        if (!string.IsNullOrWhiteSpace(api.BaseUrl)) host = api.BaseUrl;
        host = host.Trim().TrimEnd('/');

        var path = api.BasePath.Trim().Trim('/');
        if (path.Length == 0) return host + "/";
        return host + "/" + path + "/";
    }

    /// <summary>
    /// The API sections that have no address to be called at: one whose own BaseUrl
    /// is set but is not a web address, or is blank while the gateway is not set either.
    /// </summary>
    public static IReadOnlyList<string> Unaddressed(IConfiguration config, params string[] sections)
    {
        var gatewaySet = BackendOptions.Configured(config);
        var unaddressed = new List<string>();
        foreach (var section in sections)
        {
            var own = config[$"{section}:{nameof(IApiAddress.BaseUrl)}"];
            if (string.IsNullOrWhiteSpace(own))
            {
                if (!gatewaySet) unaddressed.Add(section);
                continue;
            }
            if (!BackendOptions.IsAddress(own)) unaddressed.Add(section);
        }
        return unaddressed;
    }
}
