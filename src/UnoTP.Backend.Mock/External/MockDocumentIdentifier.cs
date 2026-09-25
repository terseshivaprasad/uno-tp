using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>The mock document identification: a copy is what its file name says it is.</summary>
public sealed class MockDocumentIdentifier : IDocumentIdentifier
{
    // Which proof of address a name says a copy is, first match first.
    private static readonly (string[] Words, string Type)[] Proofs =
    [
        (["aadhaar", "aadhar"], "Aadhaar"),
        (["passport"], "Passport"),
        (["licence", "license"], "Driving Licence"),
        (["voter"], "Voter ID"),
        (["utility", "bill"], "Utility bill"),
    ];

    private static readonly Dictionary<DocumentKind, (string[] Words, string Named)> Kinds = new()
    {
        [DocumentKind.PanCard] = (["pan"], "PAN"),
        [DocumentKind.ProofOfAddress] = (["poa", "address", "aadhaar", "aadhar", "passport", "licence", "license", "voter", "utility", "bill"], "the proof"),
        [DocumentKind.Cheque] = (["cheque", "chq"], "the cheque"),
    };

    public Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default)
    {
        // A proof of address handed in as nothing in particular is identified as
        // whichever proof its name says; one handed in as a chosen type is checked
        // against it below, the chosen type counting as a name for it.
        if (expected == DocumentKind.ProofOfAddress && type.Length == 0)
        {
            var proof = Proofs.FirstOrDefault(p => MockScans.Named(file, p.Words)).Type;
            return Task.FromResult(proof is not null ? new Identification(true, Type: proof)
                // A PAN card, as IDfy would detect it: known, but no proof of address.
                : MockScans.Named(file, "pan") ? new Identification(false, Idfy.IdfyDocumentIdentifier.NotAProof)
                : new Identification(false, "This mock tells which proof a copy is by its file name, so upload it again with aadhaar, passport, licence, voter or bill in the name."));
        }

        var (words, named) = Kinds[expected];
        // The type chosen above the box counts as a name for the document too.
        var matches = MockScans.Named(file, type.Length > 0 ? [.. words, type.ToLowerInvariant()] : words);
        return Task.FromResult(matches
            ? new Identification(true, Type: expected == DocumentKind.ProofOfAddress ? type : null)
            : new Identification(false, $"This mock identifies a document by its file name, so upload it again with {named} in the name."));
    }
}
