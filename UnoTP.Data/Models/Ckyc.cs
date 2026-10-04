namespace UnoTP.Models;

/// <summary>
/// CKYC search: whether CERSAI holds a KYC record for a PAN and date of birth.
/// Asked when the partner chooses Fetch from CKYC on Upload Documents; only an
/// investor it holds a record for takes that route.
/// </summary>
public interface ICkycService
{
    Task<CkycSearchResult> SearchAsync(CkycSearch search, CancellationToken ct = default);
}

/// <summary>Whose record is searched for, with the application it is for.</summary>
/// <param name="HolderType">01 the investor, 02 the second holder, 03 the third.</param>
/// <param name="Dob">dd-MM-yyyy.</param>
public sealed record CkycSearch(string AppNo, string HolderType, string Pan, string Dob);

/// <param name="Available">Whether CERSAI holds a CKYC record for the PAN.</param>
/// <param name="Name">The name on the record; empty when there is none.</param>
/// <param name="Reference">The record's CKYC reference number, which the application goes on with.</param>
/// <param name="Why">Why no record came back, as CERSAI says it; empty when one did.</param>
public sealed record CkycSearchResult(
    bool Available, string Name = "", string Reference = "", string Why = "");
