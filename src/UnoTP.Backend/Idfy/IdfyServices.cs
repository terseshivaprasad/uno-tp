using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Idfy;

// The outside services, answered by IDfy where IDfy has an endpoint for the
// document. Where it has none - a cheque, a utility bill, an Aadhaar's source
// check, a bank account - each hands over to the service that answered before
// IDfy was configured (see IdfyServiceCollectionExtensions).

/// <summary>What IDfy calls each proof, for the proofs it knows.</summary>
internal static class IdfyDocTypes
{
    public const string Pan = "ind_pan";

    public static string? Of(DocumentKind kind, string type) => kind switch
    {
        DocumentKind.PanCard => Pan,
        DocumentKind.ProofOfAddress => type switch
        {
            "Aadhaar" => "ind_aadhaar",
            "Passport" => "ind_passport",
            "Driving Licence" => "ind_driving_license",
            "Voter ID" => "ind_voter_id",
            _ => null,
        },
        _ => null,
    };

    public static string Named(string docType) => docType switch
    {
        "ind_pan" => "a PAN card",
        "ind_aadhaar" => "an Aadhaar",
        "ind_passport" => "a passport",
        "ind_driving_license" => "a driving licence",
        "ind_voter_id" => "a voter ID",
        _ => "another document",
    };
}

/// <summary>Identification by IDfy's document validation: readable, and the kind of document expected.</summary>
public sealed class IdfyDocumentIdentifier(
    IdfyClient idfy,
    [FromKeyedServices(IdfyServiceCollectionExtensions.Fallback)] IDocumentIdentifier fallback) : IDocumentIdentifier
{
    public async Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default)
    {
        var docType = IdfyDocTypes.Of(expected, type);
        if (docType is null) return await fallback.IdentifyAsync(expected, type, file, ct);

        var result = (await idfy.ValidateAsync(file, docType, ct)).Result!;
        if (result.IsReadable != true)
            return new Identification(false, "The copy could not be read. Upload a sharper scan, with the whole card in view.");
        if (!string.Equals(result.DetectedDocType, docType, StringComparison.OrdinalIgnoreCase))
            return new Identification(false, result.DetectedDocType is { Length: > 0 } detected
                ? $"It reads as {IdfyDocTypes.Named(detected)}. Upload {IdfyDocTypes.Named(docType)} itself."
                : null);
        return new Identification(true);
    }
}

/// <summary>
/// Masking by IDfy: it is asked to mask the Aadhaar number, and a copy on which it
/// finds no number to mask is one that is masked already.
/// </summary>
public sealed class IdfyMasking(IdfyClient idfy) : IMaskingService
{
    public async Task<bool> IsMaskedAsync(UploadFile file, bool consent, CancellationToken ct = default) =>
        (await idfy.MaskAadhaarAsync(file, consent, ct)).Result!.IdNumberFound != true;
}

/// <summary>OCR by IDfy for a PAN card, an Aadhaar, a driving licence, a passport and a voter ID.</summary>
public sealed class IdfyOcr(
    IdfyClient idfy,
    [FromKeyedServices(IdfyServiceCollectionExtensions.Fallback)] IOcrService fallback) : IOcrService
{
    public async Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default)
    {
        switch (IdfyDocTypes.Of(kind, type))
        {
            case "ind_pan":
                var pan = (await idfy.ExtractPanAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Pan: pan?.IdNumber ?? "", Name: pan?.NameOnCard ?? "");

            case "ind_aadhaar":
                // What the QR code says is what UIDAI signed, so it is read first -
                // unless it carries only part of the number, as a secure QR code does.
                var aadhaar = (await idfy.ExtractAadhaarAsync(file, consent, ct)).Result!;
                var card = AadhaarNumbers.IsWhole(Digits(aadhaar.QrOutput?.IdNumber)) ? aadhaar.QrOutput : aadhaar.ExtractionOutput;
                return new OcrReading(Name: card?.NameOnCard ?? "", Address: card?.Address ?? "",
                    IdNumber: Digits(card?.IdNumber));

            case "ind_driving_license":
                var licence = (await idfy.ExtractDrivingLicenceAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Name: licence?.NameOnCard ?? "", Address: licence?.Address ?? "",
                    IdNumber: licence?.IdNumber ?? "");

            case "ind_passport":
                // A passport is verified by its file number, not its passport number.
                var passport = (await idfy.ExtractPassportAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Name: passport?.NameOnCard ?? "", Address: passport?.Address ?? "",
                    IdNumber: passport?.FileNumber ?? "");

            case "ind_voter_id":
                var voter = (await idfy.ExtractVoterIdAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Name: voter?.NameOnCard ?? "", Address: voter?.Address ?? "",
                    IdNumber: voter?.IdNumber ?? "");

            default:
                return await fallback.ReadAsync(kind, type, file, subject, consent, ct);
        }
    }

    // An Aadhaar number is printed in groups of four.
    private static string Digits(string? number) => (number ?? "").Replace(" ", "");
}

