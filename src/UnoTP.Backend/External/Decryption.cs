namespace UnoTP.Backend.External;

/// <summary>The portal's decryption service: turns what the portal encrypted into plain text.</summary>
public interface IDecryptionService
{
    /// <summary>The plain text, or null when the value cannot be decrypted - tampered with, or not the portal's.</summary>
    Task<string?> DecryptAsync(string cipherText, CancellationToken ct = default);
}

/// <summary>POST decrypt { value } → { value }, or 400 when it cannot be decrypted.</summary>
public sealed class DecryptionClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Decryption"), IDecryptionService
{
    public const string Name = "Decrypt";

    public Task<string?> DecryptAsync(string cipherText, CancellationToken ct = default) =>
        Ask(async () =>
        {
            using var request = Request(HttpMethod.Post, "decrypt", Body(new { value = cipherText }));
            using var response = await SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest) return null;
            response.EnsureSuccessStatusCode();
            return (await Read<Answer>(response, ct)).Value;
        }, ct);

    private sealed record Answer(string? Value);
}
