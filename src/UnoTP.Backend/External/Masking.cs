namespace UnoTP.Backend.External;

/// <summary>
/// Aadhaar masking: the copy with its Aadhaar number masked. An Aadhaar is masked
/// the moment Upload Documents identifies it, before OCR reads it and before any
/// copy of it is kept or filed, so no copy with the whole number is ever stored.
/// </summary>
public interface IMaskingService
{
    /// <summary>The masked copy: the same file name, the masked image.</summary>
    /// <param name="consent">Whether the holder has consented to their Aadhaar
    /// being processed. Nothing is sent without it.</param>
    Task<UploadFile> MaskAsync(UploadFile file, bool consent, CancellationToken ct = default);
}

/// <summary>POST mask (multipart: file, consent) → the masked image, as the response body, with its content type.</summary>
public sealed class MaskingClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Aadhaar masking"), IMaskingService
{
    public const string Name = "Masking";

    public Task<UploadFile> MaskAsync(UploadFile file, bool consent, CancellationToken ct = default) =>
        Ask(async () =>
        {
            using var request = Request(HttpMethod.Post, "mask", Form(file, ("consent", consent ? "true" : "false")));
            using var response = await SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? file.ContentType;
            return new UploadFile(file.FileName, contentType, bytes);
        }, ct);
}
