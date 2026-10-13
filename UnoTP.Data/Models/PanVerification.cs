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

/// <param name="PanOk">NSDL holds the PAN: it is a real one.</param>
/// <param name="DobOk">The date of birth is the one held against it.</param>
/// <param name="NameOk">The name is the one held against it. Never true unless the PAN and the date of birth are.</param>
public sealed record PanVerification(bool PanOk, bool DobOk, bool NameOk)
{
    /// <summary>The PAN is a real one and the date of birth is its holder's.</summary>
    public bool PairOk => PanOk && DobOk;
}
