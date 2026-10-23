using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.Decrypt;

/// <summary>
/// The decryption API, from the "DecryptApi" section of appsettings: its own address
/// or its base path under the gateway (<see cref="BackendOptions.BaseUrl"/>), and
/// the path of the one call.
/// </summary>
public sealed class DecryptApiOptions : IApiAddress
{
    public const string Section = "DecryptApi";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. The path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: the portal's encrypted user id and system code, decrypted.</summary>
    public string DecryptPath { get; set; } = "cipher/decrypt";
}

/// <summary>
/// The decryption API, which the way in from the portal uses first: POST DecryptPath
/// with { Text } answers { success, message, data }, data being the plain text.
/// </summary>
public sealed class DecryptApiClient(HttpClient http, IOptions<DecryptApiOptions> options) : IDecryptionService
{
    private const string Service = "The E-Sarathi decryption service";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string?> DecryptAsync(string cipherText, CancellationToken ct = default)
    {
        try
        {
            using var body = new StringContent(JsonSerializer.Serialize(new { Text = cipherText }), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(options.Value.DecryptPath, body, ct);
            var status = (int)response.StatusCode;
            // Turned down (a 4xx): nothing to sign in with.
            if (status >= 400 && status < 500) return null;
            response.EnsureSuccessStatusCode();
            var answer = await response.Content.ReadFromJsonAsync<Answer>(Json, ct);
            if (answer is null || !answer.Success) return null;
            return answer.Data;
        }
        catch (HttpRequestException e)
        {
            throw new ExternalServiceException(Service, Messages.OutsideServices.NotAnswering(Service), inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceException(Service, Messages.OutsideServices.TookTooLong(Service), inner: e);
        }
        catch (JsonException e)
        {
            throw new ExternalServiceException(Service, Messages.OutsideServices.NotReadable(Service), inner: e);
        }
    }

    private sealed record Answer(bool Success, string? Message, string? Data);
}

public static class DecryptApiServiceCollectionExtensions
{
    /// <summary>Puts the decryption API behind <see cref="IDecryptionService"/>.</summary>
    public static IServiceCollection AddDecryptApi(this IServiceCollection services)
    {
        services.AddApiClient<IDecryptionService, DecryptApiClient>(
            sp => sp.GetRequiredService<IOptions<DecryptApiOptions>>().Value);
        return services;
    }
}
