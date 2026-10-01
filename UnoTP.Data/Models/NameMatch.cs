namespace UnoTP.Models;

/// <summary>
/// The name match service: whether two names are the same person's, allowing for
/// initials and a name left out. Upload Documents asks it with the name read off a
/// proof and the holder's name as the PAN holds it.
/// </summary>
public interface INameMatchService
{
    Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default);
}

/// <summary>What the service said of two names.</summary>
public static class NameMatchOutcome
{
    /// <summary>The same name, whatever the order of the words.</summary>
    public const string Match = "match";

    /// <summary>The same person, with an initial or a name left out (KARAN D MEHTA against KARAN DEEPAK MEHTA).</summary>
    public const string Partial = "partial";

    public const string Mismatch = "mismatch";

    /// <summary>Not asked: the service is switched off (OutsideSwitches); Operations compare.</summary>
    public const string NotAsked = "not-asked";
}

/// <param name="Outcome">A <see cref="NameMatchOutcome"/>.</param>
/// <param name="Score">The service's score out of 100, where it gives one; 0 otherwise.</param>
public sealed record NameMatchResult(string Outcome, int Score = 0)
{
    public bool IsMismatch => Outcome == NameMatchOutcome.Mismatch;
}
