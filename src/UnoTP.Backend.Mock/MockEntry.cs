using System.Collections.Concurrent;
using System.Text;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock;

/// <summary>
/// The portal's encryption, as the mock has it: base64url of the plain text. The
/// real one is the portal's to keep; nothing here depends on how it is done.
/// </summary>
public sealed class MockDecryption : IDecryptionService
{
    public Task<string?> DecryptAsync(string cipherText, CancellationToken ct = default)
    {
        try
        {
            var b64 = cipherText.Trim().Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            var plain = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            return Task.FromResult<string?>(plain.Length > 0 && plain.All(c => !char.IsControl(c)) ? plain : null);
        }
        catch (FormatException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>What the portal would put in the address for a plain value.</summary>
    public static string Encrypt(string plain) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plain)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Sessions and menus for three users: the partner with every menu, one without
/// pay-in slips or the console, and one with none at all. Only this app's system
/// code starts a session.
/// </summary>
public sealed class MockSessions(IPartner partner) : ISessionApi
{
    public const string SysCode = "UNOTP";

    private static readonly MenuItem[] All =
    [
        new("new-fd", "Create New FD"), new("pis", "PIS Generation — Axis"), new("view-app", "View existing application"),
        new("short-url", "Short URL"), new("app-status", "Application status"), new("renew", "Renew FD"), new("admin", "Console Admin"),
    ];

    private static readonly Dictionary<string, MenuItem[]> Menus = new()
    {
        ["100002225"] = All,
        ["100002226"] = All.Where(m => m.Key is not ("pis" or "admin")).ToArray(),
        ["100002227"] = [],
    };

    // The sessions started, so a menu is only given for one.
    private static readonly ConcurrentDictionary<string, string> Started = new();

    public Task<UserSession?> StartAsync(string userId, string sysCode, CancellationToken ct = default)
    {
        if (sysCode != SysCode || !Menus.ContainsKey(userId)) return Task.FromResult<UserSession?>(null);
        var id = Guid.NewGuid().ToString("n");
        Started[id] = userId;
        return Task.FromResult<UserSession?>(new UserSession(id, userId, DateTime.Now.AddHours(8)));
    }

    public Task<IReadOnlyList<MenuItem>> MenuAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MenuItem>>(
            partner.SessionId is { } s && Started.TryGetValue(s, out var user) && user == partner.Id ? Menus[user] : []);
}
