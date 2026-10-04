using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.NameScreening;

/// <summary>
/// The name screening API, from the "NameScreening" section of appsettings: its
/// base path under the gateway (<see cref="BackendOptions.BaseUrl"/>), the path of
/// the call, its key, and which lists a holder is screened against.
/// </summary>
public sealed class NameScreeningOptions : IApiAddress
{
    public const string Section = "NameScreening";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. The path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: whether a holder may invest online.</summary>
    public string ScreenPath { get; set; } = "";

    /// <summary>The key the API is called with, on its apikey header. Set in the environment, never in a committed file.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>"0" switches screening off: the API is not called and every holder goes on. Anything else is sent on as it is.</summary>
    public string ApiCall { get; set; } = "1";

    // Which lists the holder is screened against, as the API takes them.
    public string BlackListCheck { get; set; } = "";
    public string CustomerDataBaseCheck { get; set; } = "";
    public string RejectedListCheck { get; set; } = "";
    public string EmployeeDataBaseCheck { get; set; } = "";
}

/// <summary>
/// POST ScreenPath with the holder's name, date of birth and mobile number, and the
/// API answers with a status and a screening status. A holder is allowed only when
/// the status is SUCCESS and the screening status is ALLOWED.
/// </summary>
public sealed class NameScreeningClient(HttpClient http, IPartner partner, IOptions<NameScreeningOptions> options)
    : INameScreeningService
{
    private const string Service = "Name screening";

    /// <summary>Where the request says it comes from: this app, for a fixed deposit.</summary>
    private const string Source = "UNO_TP";
    private const string SourceType = "FD";

    private static readonly JsonSerializerOptions AnyCase = new() { PropertyNameCaseInsensitive = true };

    public async Task<NameScreeningResult> ScreenAsync(NameScreeningRequest holder, CancellationToken ct = default)
    {
        var settings = options.Value;
        // Switched off: nobody is screened, and the KYC row says the check was skipped.
        if (settings.ApiCall == "0") return new NameScreeningResult(true, NameScreeningResult.Skipped);

        var request = new ScreeningRequest
        {
            blackListCheck = settings.BlackListCheck,
            customerDataBaseCheck = settings.CustomerDataBaseCheck,
            rejectedListCheck = settings.RejectedListCheck,
            employeeDataBaseCheck = settings.EmployeeDataBaseCheck,
            name1 = holder.Name,
            dob = holder.Dob,
            country1 = "IN",
            appl_No = holder.AppNo,
            holderType = holder.HolderType,
            source = Source,
            sourceType = SourceType,
            sourceSubType = Source,
            sessionId = partner.SessionId ?? "",
            createdBy = partner.Id,
            createdIP = partner.IpAddress,
            Api_call = settings.ApiCall,
            mobileNo = holder.Mobile,
        };

        var answer = await SendAsync(settings, request, ct);
        var allowed = Same(answer.status, "SUCCESS") && Same(answer.nameScreeingStatus, "ALLOWED");
        return new NameScreeningResult(allowed, answer.data?.uniqueRequestId ?? answer.nameScreeningCode ?? "");
    }

    private async Task<ScreeningResponse> SendAsync(NameScreeningOptions settings, ScreeningRequest request, CancellationToken ct)
    {
        try
        {
            // The names go as they are written here, not camelCased: as the API takes them.
            using var message = new HttpRequestMessage(HttpMethod.Post, settings.ScreenPath)
            {
                Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"),
            };
            message.Headers.Add("apikey", settings.ApiKey);

            using var response = await http.SendAsync(message, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
                throw new ExternalServiceException(Service, $"{Service} answered with nothing. Try again in a while.");

            var answer = JsonSerializer.Deserialize<ScreeningResponse>(body, AnyCase);
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

    private static bool Same(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    // The request's fields are the API's own names, in its own spelling, so the call
    // goes exactly as every other app makes it. What this app has nothing for is sent empty.
    private sealed class ScreeningRequest
    {
        public string blackListCheck { get; set; } = "";
        public string customerDataBaseCheck { get; set; } = "";
        public string rejectedListCheck { get; set; } = "";
        public string employeeDataBaseCheck { get; set; } = "";
        public string name1 { get; set; } = "";
        public string name2 { get; set; } = "";
        public string name3 { get; set; } = "";
        public string name4 { get; set; } = "";
        public string name5 { get; set; } = "";
        public string dob { get; set; } = "";
        public string doi { get; set; } = "";
        public string address { get; set; } = "";
        public string passportNo { get; set; } = "";
        public string pancardNo { get; set; } = "";
        public string idNumber1 { get; set; } = "";
        public string idNumber2 { get; set; } = "";
        public string idNumber3 { get; set; } = "";
        public string idNumber4 { get; set; } = "";
        public string idNumber5 { get; set; } = "";
        public string country1 { get; set; } = "";
        public string country2 { get; set; } = "";
        public string country3 { get; set; } = "";
        public string country4 { get; set; } = "";
        public string country5 { get; set; } = "";
        public string remarks { get; set; } = "";
        public string appl_No { get; set; } = "";
        public string folio_No { get; set; } = "";
        public string sysRefNo { get; set; } = "";
        public string holderType { get; set; } = "";
        public string source { get; set; } = "";
        public string sourceType { get; set; } = "";
        public string sourceSubType { get; set; } = "";
        public string sessionId { get; set; } = "";
        public string createdBy { get; set; } = "";
        public string createdIP { get; set; } = "";
        public string addtional1 { get; set; } = "";
        public string addtional2 { get; set; } = "";
        public string Api_call { get; set; } = "";
        public string mobileNo { get; set; } = "";
        public string dep_No { get; set; } = "";
    }

    // The part of the answer the app reads, by the API's own names.
    private sealed class ScreeningResponse
    {
        public string? status { get; set; }
        public string? nameScreeingStatus { get; set; }
        public string? nameScreeningCode { get; set; }
        public ScreeningData? data { get; set; }
    }

    private sealed class ScreeningData
    {
        public string? uniqueRequestId { get; set; }
    }
}
