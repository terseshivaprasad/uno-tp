namespace UnoTP.Models;

/// <summary>
/// The investor register: the folios already held. The outside checks an
/// investor goes through - OCR of the PAN copy, NSDL - are services of their own
/// (see the External folder).
/// </summary>
public interface IInvestorApi
{
    /// <summary>The deposits a PAN is the first holder of, for the folio check (<see cref="FolioCheck"/>); empty for a PAN with no folio.</summary>
    Task<IReadOnlyList<FolioDeposit>> FolioDepositsByPanAsync(string pan, CancellationToken ct = default);

    /// <summary>
    /// The folio the folio master holds a PAN as the first holder of; empty when it
    /// holds none. Asked for a PAN with no deposit, so an existing holder is not taken as new.
    /// </summary>
    Task<string> FolioOfFirstHolderAsync(string pan, CancellationToken ct = default);

    /// <summary>The deposits on a folio, with their first holder, for the folio check; empty for a folio that is not held.</summary>
    Task<IReadOnlyList<FolioDeposit>> FolioDepositsByFolioAsync(string folio, CancellationToken ct = default);

    /// <summary>The folio under this number, or null.</summary>
    Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default);

    /// <summary>
    /// The folio as its data source has it: the folio master's record, with where the
    /// holder's latest KYC is kept (found for the PAN and date of birth), and the
    /// address and the documents that source holds for the folio. A document counts as
    /// on record only once it is verified there; a photograph, which nothing verifies,
    /// counts when it is there. Null for a folio the master does not hold.
    /// </summary>
    /// <param name="dob">dd-MM-yyyy.</param>
    Task<FolioRecord?> FolioOnRecordAsync(string folio, string pan, string dob, CancellationToken ct = default);

    /// <summary>
    /// The KYC details held for a folio where its latest KYC is kept, to open Investor
    /// Information with so they are not typed again. Null where the source holds none,
    /// or is one the details are not read from yet. The holder type is left empty:
    /// whoever asks knows which holder it is for. A mailing address the source holds
    /// comes with them, as the communication address.
    /// </summary>
    /// <param name="source">Where the folio's latest KYC is kept (<see cref="KycSources"/>).</param>
    Task<HolderDetails?> KycOnFolioAsync(string source, string folio, CancellationToken ct = default);

    /// <summary>The nominees named on a deposit of the folio, for its renewal; empty where it names none.</summary>
    Task<IReadOnlyList<NomineeOnRecord>> NomineesOnDepositAsync(string folio, string depositNumber, CancellationToken ct = default);

    /// <summary>The repayment accounts on a deposit of the folio, for its renewal, each once.</summary>
    Task<IReadOnlyList<AccountOnRecord>> AccountsOnDepositAsync(string folio, string depositNumber, CancellationToken ct = default);
}

/// <summary>What is on record for a folio, document by document: verified copies, and a photograph.</summary>
public sealed record DocsOnRecord(bool Pan, bool Photo, bool Poa);

/// <summary>A nominee named on the deposit a renewal renews: picked on Investor Information to fill the nominee's fields.</summary>
/// <param name="Dob">dd-MM-yyyy; empty when the record holds none.</param>
/// <param name="GuardianName">For a minor; empty otherwise.</param>
/// <param name="UsedOn">The deposit or application it was named on.</param>
public sealed record NomineeOnRecord(string Name, string Dob, string Relation, string GuardianName, string UsedOn);

/// <summary>A repayment account used on the deposit a renewal renews: picked on Bank Details to fill the repayment fields.</summary>
/// <param name="UsedOn">The deposit or application it was used on.</param>
public sealed record AccountOnRecord(string Ifsc, string AccountNumber, string Bank, string Branch, string UsedOn);

/// <summary>The places a holder's latest KYC can be kept, as f_Data_Source names them.</summary>
public static class KycSources
{
    /// <summary>The FD system's common KYC table.</summary>
    public const string Common = "ORA";

    /// <summary>An application submitted through this app.</summary>
    public const string Bt = "BT";

    /// <summary>The folio master.</summary>
    public const string FolioMaster = "FHLD";
}

/// <summary>Where a holder's latest KYC is kept.</summary>
/// <param name="Source">ORA the common KYC table, BT an application submitted through this app, FHLD the folio master.</param>
/// <param name="Id">The row's id in that table; empty or 0 for the folio master, which is found by its folio.</param>
public sealed record KycSource(string Source, string Id);

/// <summary>A folio the register holds.</summary>
/// <param name="Dob">dd-MM-yyyy, or empty when the register holds no date of birth.</param>
/// <param name="Address">Empty when the register holds none.</param>
public sealed record FolioRecord(
    string Pan, string Dob, string Folio, string Name, string Gender,
    string Address, DocsOnRecord Docs, string Note, string Source = "");
