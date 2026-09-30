namespace UnoTP.Backend.External;

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

/// <summary>POST match { name, other } → { outcome, score }: outcome match, partial or mismatch.</summary>
public sealed class NameMatchClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Name match"), INameMatchService
{
    public const string Name = "NameMatch";

    public Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default) =>
        Ask(async () =>
        {
            using var request = Request(HttpMethod.Post, "match", JsonBody(new { name, other }));
            using var response = await SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var answer = await Read<Answer>(response, ct);
            var outcome = (answer.Outcome ?? "").Trim().ToLowerInvariant();
            if (outcome is not (NameMatchOutcome.Match or NameMatchOutcome.Partial)) outcome = NameMatchOutcome.Mismatch;
            return new NameMatchResult(outcome, answer.Score ?? 0);
        }, ct);

    private sealed record Answer(string? Outcome, int? Score);
}
