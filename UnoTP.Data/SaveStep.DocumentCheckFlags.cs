using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// What t_FD_BT_KYC_document keeps of the outside checks on a document: the number
/// and expiry read off it, and whether it was identified, read, confirmed, masked
/// and its face compared, with the task id of each request IDfy answered about the
/// document and the application's group id they were sent under. Every flag is
/// empty for a document no check was run on.
///
/// The row of a holder's PAN copy also says what NSDL and the PAN-Aadhaar link said
/// of the PAN, and the row of their proof of address how its name and date of birth
/// stand against the PAN's. Each is empty where that check was not asked or gave no
/// answer. These columns are added by db/alter_transaction_tables.sql.
/// </summary>
internal sealed record DocumentFlags(
    string? Number, DateTime? Expiry, bool? Masked, bool? OcrAsked, bool? Read, bool? Verified,
    bool? Identified, string? IdentifiedAs, bool? FaceCompared, bool? FaceFound, int? FaceScore,
    string? IdfyGroupId = null, string? IdentifyTaskId = null, string? ExtractTaskId = null,
    string? VerifyTaskId = null, string? FaceCompareTaskId = null)
{
    /// <summary>The source NSDL is written as, on a PAN copy's row.</summary>
    public const string Nsdl = "NSDL";

    // ----- On the PAN copy's row: what NSDL said
    public bool? NsdlPan { get; init; }
    public bool? NsdlDob { get; init; }
    public bool? NsdlName { get; init; }
    public string? NsdlRemark { get; init; }

    // ----- On the PAN copy's row: the PAN-Aadhaar link
    public bool? PanAadhaarLinked { get; init; }
    public string? LinkTaskId { get; init; }

    // ----- On the proof of address's row: its name and date of birth against the PAN's
    public string? ProofName { get; init; }
    public bool? ProofDob { get; init; }

    // ----- Whether the document's own source confirmed it, and which source: NSDL
    // for a PAN copy, the issuer for a proof of address.
    public bool? SourceVerified { get; init; }
    public string? Source { get; init; }

    private static readonly DocumentFlags None = new(null, null, null, null, null, null, null, null, null, null, null);

    /// <param name="docType">Which of the holder's documents it is: "pan", "poa", "mail" and the rest.</param>
    /// <param name="holder">What the checks on the holder's PAN said; null where none was run.</param>
    public static DocumentFlags Of(StoredDoc doc, string docType, HolderChecks? holder)
    {
        if (doc.Checks is not { } checks) return None;

        // What was read counts as confirmed once every check on the document came back clean.
        var verified = checks.Read && doc.CheckKind == "ok";
        var flags = new DocumentFlags(
            Number: EmptyAsNull(checks.Number), Expiry: Dates.ParseDdMmYyyy(checks.Expiry), Masked: checks.Masked,
            OcrAsked: checks.OcrAsked, Read: checks.Read, Verified: verified,
            Identified: checks.IdentifiedAs.Length > 0, IdentifiedAs: EmptyAsNull(checks.IdentifiedAs),
            FaceCompared: checks.Face is not null, FaceFound: checks.Face?.Found, FaceScore: checks.Face?.Score,
            IdfyGroupId: EmptyAsNull(checks.IdfyGroupId), IdentifyTaskId: EmptyAsNull(checks.IdentifyTaskId),
            ExtractTaskId: EmptyAsNull(checks.ExtractTaskId), VerifyTaskId: EmptyAsNull(checks.VerifyTaskId),
            FaceCompareTaskId: EmptyAsNull(checks.Face?.TaskId ?? ""))
        {
            SourceVerified = checks.VerifierConfirmed, Source = EmptyAsNull(checks.Verifier),
        };

        if (docType == "pan") return WithPanChecks(flags, holder);
        if (docType == "poa") return WithProofMatch(flags, holder);
        return flags;
    }

    // A PAN copy's row: NSDL's three answers and its remark, and the PAN-Aadhaar
    // link. NSDL is the PAN's source: the copy is verified with it when NSDL holds
    // the PAN against both the date of birth and the name.
    private static DocumentFlags WithPanChecks(DocumentFlags flags, HolderChecks? holder)
    {
        if (holder?.Nsdl is { } nsdl)
        {
            flags = flags with
            {
                NsdlPan = nsdl.PanOk, NsdlDob = nsdl.DobOk, NsdlName = nsdl.NameOk, NsdlRemark = EmptyAsNull(nsdl.Remark),
                SourceVerified = nsdl.PanOk && nsdl.DobOk && nsdl.NameOk, Source = Nsdl,
            };
        }
        if (holder?.Link is { } link)
        {
            flags = flags with { PanAadhaarLinked = link.Linked, LinkTaskId = EmptyAsNull(link.TaskId) };
        }
        return flags;
    }

    // A proof of address's row: how its name (MATCH, PARTIAL or MISMATCH) and its
    // date of birth stand against the PAN's.
    private static DocumentFlags WithProofMatch(DocumentFlags flags, HolderChecks? holder)
    {
        if (holder?.Proof is not { } proof) return flags;
        return flags with { ProofName = EmptyAsNull(proof.Name.ToUpperInvariant()), ProofDob = proof.Dob };
    }

    // Nothing read leaves the column empty.
    private static string? EmptyAsNull(string value)
    {
        if (value.Length == 0) return null;
        return value;
    }
}
