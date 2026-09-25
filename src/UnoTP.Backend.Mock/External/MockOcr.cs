using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock OCR. A PAN card reads as the holder's PAN and date of birth, under
/// the name the test data says OCR reads - or, named "otherpan" or "otherdob",
/// as another PAN or date of birth; a proof of address and a cheque read as what the issuer
/// and the bank hold, unless the copy is named as a mismatch. An Aadhaar also reads
/// as an Aadhaar number no real one can be - they never start with 0 - so the
/// PAN-Aadhaar link has one to be asked with - unless the copy is named as masked,
/// when only its last four digits are read. The mock does not hold out for the
/// holder's consent the way IDfy does.
/// </summary>
public sealed class MockOcr : IOcrService
{
    public const string AadhaarNumber = "000011112222";

    public const string MaskedNumber = "XXXXXXXX2222";

    public Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default) =>
        Task.FromResult(kind switch
        {
            DocumentKind.PanCard => new OcrReading(
                Pan: MockScans.Misread(file) || MockScans.Named(file, "otherpan") ? OtherPan(subject.Pan) : subject.Pan,
                Name: NameOn(subject.Pan),
                Dob: MockScans.Named(file, "otherdob") ? OtherDob : subject.Dob),
            DocumentKind.ProofOfAddress => new OcrReading(
                Name: subject.Name,
                Address: MockScans.Misread(file) ? MockScans.MisreadAddress : MockScans.Address,
                IdNumber: type != "Aadhaar" ? "" : MockScans.Named(file, "masked") && !MockScans.Named(file, "unmasked") ? MaskedNumber : AadhaarNumber,
                Gender: type != "Aadhaar" ? "" : GenderOn(subject)),
            _ => new OcrReading(Account: MockScans.Misread(file) ? MockScans.MisreadAccount : MockScans.Account, Bank: MockScans.Bank),
        });

    /// <summary>The gender an Aadhaar prints: the folio's where there is one, else
    /// by the first name the test data gives the holder.</summary>
    internal static string GenderOn(OcrSubject subject) =>
        Genders.Of(MockInvestors.Folios.FirstOrDefault(f => f.Pan == subject.Pan)?.Gender) is { Length: > 0 } g ? g
        : WomensNames.Contains(subject.Name.Split(' ')[0]) ? Genders.Female : Genders.Male;

    private static readonly HashSet<string> WomensNames =
        new(["ANJALI", "PRIYA", "MEERA", "NEHA", "SNEHA"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The date of birth a copy named "otherdob" reads as: nobody's in the test data.</summary>
    public const string OtherDob = "01-01-1970";

    // A PAN one letter off the holder's, as a copy named "otherpan" or a mismatch reads.
    private static string OtherPan(string pan) =>
        pan.Length == 10 ? pan[..9] + (pan[9] == 'Z' ? 'Y' : 'Z') : "ZZZZZ9999Z";

    /// <summary>The name OCR reads off a PAN card: the test data's, or one made up from the PAN.</summary>
    internal static string NameOn(string pan) =>
        MockInvestors.Pans.FirstOrDefault(p => p.Pan == pan)?.Ocr
        ?? MockInvestors.OcrNames[MockInvestors.Seed(pan) % MockInvestors.OcrNames.Length];
}
