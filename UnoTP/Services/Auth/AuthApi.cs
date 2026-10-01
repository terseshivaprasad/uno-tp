using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.Services.Auth;

/// <summary>
/// The E-Sarathi auth API, from the "AuthApi" section of appsettings: the path of
/// each call under the gateway (<see cref="BackendOptions.BaseUrl"/>).
/// </summary>
public sealed class AuthApiOptions
{
    public const string Section = "AuthApi";

    /// <summary>The API's base path under the gateway. Every path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>POST: the portal's encrypted user id and system code, decrypted.</summary>
    public string DecryptPath { get; set; } = "cipher/decrypt";

    /// <summary>POST: starts the user's session, and says who they are.</summary>
    public string SessionPath { get; set; } = "auth/sessions";

    /// <summary>GET: the menus the user may open. {userId} and {sysCode} are filled in.</summary>
    public string MenuPath { get; set; } = "app-menus/{userId}/{sysCode}";

    /// <summary>Hours a session lasts after entry; the user comes in from the portal again after that.</summary>
    public int SessionHours { get; set; } = 8;

}

/// <summary>
/// The E-Sarathi auth API, which the way in from the portal uses. Its address and
/// the path of each call come from appsettings (<see cref="AuthApiOptions"/>):
///   POST DecryptPath   the portal's encrypted user id and system code
///   POST SessionPath   starts the user's session, and says who they are
///   GET  MenuPath      the menus the user may open
/// Every answer is { success, message, data }.
/// </summary>
public sealed class AuthApiClient(HttpClient http, IOptions<AuthApiOptions> options) : IDecryptionService, ISessionApi
{
    private const string Service = "The E-Sarathi sign-in service";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string?> DecryptAsync(string cipherText, CancellationToken ct = default)
    {
        var body = JsonBody(new { Text = cipherText });
        var answer = await SendAsync<string>(HttpMethod.Post, options.Value.DecryptPath, body, ct);
        if (answer is null || !answer.Success) return null;
        return answer.Data;
    }

    public async Task<UserSession?> StartAsync(SessionStart start, CancellationToken ct = default)
    {
        var body = JsonBody(new
        {
            userId = start.UserId,
            sysCode = start.SysCode,
            serverIP = start.ServerIp,
            domainName = start.DomainName,
            ipAddress = start.IpAddress,
            macAddress = start.MacAddress,
            browserType = start.BrowserType,
            browserVersion = start.BrowserVersion,
            browserMajor = start.BrowserMajor,
            browserMinor = start.BrowserMinor,
            userAgent = start.UserAgent,
        });
        var answer = await SendAsync<AgencyUserModel>(HttpMethod.Post, options.Value.SessionPath, body, ct);
        if (answer is null || !answer.Success || answer.Data is null) return null;

        var user = answer.Data;
        var partner = new PartnerProfile(
            Name: user.Entity_Name ?? "",
            Code: user.Entity_Id ?? "",
            AgencyType: user.Agency_Type ?? "",
            BrokerCode: user.Busi_Broker_Cd ?? "",
            AgencySubType: user.Agency_Sub_Type ?? "",
            AgencyCode: user.Agency_Cd ?? "",
            UserName: user.Agency_Usr_Name ?? "",
            SysCode: start.SysCode);
        // The session is the one the API started: its id goes with every call made for the user.
        var sessionId = user.Pk_Session_ID.ToString();
        var expiresAt = DateTime.Now.AddHours(options.Value.SessionHours);
        return new UserSession(sessionId, start.UserId, expiresAt, partner, user);
    }

    public async Task<IReadOnlyList<MenuItem>> MenuAsync(string userId, string sysCode, CancellationToken ct = default)
    {
        var path = options.Value.MenuPath
            .Replace("{userId}", Uri.EscapeDataString(userId))
            .Replace("{sysCode}", Uri.EscapeDataString(sysCode));
        var answer = await SendAsync<List<MenuRow>>(HttpMethod.Get, path, null, ct);
        if (answer is null || !answer.Success || answer.Data is null) return [];

        var menu = new List<MenuItem>();
        foreach (var row in answer.Data)
        {
            if (string.IsNullOrWhiteSpace(row.PageName)) continue;
            menu.Add(new MenuItem(row.PageName.Trim(), row.SubModName ?? ""));
        }
        return menu;
    }

    // The API's answer, or null when it turned the request down (a 4xx). A service
    // that is down, slow or answering with something unreadable is an
    // ExternalServiceException, as for every outside service.
    private async Task<Answer<T>?> SendAsync<T>(HttpMethod method, string path, HttpContent? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path) { Content = body };
            using var response = await http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (status >= 400 && status < 500) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Answer<T>>(Json, ct);
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

    // A request body: the value as JSON, with its names as they are written here.
    private static StringContent JsonBody(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private sealed record Answer<T>(bool Success, string? Message, T? Data);

    // One menu of app-menus' answer.
    private sealed record MenuRow(string? SubModName, string? PageName);
}

public static class AuthApiServiceCollectionExtensions
{
    /// <summary>Puts the E-Sarathi auth API behind the way in: decryption, the session and the menus.</summary>
    public static IServiceCollection AddAuthApi(this IServiceCollection services)
    {
        services.AddApiClient<IDecryptionService, AuthApiClient>(BasePath);
        services.AddApiClient<ISessionApi, AuthApiClient>(BasePath);
        return services;
    }

    private static string BasePath(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<AuthApiOptions>>().Value.BasePath;
}
