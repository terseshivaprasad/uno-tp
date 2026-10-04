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

    /// <summary>
    /// The KYC details a compliant folio holds, to open Investor Information with so
    /// they are not typed again. Null for a folio that is not compliant, or holds none.
    /// The holder type is left empty: whoever asks knows which holder it is for.
    /// </summary>
    Task<HolderDetails?> KycOnFolioAsync(string folio, CancellationToken ct = default);

    /// <summary>The nominees named on a deposit of the folio, for its renewal; empty where it names none.</summary>
    Task<IReadOnlyList<NomineeOnRecord>> NomineesOnDepositAsync(string folio, string depositNumber, CancellationToken ct = default);

    /// <summary>The repayment accounts on a deposit of the folio, for its renewal, each once.</summary>
    Task<IReadOnlyList<AccountOnRecord>> AccountsOnDepositAsync(string folio, string depositNumber, CancellationToken ct = default);
}

/// <summary>What the register holds against a folio, document by document.</summary>
public sealed record DocsOnRecord(bool Pan, bool Photo, bool Poa);

/// <summary>A nominee named on the deposit a renewal renews: picked on Investor Information to fill the nominee's fields.</summary>
/// <param name="Dob">dd-MM-yyyy; empty when the record holds none.</param>
/// <param name="GuardianName">For a minor; empty otherwise.</param>
/// <param name="UsedOn">The deposit or application it was named on.</param>
public sealed record NomineeOnRecord(string Name, string Dob, string Relation, string GuardianName, string UsedOn);

/// <summary>A repayment account used on the deposit a renewal renews: picked on Bank Details to fill the repayment fields.</summary>
/// <param name="UsedOn">The deposit or application it was used on.</param>
public sealed record AccountOnRecord(string Ifsc, string AccountNumber, string Bank, string Branch, string UsedOn);

/// <summary>A folio the register holds.</summary>
/// <param name="Dob">dd-MM-yyyy, or empty when the register holds no date of birth.</param>
/// <param name="Address">Empty when the register holds none.</param>
/// <param name="Compliant">The folio's KYC is complete: its details on record open Investor Information.</param>
public sealed record FolioRecord(
    string Pan, string Dob, string Folio, string Name, string Gender,
    string Address, DocsOnRecord Docs, string Note, string Source = "", bool Compliant = false);
