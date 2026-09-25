namespace UnoTP.Backend.External;

/// <summary>
/// Aadhaar masking: whether an Aadhaar copy has its number masked. An Aadhaar is
/// filed unmasked, with all 12 digits readable.
/// </summary>
public interface IMaskingService
{
    /// <param name="consent">Whether the holder has consented to their Aadhaar
    /// being processed. Nothing is sent without it.</param>
    Task<bool> IsMaskedAsync(UploadFile file, bool consent, CancellationToken ct = default);
}

/// <summary>POST check (multipart: file, consent) → { masked }.</summary>
public sealed class MaskingClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Aadhaar masking"), IMaskingService
{
    public const string Name = "Masking";

    public async Task<bool> IsMaskedAsync(UploadFile file, bool consent, CancellationToken ct = default) =>
        (await Ask(() => Send<Answer>(HttpMethod.Post, "check", Form(file, ("consent", consent ? "true" : "false")), ct), ct)).Masked;

    private sealed record Answer(bool Masked);
}
