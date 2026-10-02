using System.Net;
using System.Text;
using System.Text.Json;
using UnoTP.Models;

namespace UnoTP.Tests;

/// <summary>
/// Stands in for the network under an API client: keeps the request the client
/// made, and answers with what the test set. Nothing leaves the machine.
/// </summary>
public sealed class StubNetwork : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    /// <summary>The JSON the API answers with.</summary>
    public string Answer { get; set; } = "{}";

    public int Calls { get; private set; }
    public HttpMethod? Method { get; private set; }
    public Uri? Address { get; private set; }
    public string Body { get; private set; } = "";
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The request's body, read as JSON.</summary>
    public JsonElement Sent => JsonDocument.Parse(Body).RootElement;

    /// <summary>A client over this stand-in, at the address an API has under the gateway.</summary>
    public HttpClient Client() => new(this) { BaseAddress = new Uri("http://gateway.test/some-api/api/v1/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Method = request.Method;
        Address = request.RequestUri;
        Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        foreach (var header in request.Headers) Headers[header.Key] = string.Join(",", header.Value);
        return new HttpResponseMessage(Status) { Content = new StringContent(Answer, Encoding.UTF8, "application/json") };
    }
}

/// <summary>The signed-in user, as the session gives them to a client.</summary>
public sealed class TestPartner : IPartner
{
    public string Id => "user-1";
    public string? SessionId => "9041";
    public string IpAddress => "10.0.0.7";
}

/// <summary>Who the user is, as the auth API said when their session started.</summary>
public sealed class TestPartnerApi : IPartnerApi
{
    public static readonly PartnerProfile Profile = new(
        Name: "Test Agency", Code: "E100", AgencyType: "AT1", BrokerCode: "BRK1",
        AgencySubType: "ST1", AgencyCode: "AG1", UserName: "usr_test", SysCode: "TESTAPP01");

    public Task<PartnerProfile> MeAsync(CancellationToken ct = default) => Task.FromResult(Profile);
}
