namespace UnoTP.Models;

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
