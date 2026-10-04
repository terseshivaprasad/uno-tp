namespace UnoTP.Models;

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

    /// <summary>The nominees on record against a folio, from the investor's earlier deposits, most recent first; empty for a folio with none.</summary>
    Task<IReadOnlyList<NomineeOnRecord>> NomineesByFolioAsync(string folio, CancellationToken ct = default);

    /// <summary>The repayment accounts on record against a folio, from the investor's earlier deposits, each once, most recently used first.</summary>
    Task<IReadOnlyList<AccountOnRecord>> AccountsByFolioAsync(string folio, CancellationToken ct = default);
}

/// <summary>What the register holds against a folio, document by document.</summary>
public sealed record DocsOnRecord(bool Pan, bool Photo, bool Poa);

/// <summary>A nominee named on an earlier deposit of the folio: picked on Investor Information to fill the nominee's fields.</summary>
/// <param name="Dob">dd-MM-yyyy; empty when the record holds none.</param>
/// <param name="GuardianName">For a minor; empty otherwise.</param>
/// <param name="UsedOn">The deposit or application it was named on.</param>
public sealed record NomineeOnRecord(string Name, string Dob, string Relation, string GuardianName, string UsedOn);

/// <summary>A repayment account used on an earlier deposit of the folio: picked on Bank Details to fill the repayment fields.</summary>
/// <param name="UsedOn">The deposit or application it was used on.</param>
public sealed record AccountOnRecord(string Ifsc, string AccountNumber, string Bank, string Branch, string UsedOn);

/// <summary>A folio the register holds.</summary>
/// <param name="Dob">dd-MM-yyyy, or empty when the register holds no date of birth.</param>
/// <param name="Address">Empty when the register holds none.</param>
public sealed record FolioRecord(
    string Pan, string Dob, string Folio, string Name, string Gender,
    string Address, DocsOnRecord Docs, string Note, string Source = "");
