namespace UnoTP.Services;

/// <summary>
/// Which outside services are switched on (Backend:Switches:{name}, true unless set
/// false). One switched off is not called: its check is skipped, the page goes on,
/// and the history says the check was not asked, for Operations. With the PAN
/// check off, NSDL is not asked about a PAN copy and the application goes on
/// without it. With Fetch from CKYC off, its button stays on the page, disabled,
/// and says the service is unavailable. With the cheque check off, a payment
/// instrument's copy is filed as handed over and nothing is read off it. Aadhaar masking and the way in cannot be
/// switched off; name screening has a switch of its own (NameScreening:ApiCall).
/// </summary>
public sealed class OutsideSwitches(IConfiguration config)
{
    // The name each check's switch goes under.
    public const string PanCheck = "PanCheck";
    public const string Identify = "Identify";
    public const string Ocr = "Ocr";
    public const string Verification = "Verification";
    public const string PanAadhaarLink = "PanAadhaarLink";
    public const string FaceMatch = "FaceMatch";
    public const string NameMatch = "NameMatch";
    public const string FetchCkyc = "FetchCkyc";
    public const string Cheque = "Cheque";

    /// <summary>The services a switch may turn off.</summary>
    public static readonly string[] Switchable =
    [
        PanCheck, Identify, Ocr, Verification, PanAadhaarLink, FaceMatch, NameMatch, FetchCkyc, Cheque,
    ];

    public bool IsOn(string name)
    {
        if (!Switchable.Contains(name)) return true;
        var setting = config[$"Backend:Switches:{name}"];
        if (string.IsNullOrWhiteSpace(setting)) return true;
        return !string.Equals(setting.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What the history says of a check that was not asked because its service is off.</summary>
    public const string Off = "switched off";
}
