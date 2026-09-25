namespace UnoTP.Backend;

/// <summary>
/// The investor register: the folios already held. The outside checks an
/// investor goes through - OCR of the PAN copy, NSDL - are services of their own
/// (see the External folder).
/// </summary>
public interface IInvestorApi
{
    /// <summary>Every folio held against a PAN. More than one is a record Operations has to merge.</summary>
    Task<IReadOnlyList<FolioRecord>> FoliosByPanAsync(string pan, CancellationToken ct = default);

    /// <summary>The folio under this number, or null.</summary>
    Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default);
}

/// <summary>What the register holds against a folio, document by document.</summary>
public sealed record DocsOnRecord(bool Pan, bool Photo, bool Poa);

/// <summary>A folio the register holds.</summary>
/// <param name="Dob">dd-MM-yyyy, or empty when the register holds no date of birth.</param>
/// <param name="Address">Empty when the register holds none.</param>
public sealed record FolioRecord(
    string Pan, string Dob, string Folio, string Name, string Gender,
    string Address, DocsOnRecord Docs, string Note);
