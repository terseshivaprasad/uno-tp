namespace UnoTP.ViewModels;

/// <summary>What the upload step's form posts. Every post carries all of it; a
/// field the page shut is not posted, and stays null.</summary>
public sealed class UploadForm
{
    public string? AppType { get; set; }
    /// <summary>The proof of address type, posted only while it is chosen rather than set from the upload.</summary>
    public string? PoaType { get; set; }

    /// <summary>Where post goes: "same" as the permanent address, or "different".</summary>
    public string? Mailing { get; set; }

    /// <summary>The communication address proof type, posted only while it is chosen.</summary>
    public string? MailType { get; set; }
    public string? PayMode { get; set; }
    public string? Sourcing { get; set; }
    public string? SourceCode { get; set; }
    public string? SubBroker { get; set; }
    public string? EmpCode { get; set; }
    public string? EmpCompany { get; set; }
    public string? EmpHolder { get; set; }
    public string? EmpRelation { get; set; }
    public string? EmpProofType { get; set; }
    public string? FormNo { get; set; }

    /// <summary>The slot whose Upload was pressed.</summary>
    public string? Slot { get; set; }

    /// <summary>The control whose change redrew the page, so the page comes back to it.</summary>
    public string? Refresh { get; set; }

    /// <summary>The name as printed on the investor's PAN card, typed for NSDL to be asked again.</summary>
    public string? NsdlName { get; set; }

    /// <summary>The investor's 12-digit Aadhaar number, typed where OCR could not read it.</summary>
    public string? AadhaarNo { get; set; }
}
