using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// What the mock services pretend a scan says. They cannot read one, so they go
/// by its file name: a copy named for what it is passes identification, one named
/// as masked reads as a masked Aadhaar, and one named as a mismatch or a failure
/// is misread by OCR - which the issuer or the bank then does not confirm.
/// </summary>
internal static class MockScans
{
    /// <summary>The address the issuers hold, which a clean proof of address reads as.</summary>
    public const string Address = "Flat 12B, Shantiniketan CHS, Baner Road, Balewadi, Pune, Maharashtra 411045";

    /// <summary>The issue date a licence named "issuedate" has read as its validity.</summary>
    public const string LicenceIssued = "10-06-2011";

    public const string MisreadAddress = "Flat 21B, Shantiniketan CHS, Baner Road, Balewadi, Pune, Maharashtra 411054";

    /// <summary>The account the bank holds, masked the way the register masks one.</summary>
    public const string Account = "A/c ••••••7742 · IFSC HDFC0000123 · HDFC Bank, Baner, Pune";

    public const string MisreadAccount = "A/c ••••••7724 · IFSC HDFC0000123 · HDFC Bank, Baner, Pune";

    public const string Bank = "HDFC Bank";

    /// <summary>The cheque's fields: the account above in full, its branch, and the cheque itself.</summary>
    public static ChequeFields Cheque(bool misread) =>
        new(misread ? "50100012347724" : "50100012347742", "HDFC0000123", "411240012", "004512", DateTime.Today.ToString("dd-MM-yyyy"));

    public static bool Named(UploadFile file, params string[] words) =>
        words.Any(file.FileName.ToLowerInvariant().Contains);

    public static bool Misread(UploadFile file) => Named(file, "mismatch", "fail");
}
