using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Idfy;

/// <summary>
/// Idfy.Api, the internal wrapper around IDfy EVE v3: one method per endpoint the
/// app uses. Images go as Base64 in JSON rather than as uploads, because the upload
/// routes take nothing but image/* and a proof may be a PDF.
///
/// A 200 only means IDfy returned a task, so every answer is checked for a
/// completed task before its result is read. Nothing is retried: every call that
/// reaches IDfy may be charged, and a retried one charged twice.
/// </summary>
public sealed class IdfyClient(HttpClient http, ILogger<IdfyClient> log)
{
    private const string Service = "IDfy";

    // Requests are camelCase, and a field with no value is left out: the API
    // refuses any field it does not document.
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Task responses are IDfy's own snake_case.
    private static readonly JsonSerializerOptions TaskJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>The Base64 cap the API holds an image to - an image of about 2.25 MB.</summary>
    private const int MaxBase64 = 3_000_000;

    // ----- Document validation ------------------------------------------------

    /// <param name="docType">ind_pan, ind_aadhaar, ind_voter_id, ind_driving_license or ind_passport.</param>
    /// <param name="docType">The type to check it against, or null to have IDfy say
    /// what it is (in <c>detected_doc_type</c>) without checking it against anything.</param>
    public Task<IdfyTask<ValidateResult>> ValidateAsync(UploadFile file, string? docType, CancellationToken ct = default) =>
        Post<ValidateResult>("api/documents/validate", new { document = Image(file), docType }, ct);

    // ----- OCR ---------------------------------------------------------------

    public Task<IdfyTask<Extracted<PanCard>>> ExtractPanAsync(UploadFile file, CancellationToken ct = default) =>
        Post<Extracted<PanCard>>("api/pan/extract", new { document = Image(file) }, ct);

    public Task<IdfyTask<AadhaarExtraction>> ExtractAadhaarAsync(UploadFile file, bool consent, CancellationToken ct = default)
    {
        Consented(consent);
        return Post<AadhaarExtraction>("api/aadhaar/extract", new { document = Image(file), consent }, ct);
    }

    public Task<IdfyTask<Extracted<DrivingLicenceCard>>> ExtractDrivingLicenceAsync(UploadFile file, CancellationToken ct = default) =>
        Post<Extracted<DrivingLicenceCard>>("api/driving-license/extract", new { document = Image(file) }, ct);

    public Task<IdfyTask<Extracted<PassportPage>>> ExtractPassportAsync(UploadFile file, CancellationToken ct = default) =>
        Post<Extracted<PassportPage>>("api/passport/extract", new { document = Image(file) }, ct);

    public Task<IdfyTask<Extracted<VoterIdCard>>> ExtractVoterIdAsync(UploadFile file, CancellationToken ct = default) =>
        Post<Extracted<VoterIdCard>>("api/voter-id/extract", new { document = Image(file) }, ct);

    // ----- Masking -----------------------------------------------------------

    public Task<IdfyTask<AadhaarMask>> MaskAadhaarAsync(UploadFile file, bool consent, CancellationToken ct = default)
    {
        Consented(consent);
        return Post<AadhaarMask>("api/aadhaar/mask", new { document = Image(file), consent }, ct);
    }

    // ----- Verification with the source ------------------------------------------
    // The sync forms: the partner is waiting on the page for the answer.

    public Task<IdfyTask<Sourced<SourceStatus>>> VerifyDrivingLicenceAsync(string idNumber, DateOnly dateOfBirth, CancellationToken ct = default) =>
        Post<Sourced<SourceStatus>>("api/driving-license/verify/sync", new { idNumber, dateOfBirth = Date(dateOfBirth) }, ct);

    public Task<IdfyTask<Sourced<SourceStatus>>> VerifyPassportAsync(string passportFileNumber, DateOnly dateOfBirth, CancellationToken ct = default) =>
        Post<Sourced<SourceStatus>>("api/passport/verify/sync", new { passportFileNumber, dateOfBirth = Date(dateOfBirth) }, ct);

    public Task<IdfyTask<Sourced<SourceStatus>>> VerifyVoterIdAsync(string idNumber, CancellationToken ct = default) =>
        Post<Sourced<SourceStatus>>("api/voter-id/verify/sync", new { idNumber }, ct);

    public Task<IdfyTask<Sourced<PanAadhaarLinkSource>>> VerifyPanAadhaarLinkAsync(string panNumber, string aadhaarNumber, CancellationToken ct = default) =>
        Post<Sourced<PanAadhaarLinkSource>>("api/pan-aadhaar-link/verify/sync", new { panNumber, aadhaarNumber }, ct);

    /// <summary>
    /// IDfy's face compare: whether the faces on two images are the same person.
    /// Both go as Base64, under the same cap as any other image.
    /// </summary>
    public Task<IdfyTask<FaceCompare>> CompareFacesAsync(UploadFile first, UploadFile second, CancellationToken ct = default) =>
        Post<FaceCompare>("api/face/compare", new { document1 = Image(first), document2 = Image(second) }, ct);

    // ----- Plumbing ----------------------------------------------------------

