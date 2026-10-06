using System.Globalization;
using UnoTP.Models;

namespace UnoTP.Services.Idfy;

// The document checks IDfy answers: identification, OCR, verification with the
// issuer, the PAN-Aadhaar link and face match. Where IDfy has no endpoint - an
// Aadhaar's source check, a bank account - nothing is asked, and
// the answer says so, as it does for a check that is switched off.

/// <summary>What IDfy calls each proof, for the proofs it knows.</summary>
internal static class IdfyDocTypes
{
    public const string Pan = "ind_pan";

    /// <summary>The IDfy service that verifies a document of this kind and type, or null when none does.</summary>
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

    /// <summary>The proof of address a type IDfy detected is, or null for any other document.</summary>
    public static string? ProofOf(string? docType) => docType?.ToLowerInvariant() switch
    {
        "ind_aadhaar" => "Aadhaar",
        "ind_passport" => "Passport",
        "ind_driving_license" => "Driving Licence",
        "ind_voter_id" => "Voter ID",
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

/// <summary>
/// Identification by IDfy's document validation: readable, and the kind of document
/// expected. A proof of address is validated against no type at all, and IDfy says
/// which it is; a document IDfy does not know - a utility bill, which is not taken - is not told apart.
/// </summary>
public sealed class IdfyDocumentIdentifier(IdfyClient idfy) : IDocumentIdentifier
{
    public const string NotAProof = Messages.UploadDocuments.NotAProofOfAddress;

    public async Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default)
    {
        if (expected == DocumentKind.ProofOfAddress && type.Length == 0)
        {
            var found = (await idfy.ValidateAsync(file, null, ct)).Result!;
            // A PAN card is an identity document IDfy knows, but proves no address.
            if (string.Equals(found.DetectedDocType, IdfyDocTypes.Pan, StringComparison.OrdinalIgnoreCase))
                return new Identification(false, NotAProof);
            // Not a proof IDfy knows: which proof it is cannot be said.
            if (IdfyDocTypes.ProofOf(found.DetectedDocType) is not { } proof) return new Identification(true);
            return found.IsReadable == true
                ? new Identification(true, Type: proof)
                : new Identification(false, Messages.UploadDocuments.ReadsAsButUnreadable(IdfyDocTypes.Named(found.DetectedDocType!)));
        }

        // A document IDfy has no type for is taken as what it was handed in as.
        var docType = IdfyDocTypes.Of(expected, type);
        if (docType is null) return new Identification(true, Type: type);

        var result = (await idfy.ValidateAsync(file, docType, ct)).Result!;
        if (result.IsReadable != true)
            return new Identification(false, Messages.UploadDocuments.CopyUnreadable);
        if (!string.Equals(result.DetectedDocType, docType, StringComparison.OrdinalIgnoreCase))
            return new Identification(false, result.DetectedDocType is { Length: > 0 } detected
                ? Messages.UploadDocuments.ReadsAsAnother(IdfyDocTypes.Named(detected), IdfyDocTypes.Named(docType))
                : null);
        return new Identification(true);
    }
}

/// <summary>OCR by IDfy for a PAN card, an Aadhaar, a driving licence, a passport, a voter ID and a cheque.</summary>
public sealed class IdfyOcr(IdfyClient idfy) : IOcrService
{
    public async Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default)
    {
        if (kind == DocumentKind.Cheque) return Cheque((await idfy.ExtractChequeAsync(file, ct)).Result!.ExtractionOutput);

        switch (IdfyDocTypes.Of(kind, type))
        {
            case "ind_pan":
                var pan = (await idfy.ExtractPanAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Pan: pan?.IdNumber ?? "", Name: pan?.NameOnCard ?? "", Dob: Dobs.Of(pan?.DateOfBirth));

            case "ind_aadhaar":
                // What the QR code says is what UIDAI signed, so it is read first -
                // unless it carries only part of the number, as a secure QR code does.
                var aadhaar = (await idfy.ExtractAadhaarAsync(file, consent, ct)).Result!;
                var card = AadhaarNumbers.IsWhole(Digits(aadhaar.QrOutput?.IdNumber)) ? aadhaar.QrOutput : aadhaar.ExtractionOutput;
                return new OcrReading(Name: card?.NameOnCard ?? "", Address: WithPin(card?.Address, card?.Pincode),
                    IdNumber: Digits(card?.IdNumber), Gender: Genders.Of(card?.Gender ?? aadhaar.ExtractionOutput?.Gender),
                    Dob: Dobs.Of(card?.DateOfBirth ?? aadhaar.ExtractionOutput?.DateOfBirth), Number: Digits(card?.IdNumber),
                    District: Place(card?.District ?? aadhaar.ExtractionOutput?.District), State: Place(card?.State ?? aadhaar.ExtractionOutput?.State));

            case "ind_driving_license":
                var licence = (await idfy.ExtractDrivingLicenceAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Name: licence?.NameOnCard ?? "", Address: WithPin(licence?.Address, licence?.Pincode),
                    IdNumber: licence?.IdNumber ?? "", Dob: Dobs.Of(licence?.DateOfBirth),
                    Number: licence?.IdNumber ?? "", Expiry: LicenceExpiry(licence),
                    District: Place(licence?.District), State: Place(licence?.State));

            case "ind_passport":
                // A passport is verified by its file number, not its passport number.
                var passport = (await idfy.ExtractPassportAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Name: passport?.NameOnCard ?? "", Address: WithPin(passport?.Address, passport?.Pincode),
                    IdNumber: passport?.FileNumber ?? "", Gender: Genders.Of(passport?.Gender), Dob: Dobs.Of(passport?.DateOfBirth),
                    Number: passport?.PassportNumber ?? "", Expiry: Dobs.Of(passport?.DateOfExpiry),
                    District: Place(passport?.District), State: Place(passport?.State));

            case "ind_voter_id":
                var voter = (await idfy.ExtractVoterIdAsync(file, ct)).Result!.ExtractionOutput;
                return new OcrReading(Name: voter?.NameOnCard ?? "", Address: WithPin(voter?.Address, voter?.Pincode),
                    IdNumber: voter?.IdNumber ?? "", Gender: Genders.Of(voter?.Gender), Dob: Dobs.Of(voter?.DateOfBirth), Number: voter?.IdNumber ?? "",
                    District: Place(voter?.District), State: Place(voter?.State));

            default:
                // IDfy reads no other document: nothing is read off the copy, and the
                // holder's own details stand in, as when OCR is switched off.
                return new OcrReading(Pan: subject.Pan, Name: subject.Name, Dob: subject.Dob);
        }
    }

