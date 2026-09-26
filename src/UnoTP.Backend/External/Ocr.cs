namespace UnoTP.Backend.External;

/// <summary>OCR: what a document says.</summary>
public interface IOcrService
{
    /// <param name="type">What it is within its kind - the proof of address type -
    /// or empty.</param>
    /// <param name="subject">Whose document it is expected to be, sent along so the
    /// reading is matched and logged against the holder.</param>
    /// <param name="consent">Whether the holder has consented to their Aadhaar
    /// being processed; an Aadhaar is not read without it.</param>
    Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default);
}

public sealed record OcrSubject(string Pan, string Dob, string Name);

/// <summary>What was read. Only the fields the document carries are filled.</summary>
/// <param name="IdNumber">The document's own number: an Aadhaar or licence number,
/// a passport file number, a voter ID. An Aadhaar number is never stored.</param>
/// <param name="Account">A cheque's account, masked, with its IFSC and branch.</param>
/// <param name="Bank">The bank a cheque is drawn on.</param>
/// <param name="Gender">"Male", "Female" or "Transgender", as an Aadhaar prints it; empty otherwise.</param>
/// <param name="Number">The number printed on a proof, as it is shown: a passport
/// number, a licence number, a voter ID's EPIC number, an Aadhaar number.</param>
/// <param name="Expiry">When a passport or a driving licence runs out, as dd-MM-yyyy; empty otherwise.</param>
/// <param name="Dob">The date of birth the document prints - a PAN card, or a proof of
/// address that carries one - as dd-MM-yyyy; empty otherwise.</param>
/// <param name="Cheque">What a cheque prints, field by field, for Bank Details to be
/// filled from; null for anything else.</param>
public sealed record OcrReading(
    string Pan = "", string Name = "", string Address = "",
    string IdNumber = "", string Account = "", string Bank = "", string Gender = "", string Dob = "",
    string Number = "", string Expiry = "", ChequeFields? Cheque = null);

/// <summary>A cheque's fields as OCR reads them.</summary>
/// <param name="AccountNumber">The full account number, digits only.</param>
/// <param name="Ifsc">The branch's IFSC.</param>
/// <param name="Micr">The MICR code on the cheque's bottom line.</param>
/// <param name="Number">The six-digit cheque number.</param>
/// <param name="Date">The date written on the cheque, dd-MM-yyyy; empty when none is.</param>
public sealed record ChequeFields(string AccountNumber, string Ifsc, string Micr, string Number, string Date);

/// <summary>Dates of birth however a card or a service writes them, as the app keeps them.</summary>
public static class Dobs
{
    private static readonly string[] Formats = ["dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "dd.MM.yyyy", "d-M-yyyy", "d/M/yyyy"];

    /// <summary>The date as dd-MM-yyyy, or empty when it cannot be read as a date.</summary>
    public static string Of(string? value) =>
        DateTime.TryParseExact((value ?? "").Trim(), Formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date) ? date.ToString("dd-MM-yyyy") : "";
}

/// <summary>A gender however a register or a card spells it, as the app names it.</summary>
public static class Genders
{
    public const string Male = "Male";
    public const string Female = "Female";
    public const string Transgender = "Transgender";

    /// <summary>"F", "FEMALE" or "Female" read as <see cref="Female"/>, and so on; empty when unknown.</summary>
    public static string Of(string? value) => (value ?? "").Trim().ToUpperInvariant() switch
    {
        "F" or "FEMALE" => Female,
        "M" or "MALE" => Male,
        "T" or "TRANSGENDER" => Transgender,
        _ => "",
    };
}

/// <summary>POST read (multipart: file, kind, type, pan, dob, name, consent) → OcrReading.</summary>
public sealed class OcrClient(HttpClient http, IPartner partner) : ExternalClient(http, partner, "OCR"), IOcrService
{
    public const string Name = "Ocr";

    public Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default) =>
        Ask(() => Send<OcrReading>(HttpMethod.Post, "read",
            Form(file, ("kind", kind.ToString()), ("type", type), ("pan", subject.Pan), ("dob", subject.Dob),
                ("name", subject.Name), ("consent", consent ? "true" : "false")), ct), ct);
}
