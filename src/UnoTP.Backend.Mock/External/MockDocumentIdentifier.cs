using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>The mock document identification: a copy is what its file name says it is.</summary>
public sealed class MockDocumentIdentifier : IDocumentIdentifier
{
    private static readonly Dictionary<DocumentKind, (string[] Words, string Named)> Kinds = new()
    {
        [DocumentKind.PanCard] = (["pan"], "PAN"),
        [DocumentKind.ProofOfAddress] = (["poa", "address", "aadhaar", "aadhar", "passport", "licence", "license", "voter", "utility", "bill"], "the proof"),
        [DocumentKind.Cheque] = (["cheque", "chq"], "the cheque"),
    };

    public Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default)
    {
        var (words, named) = Kinds[expected];
        // The type chosen above the box counts as a name for the document too.
        var matches = MockScans.Named(file, type.Length > 0 ? [.. words, type.ToLowerInvariant()] : words);
        return Task.FromResult(matches
            ? new Identification(true)
            : new Identification(false, $"This mock identifies a document by its file name, so upload it again with {named} in the name."));
    }
}
