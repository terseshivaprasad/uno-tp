using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// What t_FD_BT_KYC_document keeps of the outside checks on a document: the number
/// and expiry read off it, and whether it was identified, read, confirmed, masked
/// and its face compared. Every flag is empty for a document no check was run on.
/// </summary>
internal sealed record DocumentFlags(
    string? Number, DateTime? Expiry, bool? Masked, bool? OcrAsked, bool? Read, bool? Verified,
    bool? Identified, string? IdentifiedAs, bool? FaceCompared, bool? FaceFound, int? FaceScore)
{
    private static readonly DocumentFlags None = new(null, null, null, null, null, null, null, null, null, null, null);

    public static DocumentFlags Of(StoredDoc doc)
    {
        if (doc.Checks is not { } checks) return None;

        // What was read counts as confirmed once every check on the document came back clean.
        var verified = checks.Read && doc.CheckKind == "ok";
        return new DocumentFlags(
            Number: EmptyAsNull(checks.Number), Expiry: Dates.ParseDdMmYyyy(checks.Expiry), Masked: checks.Masked,
            OcrAsked: checks.OcrAsked, Read: checks.Read, Verified: verified,
            Identified: checks.IdentifiedAs.Length > 0, IdentifiedAs: EmptyAsNull(checks.IdentifiedAs),
            FaceCompared: checks.Face is not null, FaceFound: checks.Face?.Found, FaceScore: checks.Face?.Score);
    }

    // Nothing read leaves the column empty.
    private static string? EmptyAsNull(string value)
    {
        if (value.Length == 0) return null;
        return value;
    }
}
