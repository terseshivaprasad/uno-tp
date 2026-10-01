using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.Services.Pan;

/// <summary>
/// The PAN verification API, from the "PanApi" section of appsettings: the path of
/// the call under the gateway (<see cref="BackendOptions.BaseUrl"/>). Everything
/// else the request carries comes from the signed-in partner and the application.
/// </summary>
public sealed class PanApiOptions
{
    public const string Section = "PanApi";

    /// <summary>The API's base path under the gateway. Every path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: the PAN checked with NSDL.</summary>
    public string VerifyPath { get; set; } = "";

}

/// <summary>
/// The PAN verification API: POST VerifyPath with the holder's PAN, date of birth
/// and name, and it answers with a match status for each. "1" is a match and "0"
/// is not; anything else is read as not a match.
/// </summary>
public sealed class PanApiClient(HttpClient http, IPartner partner, IPartnerApi partners, IOptions<PanApiOptions> options)
    : IPanVerificationService
{
    private const string Service = "The PAN check";

    private const string Matched = "1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PanVerification> VerifyAsync(PanToVerify pan, CancellationToken ct = default)
    {
        var me = await partners.MeAsync(ct);
        var request = new PanRequest
        {
            App_Code = me.SysCode,
            Appl_No = pan.AppNo,
            Source_Type = me.AgencyType,
            Source_Sub_Type = me.AgencySubType,
            Holder_Type = pan.HolderType,
            PAN_No = pan.Pan,
            PAN_Holder_Name = pan.Name,
            PAN_Holder_DOB = pan.Dob,
            User_Name = me.UserName,
            CreatedBy = partner.Id,
            CreatedByUName = me.UserName,
            CreatedIP = partner.IpAddress,
            SessionId = SessionNumber(partner.SessionId),
        };

        var answer = await SendAsync(options.Value.VerifyPath, request, ct);
        // A PAN the API does not hold, or holds against another date of birth, is not
        // this holder's: the name is not asked about at all.
        var pairOk = answer.PAN_No_Match_Status == Matched && answer.PAN_DOB_Match_Status == Matched;
        var nameOk = pairOk && answer.PAN_Name_Match_Status == Matched;
        return new PanVerification(pairOk, nameOk);
    }

    // The session's id as the number the API takes it as; 0 when there is none.
    private static long SessionNumber(string? sessionId)
    {
        if (long.TryParse(sessionId, out var number)) return number;
        return 0;
    }

    private async Task<PanResponse> SendAsync(string path, PanRequest request, CancellationToken ct)
    {
        try
        {
            using var body = JsonContent.Create(request, options: Json);
            using var response = await http.PostAsync(path, body, ct);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                throw new ExternalServiceException(Service, $"{Service} is not available just now. Try again in a while.");
            response.EnsureSuccessStatusCode();

            var answer = await response.Content.ReadFromJsonAsync<PanResponse>(Json, ct);
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

    // The request's fields are the API's own names, in its own spelling, so the call
    // goes exactly as every other app makes it. What is left empty here is not known
    // to this app yet.
    private sealed class PanRequest
    {
        public string App_Code { get; set; } = "";
        public string Appl_No { get; set; } = "";
        public string MCP_Code { get; set; } = "";
        public string SCP_Code { get; set; } = "";
        public string MF_Sys_Ref_No { get; set; } = "";
        public string CP_Trans_Ref_No { get; set; } = "";
        public string Source_Type { get; set; } = "";
        public string Source_Sub_Type { get; set; } = "";
        public string Holder_Type { get; set; } = "";
        public string User_Name { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string PAN_Holder_Name { get; set; } = "";
        public string PAN_Holder_DOB { get; set; } = "";
        public string PAN_No { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public string CreatedByUName { get; set; } = "";
        public string CreatedIP { get; set; } = "";
        public long SessionId { get; set; }
        public string CreatedType { get; set; } = "";
        public string Ref_Type { get; set; } = "";
    }

    // The answer, by the API's own names.
    private sealed class PanResponse
    {
        public string? PAN_No_Match_Status { get; set; }
        public string? PAN_Name_Match_Status { get; set; }
        public string? PAN_DOB_Match_Status { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Status { get; set; }
    }
}

public static class PanApiServiceCollectionExtensions
{
    /// <summary>Puts the PAN verification API behind <see cref="IPanVerificationService"/>.</summary>
    public static IServiceCollection AddPanApi(this IServiceCollection services)
    {
        services.AddApiClient<IPanVerificationService, PanApiClient>(
            sp => sp.GetRequiredService<IOptions<PanApiOptions>>().Value.BasePath);
        return services;
    }
}
