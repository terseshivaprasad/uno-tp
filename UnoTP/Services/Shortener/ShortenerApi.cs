using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.Shortener;

/// <summary>
/// UrlShortener.Api, from the "Shortener" section of appsettings: the path of the
/// call under the gateway (<see cref="BackendOptions.BaseUrl"/>). Left empty, the
/// payment link goes in full.
/// </summary>
public sealed class ShortenerOptions : IApiAddress
{
    public const string Section = "Shortener";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. Every path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>GET: the long URL shortened. The URL goes on the query string, as url=.</summary>
    public string ShortenPath { get; set; } = "";

    /// <summary>Whether a path is set for the shortener; without one, links go in full.</summary>
    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[$"{Section}:{nameof(ShortenPath)}"]);
}

/// <summary>GET ShortenPath?url={long URL} → { shortUrl, longUrl }.</summary>
public sealed class ShortenerClient(HttpClient http, IPartner partner, IOptions<ShortenerOptions> options)
    : ExternalClient(http, partner, "The link shortener"), IShortLinkService
{
    public async Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        var path = options.Value.ShortenPath + "?url=" + Uri.EscapeDataString(longUrl);
        var answer = await Ask(() => Get<Answer>(path, ct), ct);
        return answer?.ShortUrl is { Length: > 0 } shortUrl
            ? shortUrl
            : throw new ExternalServiceException("The link shortener", Messages.OutsideServices.NoShortLink);
    }

    private sealed record Answer(string ShortUrl, string LongUrl);
}

/// <summary>No shortener configured: links go as they are.</summary>
public sealed class UnshortenedLinks : IShortLinkService
{
    public Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default) => Task.FromResult<string?>(null);
}

public static class ShortenerServiceCollectionExtensions
{
    /// <summary>
    /// Puts UrlShortener.Api behind <see cref="IShortLinkService"/>, in place of whatever
    /// answered before.
    /// </summary>
    public static IServiceCollection AddShortener(this IServiceCollection services)
    {
        services.RemoveAll<IShortLinkService>();
        services.AddApiClient<IShortLinkService, ShortenerClient>(
            sp => sp.GetRequiredService<IOptions<ShortenerOptions>>().Value);
        return services;
    }

    /// <summary>With no shortener configured, links go unshortened.</summary>
    public static IServiceCollection AddUnshortenedLinks(this IServiceCollection services)
    {
        services.TryAddSingleton<IShortLinkService, UnshortenedLinks>();
        return services;
    }
}