    /// <summary>
    /// When a licence runs out, as far as the copy tells. OCR sometimes reads an
    /// issue date as the validity, so a date that is an issue date, or not after the
    /// latest one, is not taken; the latest date left is. None left is empty, not a
    /// wrong date: Sarathi's own date replaces it once the licence is found there.
    /// </summary>
    internal static string LicenceExpiry(DrivingLicenceCard? card)
    {
        if (card is null) return "";
        static DateOnly? On(string? iso) =>
            DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        var issued = (card.IssueDates?.Values ?? []).Select(On).OfType<DateOnly>().ToList();
        var latestIssue = issued.Count > 0 ? issued.Max() : (DateOnly?)null;
        var expiry = new[] { card.DateOfValidity }.Concat(card.Validity?.Values ?? [])
            .Select(On).OfType<DateOnly>()
            .Where(d => latestIssue is not { } i || d > i)
            .DefaultIfEmpty().Max();
        return expiry == default ? "" : expiry.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    }

    // What a cheque says: the account as it is shown (masked, with its IFSC and
    // bank), and field by field for Bank Details to be filled from.
    private static OcrReading Cheque(ChequeLeaf? cheque)
    {
        var account = OnlyDigits(cheque?.AccountNo);
        var ifsc = (cheque?.IfscCode ?? "").Trim().ToUpperInvariant();
        var bank = (cheque?.BankName ?? "").Trim();

        var shown = new List<string>();
        if (account.Length > 0) shown.Add(MaskedAccount(account));
        if (ifsc.Length > 0) shown.Add(ifsc);
        if (bank.Length > 0) shown.Add(bank);

        var fields = new ChequeFields(
            AccountNumber: account,
            Ifsc: ifsc,
            Micr: OnlyDigits(cheque?.MicrCode),
            Number: OnlyDigits(cheque?.MicrChequeNumber),
            Date: Dobs.Of(cheque?.DateOfIssue));
        return new OcrReading(Name: cheque?.AccountName ?? "", Account: string.Join(", ", shown), Bank: bank, Cheque: fields);
    }

    // An account number with all but its last four digits masked.
    private static string MaskedAccount(string account)
    {
        if (account.Length <= 4) return account;
        return new string('X', account.Length - 4) + account[^4..];
    }

    private static string OnlyDigits(string? value) => new((value ?? "").Where(char.IsAsciiDigit).ToArray());

