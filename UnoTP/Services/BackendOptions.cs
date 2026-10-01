using Microsoft.Extensions.Options;

namespace UnoTP.Services;

/// <summary>
/// The "Backend" section of appsettings: the one address every backend API answers
/// at, the name this app calls itself by, how long each API is given to answer, and
/// how long the lists and rules are kept. Each API's own section holds its base path
/// under this address, and the path of each call under that.
/// </summary>
public sealed class BackendOptions
{
    public const string Section = "Backend";

    /// <summary>
    /// The API gateway every backend API is behind: https://{gateway-host}/ .
    /// appsettings.json carries it with the host left as a placeholder; the real
    /// host is set in the environment, never in a committed file.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The name this app calls itself by on every API's X-Client-Id header.</summary>
    public string ClientId { get; set; } = "unotp";

    /// <summary>Seconds an API is given to answer: 55, as for every one of them. Nothing is retried.</summary>
    public int TimeoutSeconds { get; set; } = 55;

    /// <summary>Minutes the lists and rules (reference, config) are kept for. 0 asks every time.</summary>
    public int ReferenceCacheMinutes { get; set; } = 10;

    /// <summary>Whether Backend:BaseUrl is a real address: not blank, and not still the placeholder.</summary>
    public static bool Configured(IConfiguration config)
    {
        var baseUrl = config[$"{Section}:{nameof(BaseUrl)}"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var address)) return false;
        return address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps;
    }
}

public static class BackendHttpClients
{
    /// <summary>The header every API is told which app is calling it on.</summary>
    public const string ClientIdHeader = "X-Client-Id";

    /// <summary>
    /// An HTTP client for one API: the gateway (<see cref="BackendOptions.BaseUrl"/>)
    /// and that API's base path under it. Each call then goes to its own path,
    /// relative to that - no leading slash.
    /// </summary>
    public static IHttpClientBuilder AddApiClient<TService, TClient>(
        this IServiceCollection services, Func<IServiceProvider, string> basePath)
        where TService : class
        where TClient : class, TService =>
        services.AddHttpClient<TService, TClient>((sp, http) => Configure(sp, http, basePath(sp)));

    /// <summary>The same, for a client the app asks for by its own name rather than through a contract.</summary>
    public static IHttpClientBuilder AddApiClient<TClient>(
        this IServiceCollection services, Func<IServiceProvider, string> basePath)
        where TClient : class =>
        services.AddHttpClient<TClient>((sp, http) => Configure(sp, http, basePath(sp)));

    private static void Configure(IServiceProvider sp, HttpClient http, string basePath)
    {
        var backend = sp.GetRequiredService<IOptions<BackendOptions>>().Value;
        http.BaseAddress = new Uri(Address(backend.BaseUrl, basePath));
        http.Timeout = TimeSpan.FromSeconds(backend.TimeoutSeconds);
        http.DefaultRequestHeaders.Add(ClientIdHeader, backend.ClientId);
    }

    /// <summary>The gateway and an API's base path as one address, ending in a slash.</summary>
    private static string Address(string baseUrl, string basePath)
    {
        var gateway = baseUrl.TrimEnd('/');
        var path = basePath.Trim().Trim('/');
        return path.Length == 0 ? gateway + "/" : gateway + "/" + path + "/";
    }
}
