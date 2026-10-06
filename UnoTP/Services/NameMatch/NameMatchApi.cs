using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
/// SUCCESS is a match. FAIL is the names not matching: it comes with an error code
/// and a message saying the criteria were not met. Any other status is the service
/// not having compared them, which is an outage and not a mismatch.
///
/// The call is made the way the IDfy client makes its own: the body goes whole,
/// with its length, as application/json, and the request carries nothing but the
/// headers every API gets (X-Client-Id). The gateway refuses it otherwise.
/// </summary>
public sealed class NameMatchClient(HttpClient http, IOptions<NameMatchOptions> options, ILogger<NameMatchClient> log) : INameMatchService
{
    private const string Service = "Name match";

    private const string Matched = "SUCCESS";
    private const string NotMatched = "FAIL";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default)
    {
        var answer = await PostAsync(options.Value.MatchPath, new MatchRequest { SourceName = name, TargetName = other }, ct);
        var status = (answer.Status ?? "").Trim().ToUpperInvariant();
        var errorMessage = (answer.ErrorMessage ?? "").Trim();

        if (status == Matched) return new NameMatchResult(NameMatchOutcome.Match);
        if (status == NotMatched) return new NameMatchResult(NameMatchOutcome.Mismatch);

        // A status that is neither: nothing was compared.
        throw new ExternalServiceException(Service, errorMessage.Length > 0
            ? Messages.OutsideServices.NamesNotComparedWith(Service, errorMessage)
            : Messages.OutsideServices.NamesNotCompared(Service));
    }

    private async Task<Answer> PostAsync(string path, MatchRequest body, CancellationToken ct)
    {
        try
        {
            // Sent whole, with its length: a streamed body is one more thing for a
            // proxy in the way to refuse.
            using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, Json));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using var response = await http.PostAsync(path, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Refused or failed on the way: said for support, with what came back.
                var said = await response.Content.ReadAsStringAsync(ct);
                log.LogWarning("Name match {Path} failed with {Status}: {Body}", path, (int)response.StatusCode, said.Length > 300 ? said[..300] : said);
                throw new ExternalServiceException(Service, response.StatusCode == HttpStatusCode.Forbidden
                    ? Messages.OutsideServices.RefusedByGateway(Service)
                    : Messages.OutsideServices.NotAnswering(Service));
            }

            var answer = await response.Content.ReadFromJsonAsync<Answer>(Json, ct);
            if (answer is null)
                throw new ExternalServiceException(Service, Messages.OutsideServices.AnsweredNothing(Service));
            return answer;
        }
        catch (HttpRequestException e)
        {
            throw new ExternalServiceException(Service, Messages.OutsideServices.NotAnswering(Service), inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceException(Service, Messages.OutsideServices.TookTooLong(Service), inner: e);
        }
        catch (JsonException e)
        {
            throw new ExternalServiceException(Service, Messages.OutsideServices.NotReadable(Service), inner: e);
        }
    }

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
