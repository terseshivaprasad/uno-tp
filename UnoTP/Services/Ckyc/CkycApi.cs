using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.Ckyc;

/// <summary>
/// The CKYC (CERSAI) search API, from the "Ckyc" section of appsettings: its base
/// path under the gateway (<see cref="BackendOptions.BaseUrl"/>) and the path of
/// the call.
/// </summary>
public sealed class CkycOptions : IApiAddress
{
    public const string Section = "Ckyc";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. The path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: whether CERSAI holds a record for a PAN and date of birth.</summary>
    public string SearchPath { get; set; } = "";

    /// <summary>Whether the record's images come back with the search: Y or N.</summary>
    public string IncludeImages { get; set; } = "N";
}

/// <summary>
/// POST SearchPath with the PAN and date of birth, and CERSAI answers with what it
/// holds against them. The search is by PAN (input id type C).
/// </summary>
public sealed class CkycApiClient(HttpClient http, IPartner partner, IOptions<CkycOptions> options) : ICkycService
{
    private const string Service = "CKYC";

    /// <summary>The kind of id the search is made with: C, a PAN.</summary>
    private const string PanIdType = "C";

    private static readonly JsonSerializerOptions AnyCase = new() { PropertyNameCaseInsensitive = true };

    public async Task<CkycSearchResult> SearchAsync(CkycSearch search, CancellationToken ct = default)
    {
        var settings = options.Value;
        var request = new SearchRequest
        {
            IncludeImages = settings.IncludeImages,
            SearchInCkycSearchParamDetail =
            [
                new SearchParam
                {
                    TransactionId = NewTransactionId(),
                    RecordIdentifier = search.AppNo + search.HolderType + (partner.SessionId ?? ""),
                    ApplicationFormNo = search.AppNo,
                    InputIdType = PanIdType,
                    InputIdNo = search.Pan,
                    DOB = search.Dob,
                },
            ],
        };

        var answer = await SendAsync(settings.SearchPath, request, ct);
        var response = answer.ckycResponse;
        var record = response?.searchInCkycResponseDetail?.FirstOrDefault();
        if (record is null)
            return new CkycSearchResult(false, Why: response?.requestRejectionDescription ?? "");

        if (!IsYes(record.ckycAvailable))
            return new CkycSearchResult(false, Why: record.transactionRejectionDescription ?? response?.requestRejectionDescription ?? "");

        return new CkycSearchResult(
            Available: true,
            Name: record.ckycName ?? "",
            Reference: record.ckycReferenceID ?? "");
    }

    private async Task<SearchResponse> SendAsync(string path, SearchRequest request, CancellationToken ct)
    {
        try
        {
            // The names go as they are written here, not camelCased: as the API takes them.
            using var body = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path, body, ct);
            response.EnsureSuccessStatusCode();

            var said = await response.Content.ReadAsStringAsync(ct);
            var answer = string.IsNullOrWhiteSpace(said) ? null : JsonSerializer.Deserialize<SearchResponse>(said, AnyCase);
            if (answer is null)
                throw new ExternalServiceException(Service, $"{Service} answered with nothing. Try again in a while.");
            return answer;
        }
        catch (HttpRequestException e)
        {
            throw new ExternalServiceException(Service, $"{Service} is not answering. Try again in a while.", inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceException(Service, $"{Service} took too long to answer. Try again in a while.", inner: e);
        }
        catch (JsonException e)
        {
            throw new ExternalServiceException(Service, $"{Service} answered with something that could not be read. Try again in a while.", inner: e);
        }
    }

    /// <summary>A ten-digit id for one search: the last ten digits of the clock's ticks and a random four-digit number.</summary>
    private static string NewTransactionId()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var random = Random.Shared.Next(1000, 9999);
        var combined = $"{ticks}{random}";
        return combined[^10..];
    }

    private static bool IsYes(string? value)
    {
        var said = (value ?? "").Trim();
        return said.Equals("Y", StringComparison.OrdinalIgnoreCase) || said.Equals("Yes", StringComparison.OrdinalIgnoreCase);
    }

    // The request's fields are the API's own names, in its own spelling, so the call
    // goes exactly as every other app makes it. What this app has nothing for is sent empty.
    private sealed class SearchRequest
    {
        public string IncludeImages { get; set; } = "N";
        public List<SearchParam> SearchInCkycSearchParamDetail { get; set; } = [];
    }

    private sealed class SearchParam
    {
        public string TransactionId { get; set; } = "";
        public string RecordIdentifier { get; set; } = "";
        public string ApplicationFormNo { get; set; } = "";
        public string BranchCode { get; set; } = "";
        public string InputIdType { get; set; } = "";
        public string InputIdNo { get; set; } = "";
        public string APITags { get; set; } = "";
        public string SourceSystem { get; set; } = "";
        public string SourceSystemSegment { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string DOB { get; set; } = "";
        public string? Gender { get; set; }
    }

    // The part of the answer the app reads, by the API's own names. The ids, statuses
    // and codes it does not use are left out: the answer is then read whether they
    // come as text or as a number.
    private sealed class SearchResponse
    {
        public CkycResponse? ckycResponse { get; set; }
    }

    private sealed class CkycResponse
    {
        public string? requestRejectionDescription { get; set; }
        public List<SearchRecord>? searchInCkycResponseDetail { get; set; }
    }

    private sealed class SearchRecord
    {
        public string? ckycAvailable { get; set; }
        public string? ckycName { get; set; }
        public string? transactionRejectionDescription { get; set; }
        public string? ckycReferenceID { get; set; }
    }
}

public static class CkycServiceCollectionExtensions
{
    /// <summary>Puts the CKYC search API behind <see cref="ICkycService"/>.</summary>
    public static IServiceCollection AddCkyc(this IServiceCollection services)
    {
        services.AddApiClient<ICkycService, CkycApiClient>(
            sp => sp.GetRequiredService<IOptions<CkycOptions>>().Value);
        return services;
    }
}