    // An address with its PIN code at the end. IDfy gives the PIN in a field of its
    // own, and the address it gives may or may not carry it: a passport's and a
    // voter ID's do not. With no address read, a PIN alone is not one.
    internal static string WithPin(string? address, string? pincode)
    {
        var lines = (address ?? "").Trim();
        var pin = OnlyDigits(pincode);
        if (lines.Length == 0 || pin.Length == 0) return lines;
        if (lines.Replace(" ", "").Contains(pin)) return lines;
        return lines.TrimEnd(',', ' ') + " " + pin;
    }

    // A district or a state as it was read; empty when the proof gave none.
    private static string Place(string? name) => (name ?? "").Trim();

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
public sealed class IdfyVerification(IdfyClient idfy) : IVerificationService
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
                if (ParseDob(holderDob) is not { } licenceDob) return NotAsked(sarathi, "the holder's date of birth is not on record in a form it can be asked with");
                return Licence((await idfy.VerifyDrivingLicenceAsync(number, licenceDob, ct)).Result!.SourceOutput, sarathi);

            case "Passport":
                const string seva = "Passport Seva";
                // The file number is on the passport's last page, which a single copy may not show.
                if (number.Length == 0) return NotAsked(seva, "the passport file number is not on the copy — it is on the last page");
                if (ParseDob(holderDob) is not { } passportDob) return NotAsked(seva, "the holder's date of birth is not on record in a form it can be asked with");
                return Answer((await idfy.VerifyPassportAsync(number, passportDob, ct)).Result!, seva);

            case "Voter ID":
                const string commission = "the Election Commission";
                if (number.Length == 0) return NotAsked(commission, "the EPIC number could not be read off the copy");
                // IDfy may read the EPIC number partly masked, and a masked one cannot be looked up.
                if (number.Contains('*')) return NotAsked(commission, "the EPIC number was read partly masked");
                return Answer((await idfy.VerifyVoterIdAsync(number, ct)).Result!, commission);

            default:
                // IDfy has no source check for an Aadhaar.
                return NotAsked("", "there is no issuer check for this proof");
        }
    }

    // IDfy has no bank-account check.
    public Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default) =>
        Task.FromResult(NotAsked("", "there is no bank account check"));

    private static Verification Answer(Sourced<SourceStatus> result, string verifier) =>
        new(string.Equals(result.SourceOutput?.Status, "id_found", StringComparison.OrdinalIgnoreCase), verifier);

    // Sarathi's validity is the licence's own, read off no copy: the later of the
    // non-transport and transport dates it holds.
    private static Verification Licence(LicenceSource? source, string verifier)
    {
        var found = string.Equals(source?.Status, "id_found", StringComparison.OrdinalIgnoreCase);
        var until = new[] { source?.NtValidityTo, source?.TValidityTo }
            .Select(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var on) ? on : (DateOnly?)null)
            .OfType<DateOnly>().DefaultIfEmpty().Max();
        return new(found, verifier, Expiry: found && until != default ? until.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : "",
            Standing: found ? source?.DlStatus ?? "" : "", Gender: found ? Genders.Of(source?.Gender) : "");
    }

    private static Verification NotAsked(string verifier, string why) => new(false, verifier, why);

    /// <summary>A date of birth as IDfy returns it, or null when it cannot be read.</summary>
    private static DateOnly? ParseDob(string dob) =>
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

/// <summary>
/// The PAN-proof face match by IDfy's face compare, the PAN copy sent first. No
/// face found on either copy, or a review IDfy recommends, is no answer either
/// way: a person looks at it.
/// </summary>
public sealed class IdfyFaceMatch(IdfyClient idfy) : IFaceMatchService
{
    public async Task<FaceMatch> CompareAsync(UploadFile panCopy, UploadFile proof, CancellationToken ct = default)
    {
        var result = (await idfy.CompareFacesAsync(panCopy, proof, ct)).Result!;
        var score = result.MatchScore ?? 0;
        if (result.Image1?.FaceDetected == false) return new FaceMatch(false, score, "no face could be found on the PAN copy", PanFace: false);
        if (result.Image2?.FaceDetected == false) return new FaceMatch(false, score, "no face could be found on the proof of address", ProofFace: false);
        if (result.ReviewRecommended == true)
        {
            var quality = string.Join(", ", new[] { ("PAN copy", result.Image1?.FaceQuality), ("proof", result.Image2?.FaceQuality) }
                .Where(q => !string.IsNullOrWhiteSpace(q.Item2)).Select(q => $"{q.Item1} face quality {q.Item2}"));
            return new FaceMatch(result.IsAMatch == true, score,
                "IDfy recommends a person looks at the faces" + (quality.Length > 0 ? $" ({quality})" : ""));
        }
        return new FaceMatch(result.IsAMatch == true, score);
    }
}
