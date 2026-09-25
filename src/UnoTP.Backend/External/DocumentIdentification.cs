namespace UnoTP.Backend.External;

/// <summary>
/// Document identification: whether a copy is the kind of document it is handed in
/// as, and for a proof of address, which proof it is.
/// </summary>
public interface IDocumentIdentifier
{
    /// <param name="type">What it was handed in as within the kind - the payment
    /// mode - or empty. A proof of address is handed in as nothing in particular:
    /// the service says which it is, in <see cref="Identification.Type"/>.</param>
    Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default);
}

/// <param name="Hint">What to do differently next time, where the service has something to say.</param>
/// <param name="Type">Which proof of address the copy is - Aadhaar, Passport, Driving
/// Licence, Voter ID or Utility bill - when it is one; null for anything else.</param>
public sealed record Identification(bool Matches, string? Hint = null, string? Type = null);

/// <summary>POST identify (multipart: file, expected, type) → Identification.</summary>
public sealed class DocumentIdentifierClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Document identification"), IDocumentIdentifier
{
    public const string Name = "Identify";

    public Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default) =>
        Ask(() => Send<Identification>(HttpMethod.Post, "identify", Form(file, ("expected", expected.ToString()), ("type", type)), ct), ct);
}
