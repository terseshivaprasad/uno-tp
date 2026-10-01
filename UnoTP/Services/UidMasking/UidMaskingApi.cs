using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.Services.UidMasking;

/// <summary>
/// The UID masking API, from the "UidMasking" section of appsettings: the path of
/// the call under the gateway (<see cref="BackendOptions.BaseUrl"/>), how the
/// masked copy is to come back, and the codes every request carries.
/// </summary>
public sealed class UidMaskingOptions
{
    public const string Section = "UidMasking";

    /// <summary>The API's base path under the gateway. Every path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: the copy with its Aadhaar number masked.</summary>
    public string MaskPath { get; set; } = "";

    /// <summary>How many digits of the Aadhaar number are masked.</summary>
    public int MaskLength { get; set; } = 8;

    /// <summary>How good the masked JPEG comes back, 1 to 100.</summary>
    public int OutputJpegQuality { get; set; } = 80;

    /// <summary>Whether the API checks the copy is an Aadhaar before it masks it.</summary>
    public bool CheckDocumentType { get; set; } = true;
}

/// <summary>
/// Aadhaar masking: the copy goes as Base64 and comes back masked, with the digits
/// of the number it still shows. Nothing is retried - a repeated call may be
/// charged twice.
/// </summary>
public sealed class UidMaskingClient(HttpClient http, IPartner partner, IPartnerApi partners, IOptions<UidMaskingOptions> options)
    : IMaskingService
{
    private const string Service = "Aadhaar masking";

    // camelCase in both directions, and names read whatever their case: the same as
    // the API is called with elsewhere.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<MaskedAadhaar> MaskAsync(AadhaarToMask aadhaar, CancellationToken ct = default)
    {
        // Nothing about an Aadhaar - not even its image - is sent without the holder's consent.
        if (!aadhaar.Consent)
            throw new ExternalServiceException(Service, "An Aadhaar is only masked with the investor's consent, and none has been given.");

        var settings = options.Value;
        var me = await partners.MeAsync(ct);
        var request = new MaskRequest
        {
            FileType = FileTypeOf(aadhaar.Copy),
            FileData = Convert.ToBase64String(aadhaar.Copy.Bytes),
            MaskLength = settings.MaskLength,
            OutputJpegQuality = settings.OutputJpegQuality,
            CreatedIP = partner.IpAddress,
            CreatedBy = partner.Id,
            CreatedByUName = me.UserName,
            SessionID = partner.SessionId ?? "",
            Form_Code = aadhaar.FormCode,
            Source = me.SysCode,
            ApplNo = aadhaar.AppNo,
            FolioNo = aadhaar.Folio,
            HolderType = aadhaar.HolderType,
            PAN = aadhaar.Pan,
            DOB = aadhaar.Dob,
            CheckDocumentType = settings.CheckDocumentType,
        };

        var answer = await SendAsync(settings.MaskPath, request, ct);
        if (answer.Result?.FileData is not { Length: > 0 } masked)
            throw new ExternalServiceException(Service,
                answer.Error is { Length: > 0 } error
                    ? $"The Aadhaar could not be masked: {error}"
                    : "The Aadhaar could not be masked. Upload a clearer copy.");

        var copy = new UploadFile(aadhaar.Copy.FileName, aadhaar.Copy.ContentType, Convert.FromBase64String(masked));
        return new MaskedAadhaar(copy, answer.Result.AadhaarSuffix ?? "");
    }

    private async Task<MaskResponse> SendAsync(string path, MaskRequest request, CancellationToken ct)
    {
        try
        {
            using var body = JsonContent.Create(request, options: Json);
            using var response = await http.PostAsync(path, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var said = await response.Content.ReadAsStringAsync(ct);
                throw new ExternalServiceException(Service, $"{Service} could not mask the copy. Try again in a while.", TraceOf(said));
            }

            var answer = await response.Content.ReadFromJsonAsync<MaskResponse>(Json, ct);
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

    /// <summary>The kind of file the copy is, as the API names it: the extension, without its dot.</summary>
    private static string FileTypeOf(UploadFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (extension.Length < 2) return "";
        return extension[1..].ToLowerInvariant();
    }

    // What the API said, cut to a length worth logging beside the error.
    private static string TraceOf(string said) => said.Length <= 200 ? said : said[..200];

    // The request's fields are the API's own names, in its own spelling, so the
    // call goes exactly as every other app makes it.
    private sealed class MaskRequest
    {
        public string FileType { get; set; } = "";
        public string FileData { get; set; } = "";
        public int MaskLength { get; set; }
        public int OutputJpegQuality { get; set; }
        public string Trans_Ref_No { get; set; } = "";
        public string CreatedIP { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public string CreatedByUName { get; set; } = "";
        public string CreatedType { get; set; } = "";
        public string SessionID { get; set; } = "";
        public string Form_Code { get; set; } = "";
        public string Source { get; set; } = "";
        public string API_Response_File_Path { get; set; } = "";
        public string ApplNo { get; set; } = "";
        public string FolioNo { get; set; } = "";
        public string HolderType { get; set; } = "";
        public string PAN { get; set; } = "";
        public string DOB { get; set; } = "";
        public bool CheckDocumentType { get; set; } = true;
        public string AadharSuffix { get; set; } = "";
    }

    private sealed class MaskResponse
    {
        public string? IntStatusCode { get; set; }
        public string? Status { get; set; }
        public MaskedFile? Result { get; set; }
        public string? Error { get; set; }
    }

    private sealed class MaskedFile
    {
        public string? FileData { get; set; }
        public string? AadhaarSuffix { get; set; }
    }
}

public static class UidMaskingServiceCollectionExtensions
{
    /// <summary>Puts the UID masking API behind <see cref="IMaskingService"/>.</summary>
    public static IServiceCollection AddUidMasking(this IServiceCollection services)
    {
        services.AddApiClient<IMaskingService, UidMaskingClient>(
            sp => sp.GetRequiredService<IOptions<UidMaskingOptions>>().Value.BasePath);
        return services;
    }
}