/// <summary>
/// Verification by IDfy against the issuing source, for a driving licence, a
/// passport and a voter ID, by the number OCR read off the copy. The licence and
/// passport are asked with the holder's date of birth, so one that is not the
/// holder's is not found. With no number to ask with, the issuer is not asked, and
/// the answer says why rather than reading as a refusal.
/// </summary>
public sealed class IdfyVerification(
    IdfyClient idfy,
    [FromKeyedServices(IdfyServiceCollectionExtensions.Fallback)] IVerificationService fallback) : IVerificationService
{
    public async Task<Verification> ConfirmProofAsync(string proofType, OcrReading reading, string holderDob, CancellationToken ct = default)
    {
        var number = reading.IdNumber.Trim();
        switch (proofType)
        {
            case "Driving Licence":
                const string sarathi = "Sarathi";
                number = number.Replace(" ", "").Replace("-", "");
                if (number.Length == 0) return NotAsked(sarathi, "the licence number could not be read off the copy");
                if (Dob(holderDob) is not { } licenceDob) return NotAsked(sarathi, "the holder's date of birth is not on record in a form it can be asked with");
                return Answer((await idfy.VerifyDrivingLicenceAsync(number, licenceDob, ct)).Result!, sarathi);

            case "Passport":
                const string seva = "Passport Seva";
                // The file number is on the passport's last page, which a single copy may not show.
                if (number.Length == 0) return NotAsked(seva, "the passport file number is not on the copy — it is on the last page");
                if (Dob(holderDob) is not { } passportDob) return NotAsked(seva, "the holder's date of birth is not on record in a form it can be asked with");
                return Answer((await idfy.VerifyPassportAsync(number, passportDob, ct)).Result!, seva);

            case "Voter ID":
                const string commission = "the Election Commission";
                if (number.Length == 0) return NotAsked(commission, "the EPIC number could not be read off the copy");
                // IDfy may read the EPIC number partly masked, and a masked one cannot be looked up.
                if (number.Contains('*')) return NotAsked(commission, "the EPIC number was read partly masked");
                return Answer((await idfy.VerifyVoterIdAsync(number, ct)).Result!, commission);

            default:
                // IDfy has no source check for an Aadhaar, and nobody answers for a bill.
                return await fallback.ConfirmProofAsync(proofType, reading, holderDob, ct);
        }
    }

    // IDfy has no bank-account check.
    public Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default) =>
        fallback.ConfirmAccountAsync(account, bank, ct);

    private static Verification Answer(Sourced<SourceStatus> result, string verifier) =>
        new(string.Equals(result.SourceOutput?.Status, "id_found", StringComparison.OrdinalIgnoreCase), verifier);

    private static Verification NotAsked(string verifier, string why) => new(false, verifier, why);

    private static DateOnly? Dob(string dob) =>
        DateOnly.TryParseExact(dob, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}

/// <summary>The PAN-Aadhaar link by IDfy, asked with both numbers.</summary>
public sealed class IdfyPanAadhaarLink(IdfyClient idfy) : IPanAadhaarLinkService
{
    public async Task<PanAadhaarLink> CheckAsync(string pan, string aadhaarNumber, CancellationToken ct = default)
    {
        if (!AadhaarNumbers.IsWhole(aadhaarNumber)) return PanAadhaarLink.NeedsAadhaar;
        var source = (await idfy.VerifyPanAadhaarLinkAsync(pan, aadhaarNumber, ct)).Result!.SourceOutput;
        return source?.IsLinked == true ? PanAadhaarLink.Linked : PanAadhaarLink.NotLinked;
    }
}
