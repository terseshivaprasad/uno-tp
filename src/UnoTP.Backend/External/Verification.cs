namespace UnoTP.Backend.External;

/// <summary>
/// Verification with whoever answers for a document: the issuer of a proof of
/// address (UIDAI, Passport Seva, Sarathi, the Election Commission), or the bank a
/// cheque is drawn on.
/// </summary>
public interface IVerificationService
{
    /// <summary>Whether the issuer of this kind of proof holds the document as it was read, for the holder.</summary>
    /// <param name="holderDob">The holder's date of birth, dd-MM-yyyy: the proof has to be theirs.</param>
    Task<Verification> ConfirmProofAsync(string proofType, OcrReading reading, string holderDob, CancellationToken ct = default);

    /// <summary>Whether the bank holds this account.</summary>
    Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default);
}

/// <param name="Verifier">Who answers for it; empty when nobody does, as for a utility bill.</param>
/// <param name="NotAsked">Why the verifier could not be asked, when it could not -
/// the number it is asked with was not on the copy, or not readable. Nothing was
/// found either way, so this is not a refusal.</param>
public sealed record Verification(bool Confirmed, string Verifier, string? NotAsked = null);

/// <summary>POST proof { proofType, reading, holderDob } and POST account { account, bank } → Verification.</summary>
public sealed class VerificationClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Verification"), IVerificationService
{
    public const string Name = "Verification";

    public Task<Verification> ConfirmProofAsync(string proofType, OcrReading reading, string holderDob, CancellationToken ct = default) =>
        Ask(() => Send<Verification>(HttpMethod.Post, "proof", Body(new { proofType, reading, holderDob }), ct), ct);

    public Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default) =>
        Ask(() => Send<Verification>(HttpMethod.Post, "account", Body(new { account, bank }), ct), ct);
}
