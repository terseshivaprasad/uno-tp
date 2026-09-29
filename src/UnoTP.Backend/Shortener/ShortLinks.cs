using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Shortener;

/// <summary>
/// Short links for the investor: the payment link an application is submitted with
/// is shortened before it goes to the backend, so the SMS carries a cmtpl.in link
/// rather than a long one. Nothing is retried - a repeated call could mint a second
/// short link - and a shortener that cannot answer does not stop a submission: the
/// application goes with the long link alone.
/// </summary>
public interface IShortLinkService
{
    /// <summary>
    /// The short link for <paramref name="longUrl"/>, or null when there is nothing
    /// to shorten with (no shortener configured). A shortener that is configured
    /// but cannot answer throws <see cref="ExternalServiceException"/>.
    /// </summary>
    Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default);
}

/// <summary>Where UrlShortener.Api is served from, from the "Shortener" section of appsettings.</summary>
public sealed class ShortenerOptions
{
    public const string Section = "Shortener";

    public string BaseUrl { get; set; } = "";

    /// <summary>The name this app calls itself by on UrlShortener.Api's X-Client-Id header (its rate limit is per client).</summary>
    public string ClientId { get; set; } = "unotp";

    /// <summary>UrlShortener.Api waits up to 30 s for the provider, so the app waits a little longer.</summary>
    public int TimeoutSeconds { get; set; } = 35;

    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[$"{Section}:{nameof(BaseUrl)}"]);
}

/// <summary>GET api/shorten?url={long URL} → { shortUrl, longUrl }.</summary>
public sealed class ShortenerClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "The link shortener"), IShortLinkService
{
    public async Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        var answer = await Ask(() => Get<Answer>("api/shorten?url=" + Uri.EscapeDataString(longUrl), ct), ct);
        return answer?.ShortUrl is { Length: > 0 } shortUrl
            ? shortUrl
            : throw new ExternalServiceException("The link shortener", "The link shortener answered with no short link. Try again in a while.");
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
    /// answered before (the mock, or nothing). Call it after the backend or the mock.
    /// </summary>
    public static IServiceCollection AddShortener(this IServiceCollection services)
    {
        services.RemoveAll<IShortLinkService>();
        services.AddHttpClient<IShortLinkService, ShortenerClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<ShortenerOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            http.DefaultRequestHeaders.Add("X-Client-Id", options.ClientId);
        });
        return services;
    }

    /// <summary>With no shortener configured, links go unshortened - unless the mock is answering.</summary>
    public static IServiceCollection AddUnshortenedLinks(this IServiceCollection services)
    {
        services.TryAddSingleton<IShortLinkService, UnshortenedLinks>();
        return services;
    }
}
