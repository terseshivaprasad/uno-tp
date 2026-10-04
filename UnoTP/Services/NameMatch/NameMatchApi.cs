using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.NameMatch;

/// <summary>The name match API, from the "NameMatch" section of appsettings.</summary>
public sealed class NameMatchOptions : IApiAddress
{
    public const string Section = "NameMatch";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. The path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: whether two names are the same person's.</summary>
    public string MatchPath { get; set; } = "";
}

/// <summary>
/// POST MatchPath { SourceName, TargetName } → { status, error_code, error_message }.
/// SUCCESS is a match. FAIL with no error code is the names not matching (its message
/// says the criteria were not met); FAIL with an error code is the service failing,
/// which is an outage and not a mismatch.
/// </summary>
public sealed class NameMatchClient(HttpClient http, IPartner partner, IOptions<NameMatchOptions> options)
    : ExternalClient(http, partner, Service), INameMatchService
{
    private const string Service = "Name match";

    private const string Matched = "SUCCESS";
    private const string NotMatched = "FAIL";

    public Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default) =>
        Ask(async () =>
        {
            var body = new MatchRequest { SourceName = name, TargetName = other };
            using var request = Request(HttpMethod.Post, options.Value.MatchPath, JsonBody(body));
            using var response = await SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var answer = await Read<Answer>(response, ct);
            var status = (answer.Status ?? "").Trim().ToUpperInvariant();
            var errorCode = (answer.ErrorCode ?? "").Trim();
            var errorMessage = (answer.ErrorMessage ?? "").Trim();

            if (status == Matched) return new NameMatchResult(NameMatchOutcome.Match);
            if (status == NotMatched && errorCode.Length == 0) return new NameMatchResult(NameMatchOutcome.Mismatch);

            // A failure with an error code, or a status that is neither: nothing was compared.
            throw new ExternalServiceException(Service, errorMessage.Length > 0
                ? $"{Service} could not compare the names: {errorMessage}"
                : $"{Service} could not compare the names just now. Try again in a while.");
        }, ct);

    // The request, under the API's own names: the name read off the document, and
    // the holder's name it is compared with.
    private sealed class MatchRequest
    {
        [JsonPropertyName("SourceName")]
        public string SourceName { get; set; } = "";

        [JsonPropertyName("TargetName")]
        public string TargetName { get; set; } = "";
    }

    private sealed class Answer
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }
}