    private async Task<IdfyTask<T>> Post<T>(string path, object body, CancellationToken ct) where T : class
    {
        HttpResponseMessage response;
        try
        {
            // Sent whole, with its length: the image is in memory already, and a
            // streamed body is one more thing for a proxy in the way to refuse. Written
            // straight to UTF-8, so a 4 MB image is not held as a string twice its size too.
            using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, RequestJson));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            response = await http.PostAsync(path, content, ct);
        }
        catch (HttpRequestException e)
        {
            throw new ExternalServiceException(Service, "The document check is not answering. Try again in a while.", inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            // IDfy may still finish the task and charge for it, so it is not sent again.
            throw new ExternalServiceException(Service, "The document check took too long to answer. Try again in a while.", inner: e);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode) throw await Failure(path, response, ct);
            IdfyTask<T>? task;
            try
            {
                task = await response.Content.ReadFromJsonAsync<IdfyTask<T>>(TaskJson, ct);
            }
            catch (JsonException e)
            {
                // A 200 that is not IDfy's task - a proxy's page, say - says nothing about the copy.
                log.LogWarning(e, "IDfy {Path} answered 200 with something that is not a task.", path);
                throw new ExternalServiceException(Service, "The document check answered with something that could not be read. Try again in a while.", inner: e);
            }
            if (task is null || !string.Equals(task.Status, "completed", StringComparison.OrdinalIgnoreCase) || task.Result is null)
            {
                log.LogWarning("IDfy {Path} returned task {TaskId} as {Status}.", path, task?.TaskId, task?.Status);
                throw new ExternalServiceException(Service, "The document check could not complete on this copy. Upload a clearer copy.");
            }
            return task;
        }
    }

    // What went wrong, said so the partner knows whether the copy or the service
    // is at fault. Every error carries a trace id, which is logged for support.
    private async Task<ExternalServiceException> Failure(string path, HttpResponseMessage response, CancellationToken ct)
    {
        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<Problem>(ct);
        }
        catch (JsonException)
        {
        }

        var status = (int)response.StatusCode;
        log.LogWarning("IDfy {Path} failed with {Status} {Title}: {Detail} (trace {TraceId}).",
            path, status, problem?.Title, problem?.Detail, problem?.TraceId);

        var message = response.StatusCode switch
        {
            HttpStatusCode.RequestEntityTooLarge => "The copy is too large for the document check. Keep it under about 2 MB.",
            HttpStatusCode.UnsupportedMediaType => "The document check does not take this kind of file. Upload a JPEG.",
            HttpStatusCode.UnprocessableEntity => problem?.Detail ?? "The copy's resolution is outside what the document check takes.",
            HttpStatusCode.BadRequest => $"The document check could not use this copy{(problem?.Detail is { Length: > 0 } d ? $": {d}" : ".")}",
            HttpStatusCode.TooManyRequests => "The document check is busy. Try again in a minute.",
            _ => "The document check is not answering. Try again in a while.",
        };
        return new ExternalServiceException(Service, message, problem?.TraceId);
    }

    private static string Image(UploadFile file)
    {
        var base64 = Convert.ToBase64String(file.Bytes);
        if (base64.Length > MaxBase64)
            throw new ExternalServiceException(Service, "The copy is too large for the document check. Keep it under about 2 MB.");
        return base64;
    }

    // Every Aadhaar endpoint wants the holder's consent, and nothing about an
    // Aadhaar - not even its image - is sent without it.
    private static void Consented(bool consent)
    {
        if (!consent)
            throw new ExternalServiceException(Service, "An Aadhaar is only read or checked with the investor's consent, and none has been given.");
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record Problem(string? Title, int? Status, string? Detail, string? TraceId);
}

// ----- What IDfy answers ------------------------------------------------------
// Only the fields the app reads; the rest of each task is ignored.

/// <summary>IDfy's task object. Check <see cref="Status"/> before reading <see cref="Result"/>.</summary>
public sealed record IdfyTask<T>(string? Status, string? TaskId, T? Result);

public sealed record ValidateResult(string? DetectedDocType, bool? IsReadable);

public sealed record Extracted<T>(T? ExtractionOutput);

public sealed record Sourced<T>(T? SourceOutput);

public sealed record PanCard(string? IdNumber, string? NameOnCard, string? DateOfBirth = null);

/// <param name="QrOutput">Decoded from the QR code, which UIDAI signs, when there is one.</param>
public sealed record AadhaarExtraction(AadhaarCard? ExtractionOutput, AadhaarCard? QrOutput);

public sealed record AadhaarCard(string? IdNumber, string? NameOnCard, string? Address, string? Gender = null);

/// <param name="IsAMatch">Whether the two faces are the same person.</param>
/// <param name="MatchScore">How alike they are, 0 to 100.</param>
/// <param name="ReviewNeeded">Set when IDfy could not be sure either way.</param>
public sealed record FaceCompare(bool? IsAMatch, double? MatchScore, bool? ReviewNeeded);

/// <param name="IdNumberFound">False when there was no Aadhaar number on the copy to mask.</param>
public sealed record AadhaarMask(bool? IdNumberFound);

public sealed record DrivingLicenceCard(string? IdNumber, string? NameOnCard, string? Address);

public sealed record PassportPage(string? FileNumber, string? NameOnCard, string? Address);

/// <param name="IdNumber">The EPIC number, which IDfy may return partly masked, as T*****0275.</param>
public sealed record VoterIdCard(string? IdNumber, string? NameOnCard, string? Address);

/// <summary>What a verify-with-source call found.</summary>
/// <param name="Status">IDfy's id_found or id_not_found.</param>
public sealed record SourceStatus(string? Status);

public sealed record PanAadhaarLinkSource(bool? IsLinked);
