namespace UnoTP.Backend.External;

/// <summary>
/// Aadhaar masking: the copy with its Aadhaar number masked. Upload Documents
/// masks an Aadhaar after OCR has read it and the name has been matched, and
/// before any copy of it is filed or kept aside, so no copy with the whole number
/// is ever stored. The PAN-Aadhaar link is asked afterwards, with the number OCR
/// read off the copy before it was masked.
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
