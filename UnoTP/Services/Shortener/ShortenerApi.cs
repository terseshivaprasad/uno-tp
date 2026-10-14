using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>POST: the long URL shortened. The URL goes in the body, as long_url, with Source UNO_TP.</summary>
    public string ShortenPath { get; set; } = "";

    /// <summary>Whether a path is set for the shortener; without one, links go in full.</summary>
    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[$"{Section}:{nameof(ShortenPath)}"]);
}

/// <summary>
/// POST ShortenPath { long_url, Source: "UNO_TP" } → { ShortURL }, as the FD system's own
/// app calls MMFSL_FD_Shortner_API and reads its answer.
/// </summary>
public sealed class ShortenerClient(HttpClient http, IPartner partner, IOptions<ShortenerOptions> options)
    : ExternalClient(http, partner, "The link shortener"), IShortLinkService
{
    private const string Source = "UNO_TP";

    private static readonly JsonSerializerOptions AsNamed = new();

    public async Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        var answer = await Ask(() => SendAsync(new ShortenRequest(longUrl, Source), ct), ct);
        return answer.ShortUrl is { Length: > 0 } shortUrl
            ? shortUrl
            : throw new ExternalServiceException("The link shortener", Messages.OutsideServices.NoShortLink);
    }

    // Sent whole, with its length, and with the API's own field names.
    private Task<Answer> SendAsync(ShortenRequest request, CancellationToken ct)
    {
        var body = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(request, AsNamed));
        body.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return Send<Answer>(HttpMethod.Post, options.Value.ShortenPath, body, ct);
    }

    private sealed record ShortenRequest([property: JsonPropertyName("long_url")] string LongUrl, [property: JsonPropertyName("Source")] string Source);

    private sealed record Answer([property: JsonPropertyName("ShortURL")] string? ShortUrl);
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
