namespace UnoTP.Backend.External;

/// <summary>Document identification: whether a copy is the kind of document it is handed in as.</summary>
public interface IDocumentIdentifier
{
    /// <param name="type">What it was chosen as within the kind - the proof of
    /// address type, or the payment mode - or empty.</param>
    Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default);
}

/// <param name="Hint">What to do differently next time, where the service has something to say.</param>
public sealed record Identification(bool Matches, string? Hint = null);

/// <summary>POST identify (multipart: file, expected, type) → Identification.</summary>
public sealed class DocumentIdentifierClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Document identification"), IDocumentIdentifier
{
    public const string Name = "Identify";

    public Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default) =>
        Ask(() => Send<Identification>(HttpMethod.Post, "identify", Form(file, ("expected", expected.ToString()), ("type", type)), ct), ct);
}
