using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UnoTP.Backend;

/// <summary>
/// What every HTTP client here shares: the partner on each request, JSON the web
/// way, a 404 read as "not found" where the contract allows for it, and any other
/// failure thrown.
/// </summary>
public abstract class ApiClient(HttpClient http, IPartner partner)
{
    /// <summary>
    /// The partner a request is made for. It stands in for the signed-in partner's
    /// token until the app has a sign-in to forward.
    /// </summary>
    public const string PartnerHeader = "X-Partner-Id";

    /// <summary>The session the backend started for the user when they came in from the portal.</summary>
    public const string SessionHeader = "X-Session-Id";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(PartnerHeader, partner.Id);
        if (partner.SessionId is { Length: > 0 } session) request.Headers.Add(SessionHeader, session);
        return request;
    }

    protected Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        http.SendAsync(request, ct);

    protected async Task<T?> Get<T>(string path, CancellationToken ct) where T : class
    {
        using var request = Request(HttpMethod.Get, path);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await Read<T>(response, ct);
    }

    protected async Task<List<T>> List<T>(string path, CancellationToken ct) =>
        await Get<List<T>>(path, ct) ?? [];

    protected async Task<T> Send<T>(HttpMethod method, string path, HttpContent content, CancellationToken ct)
    {
        using var request = Request(method, path, content);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await Read<T>(response, ct);
    }

    protected async Task Send(HttpMethod method, string path, HttpContent content, CancellationToken ct)
    {
        using var request = Request(method, path, content);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    protected static JsonContent Body(object value) => JsonContent.Create(value, options: Json);

    protected static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new HttpRequestException($"{response.RequestMessage?.RequestUri} answered with no body.");

    // The file goes as "file", beside whatever fields describe it.
    protected static MultipartFormDataContent Form(UploadFile file, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        var bytes = new ByteArrayContent(file.Bytes);
        // The type is the browser's claim, so one that does not parse goes as a bare stream.
        bytes.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type
            : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(bytes, "file", file.FileName);
        return form;
    }

    protected static string Seg(string value) => Uri.EscapeDataString(value);
}
