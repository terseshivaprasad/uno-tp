using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock OCR. A PAN card reads as the holder's PAN, under the name the test
/// data says OCR reads; a proof of address and a cheque read as what the issuer
/// and the bank hold, unless the copy is named as a mismatch. An Aadhaar also reads
/// as an Aadhaar number no real one can be - they never start with 0 - so the
/// PAN-Aadhaar link has one to be asked with. The mock does not hold out for the
/// holder's consent the way IDfy does.
/// </summary>
public sealed class MockOcr : IOcrService
{
    public const string AadhaarNumber = "000011112222";

    public Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default) =>
        Task.FromResult(kind switch
        {
            DocumentKind.PanCard => new OcrReading(Pan: subject.Pan, Name: NameOn(subject.Pan)),
            DocumentKind.ProofOfAddress => new OcrReading(
                Name: subject.Name,
                Address: MockScans.Misread(file) ? MockScans.MisreadAddress : MockScans.Address,
                IdNumber: type == "Aadhaar" ? AadhaarNumber : ""),
            _ => new OcrReading(Account: MockScans.Misread(file) ? MockScans.MisreadAccount : MockScans.Account, Bank: MockScans.Bank),
        });

    /// <summary>The name OCR reads off a PAN card: the test data's, or one made up from the PAN.</summary>
    internal static string NameOn(string pan) =>
        MockInvestors.Pans.FirstOrDefault(p => p.Pan == pan)?.Ocr
        ?? MockInvestors.OcrNames[MockInvestors.Seed(pan) % MockInvestors.OcrNames.Length];
}
