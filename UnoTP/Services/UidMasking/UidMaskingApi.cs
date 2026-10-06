using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UnoTP.Models;

namespace UnoTP.Services.UidMasking;

/// <summary>
/// The UID masking API, from the "UidMasking" section of appsettings: the path of
/// the call under the gateway (<see cref="BackendOptions.BaseUrl"/>), and the codes
/// every request carries.
/// </summary>
public sealed class UidMaskingOptions : IApiAddress
{
    public const string Section = "UidMasking";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. Every path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: the copy with its Aadhaar number masked.</summary>
    public string MaskPath { get; set; } = "";

    /// <summary>How many digits of the Aadhaar number are masked.</summary>
    public int MaskLength { get; set; } = 8;

    /// <summary>The name the API knows this app by.</summary>
    public string Source { get; set; } = "UNO_TP";
}

/// <summary>
/// Aadhaar masking: the copy goes as Base64 and comes back masked. The request and
/// the answer are the API's own, field for field. Nothing is retried - a repeated
/// call may be charged twice.
/// </summary>
public sealed class UidMaskingClient(HttpClient http, IPartner partner, IOptions<UidMaskingOptions> options)
    : IMaskingService
{
    private const string Service = "Aadhaar masking";

    /// <summary>What the API's Status says when the copy came back masked.</summary>
    private const string Succeeded = "SUCCESS";

    // The request goes under the API's own names, exactly as they are spelt below.
    private static readonly JsonSerializerOptions AsNamed = new();

    // The answer's names are read whatever their case.
    private static readonly JsonSerializerOptions AnyCase = new() { PropertyNameCaseInsensitive = true };

    public async Task<MaskedAadhaar> MaskAsync(AadhaarToMask aadhaar, CancellationToken ct = default)
    {
        // Nothing about an Aadhaar - not even its image - is sent without the holder's consent.
        if (!aadhaar.Consent)
            throw new ExternalServiceException(Service, Messages.OutsideServices.NoConsentToMask);

        var settings = options.Value;
        var request = new MaskRequest
        {
            MaskLength = settings.MaskLength.ToString(),
            // Which application and holder the copy is for: 123456_01.
            Trans_Ref_No = $"{aadhaar.AppNo}_{aadhaar.HolderType}",
            Source = settings.Source,
            CreatedIP = partner.IpAddress,
            FileType = FileTypeOf(aadhaar.Copy),
            FileData = Convert.ToBase64String(aadhaar.Copy.Bytes),
        };

        var answer = await SendAsync(settings.MaskPath, request, ct);
        var succeeded = (answer.Status ?? "").Trim().ToUpperInvariant() == Succeeded;
        if (!succeeded || answer.Result?.FileData is not { Length: > 0 } masked)
        {
            // Why not, in the API's own words: its error, else what its status says.
            var why = (answer.Error ?? "").Trim();
            if (why.Length == 0) why = (answer.IntStatusDesc ?? "").Trim();
            throw new ExternalServiceException(Service, why.Length > 0
                ? Messages.OutsideServices.NotMaskedBecause(why)
                : Messages.OutsideServices.NotMasked);
        }

        var copy = new UploadFile(aadhaar.Copy.FileName, aadhaar.Copy.ContentType, Convert.FromBase64String(masked));
        return new MaskedAadhaar(copy, answer.Result.AadhaarSuffix ?? "");
    }

    private async Task<MaskResponse> SendAsync(string path, MaskRequest request, CancellationToken ct)
    {
        try
        {
            // Sent whole, with its length: a streamed body is one more thing for a
            // proxy in the way to refuse.
            using var body = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(request, AsNamed));
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using var response = await http.PostAsync(path, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var said = await response.Content.ReadAsStringAsync(ct);
                throw new ExternalServiceException(Service, Messages.OutsideServices.MaskingFailed(Service, (int)response.StatusCode), TraceOf(said));
            }

            var answer = await response.Content.ReadFromJsonAsync<MaskResponse>(AnyCase, ct);
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

    /// <summary>The kind of file the copy is, as the API names it: IMAGE, or PDF for a PDF.</summary>
    private static string FileTypeOf(UploadFile file)
    {
        if (Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return "PDF";
        return "IMAGE";
    }

    // What the API said, cut to a length worth logging beside the error.
    private static string TraceOf(string said) => said.Length <= 200 ? said : said[..200];

    // The request, as the API takes it: these fields, under these names, the mask
    // length as text.
    private sealed class MaskRequest
    {
        public string MaskLength { get; set; } = "";
        public string Trans_Ref_No { get; set; } = "";
        public string Source { get; set; } = "";
        public string CreatedIP { get; set; } = "";
        public string FileType { get; set; } = "";
        public string FileData { get; set; } = "";
    }

    // The part of the answer the app reads:
    // { "IntStatusCode": "MF-SYS-200", "IntStatusDesc": "Success with masking", "Status": "Success",
    //   "Result": { "FileData": "...", "AadhaarSuffix": null, ... }, "Error": "" }
    private sealed class MaskResponse
    {
        public string? Status { get; set; }
        public string? IntStatusDesc { get; set; }
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
            sp => sp.GetRequiredService<IOptions<UidMaskingOptions>>().Value);
        return services;
    }
}
