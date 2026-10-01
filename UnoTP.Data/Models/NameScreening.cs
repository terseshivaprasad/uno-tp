namespace UnoTP.Models;

/// <summary>
/// The name screening service (NSA): whether a holder may invest online, by name and
/// date of birth (sanctions, watch, rejected and black lists). Asked of every holder
/// on Investor Information before the application goes on; one not allowed invests
/// offline, at a branch.
/// </summary>
public interface INameScreeningService
{
    Task<NameScreeningResult> ScreenAsync(NameScreeningRequest request, CancellationToken ct = default);
}

/// <summary>A holder to screen, with the application they are on.</summary>
/// <param name="HolderType">01 the investor, 02 the second holder, 03 the third.</param>
/// <param name="Dob">dd-MM-yyyy.</param>
public sealed record NameScreeningRequest(string AppNo, string HolderType, string Name, string Dob, string Mobile);

/// <param name="Allowed">Whether the holder may invest online.</param>
/// <param name="Reference">The service's reference for the check, kept on the application's KYC row.</param>
public sealed record NameScreeningResult(bool Allowed, string Reference = "")
{
    /// <summary>The reference of a screening that was switched off: nobody was asked, and the KYC row says it was skipped.</summary>
    public const string Skipped = "switched off";
}
