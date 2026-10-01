namespace UnoTP.Models;

/// <summary>
/// PAN verification: whether NSDL holds the PAN, holds it against the date of
/// birth, and holds it against the name. Asked of every holder's PAN on Upload
/// Documents.
/// </summary>
public interface IPanVerificationService
{
    Task<PanVerification> VerifyAsync(PanToVerify pan, CancellationToken ct = default);
}

/// <summary>A holder's PAN as the application has it, with where it is being asked from.</summary>
/// <param name="HolderType">01 the investor, 02 the second holder, 03 the third.</param>
/// <param name="Dob">dd-MM-yyyy.</param>
public sealed record PanToVerify(string AppNo, string HolderType, string Pan, string Dob, string Name);

/// <param name="PairOk">The PAN is a real one and the date of birth is its holder's.</param>
/// <param name="NameOk">The name is the one held against it.</param>
public sealed record PanVerification(bool PairOk, bool NameOk);
