using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.NameMatch;

/// <summary>The name match API, from the "NameMatch" section of appsettings.</summary>
public sealed class NameMatchOptions
{
    public const string Section = "NameMatch";

    /// <summary>The API's base path under the gateway. The path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: whether two names are the same person's.</summary>
    public string MatchPath { get; set; } = "";
}

/// <summary>POST MatchPath { name, other } → { outcome, score }: outcome match, partial or mismatch.</summary>
public sealed class NameMatchClient(HttpClient http, IPartner partner, IOptions<NameMatchOptions> options)
    : ExternalClient(http, partner, "Name match"), INameMatchService
{

    public Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default) =>
        Ask(async () =>
        {
            using var request = Request(HttpMethod.Post, options.Value.MatchPath, JsonBody(new { name, other }));
            using var response = await SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var answer = await Read<Answer>(response, ct);
            var outcome = (answer.Outcome ?? "").Trim().ToLowerInvariant();
            if (outcome is not (NameMatchOutcome.Match or NameMatchOutcome.Partial)) outcome = NameMatchOutcome.Mismatch;
            return new NameMatchResult(outcome, answer.Score ?? 0);
        }, ct);

    private sealed record Answer(string? Outcome, int? Score);
}
