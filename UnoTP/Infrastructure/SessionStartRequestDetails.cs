using System.Net.NetworkInformation;
using UnoTP.Models;

namespace UnoTP.Infrastructure;

/// <summary>
/// What the auth API is told when a session starts, taken from the request: the
/// server, the host the app was opened at, the caller's address and their browser.
/// </summary>
public static class SessionStarts
{
    // The browsers a User-Agent is read for, in the order they are looked for. Edge
    // and the like also say Chrome, and are taken as Chrome.
    private static readonly (string Token, string Name)[] Browsers =
    [
        ("Chrome/", "Chrome"),
        ("Firefox/", "Firefox"),
        ("Version/", "Safari"),
    ];

    private static readonly Lazy<string> ServerMac = new(FindServerMac);

    public static SessionStart For(HttpContext http, string userId, string sysCode)
    {
        var userAgent = http.Request.Headers.UserAgent.ToString();
        var (browser, version) = Browser(userAgent);
        var versionParts = version.Split('.');
        var major = versionParts[0];
        var minor = versionParts.Length > 1 ? versionParts[1] : "0";

        return new SessionStart(
            UserId: userId,
            SysCode: sysCode,
            ServerIp: http.Connection.LocalIpAddress?.ToString() ?? "",
            DomainName: http.Request.Host.Host,
            IpAddress: http.Connection.RemoteIpAddress?.ToString() ?? "",
            MacAddress: ServerMac.Value,
            BrowserType: browser,
            BrowserVersion: version,
            BrowserMajor: major,
            BrowserMinor: minor,
            UserAgent: userAgent);
    }

    // "Chrome" and "153.0.0.0" from "... Chrome/153.0.0.0 Safari/537.36".
    private static (string Name, string Version) Browser(string userAgent)
    {
        foreach (var (token, name) in Browsers)
        {
            var at = userAgent.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;

            var start = at + token.Length;
            var end = start;
            while (end < userAgent.Length && (char.IsAsciiDigit(userAgent[end]) || userAgent[end] == '.'))
            {
                end++;
            }
            if (end > start) return (name, userAgent[start..end]);
        }
        return ("Unknown", "0.0");
    }

    // The first network card that is up and is not the loopback, as twelve hex digits.
    private static string FindServerMac()
    {
        foreach (var card in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (card.OperationalStatus != OperationalStatus.Up) continue;
            if (card.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var mac = card.GetPhysicalAddress().ToString();
            if (mac.Length > 0) return mac;
        }
        return "";
    }
}
