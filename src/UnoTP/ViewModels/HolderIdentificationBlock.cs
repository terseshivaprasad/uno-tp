namespace UnoTP.ViewModels;

/// <summary>
/// One holder's identification, as the shared _HolderIdentification partial draws it:
/// the search fields, and once the register answers, the record and - for a PAN
/// with no folio - the PAN copy and NSDL.
/// </summary>
/// <param name="Search">The holder's search, checked from what the session holds.</param>
/// <param name="Ids">What the block's element ids start with, so two blocks on one page do not clash.</param>
/// <param name="Url">The address each step posts to, by its name: Check, Again, Verify, Retry.</param>
/// <param name="Prefix">Null when the block posts with forms of its own, as on
/// Investor Identification. Inside a larger form - a joint holder on Investor
/// Information - the prefix its fields carry there, and its buttons post that form.</param>
/// <param name="FolioSearch">Whether the holder can be searched by folio as well as by PAN and date of birth.</param>
/// <param name="NameRetry">Whether a name NSDL does not match can be typed from the PAN card and tried again.</param>
/// <param name="Locked">Once a joint holder is added, the record only: no searching again.</param>
/// <param name="EstablishHere">Whether a PAN with no folio is established in the block,
/// from its PAN copy. A joint holder's is put to NSDL once the copy is filed under
/// their documents instead, and the block only says so.</param>
/// <param name="KnownName">The name the holder goes by now, where NSDL has since verified one.</param>
public sealed record HolderIdentificationBlock(
    InvestorIdentificationViewModel Search,
    string Ids,
    Func<string, string> Url,
    string? Prefix = null,
    bool FolioSearch = true,
    bool NameRetry = true,
    bool Locked = false,
    bool EstablishHere = true,
    string? KnownName = null)
{
    public bool Embedded => Prefix is not null;

    /// <summary>A field's name: as the step's own form posts it, or under the prefix.</summary>
    public string Name(string field) => Prefix is null ? field.ToLowerInvariant() : Prefix + field;
}
