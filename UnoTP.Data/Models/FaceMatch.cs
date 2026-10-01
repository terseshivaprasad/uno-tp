namespace UnoTP.Models;

/// <summary>
/// Face match: whether the photograph on a holder's PAN copy is the same person as
/// the one on their proof of address. For now its answer is shown, not acted on:
/// a copy is filed whatever it says.
/// </summary>
public interface IFaceMatchService
{
    Task<FaceMatch> CompareAsync(UploadFile panCopy, UploadFile proof, CancellationToken ct = default);
}

/// <param name="Matched">Whether the two faces are the same person.</param>
/// <param name="Score">How alike they are, 0 to 100.</param>
/// <param name="Unsure">Set when no answer either way could be given - no face found, or too close to call - and why.</param>
public sealed record FaceMatch(bool Matched, int Score, string? Unsure = null);
