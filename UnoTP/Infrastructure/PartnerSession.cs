using System.Text.Json;
using UnoTP.Models;

namespace UnoTP.Infrastructure;

/// <summary>
/// What the server keeps for one partner's browser: the sign-in - who the partner
/// is, the backend's session for them and when it ends - and
/// nothing else. The application and everything on it are the backend's, kept
/// there for audit; the application a page is on is in its address (see
/// ApplicationUrls), and addresses carry no PAN, date of birth, name or folio.
///
/// Held in ASP.NET Core session state behind an HttpOnly, SameSite=Lax cookie.
/// </summary>
public static class PartnerSession
{
    private const string OwnerKey = "partner";
    private const string SessionIdKey = "entry.session";
    private const string ExpiresKey = "entry.expires";
    private const string ProfileKey = "entry.profile";
    private const string UserKey = "entry.user";
    private const string PortalUserKey = "portal.userId";
    private const string PortalSysCodeKey = "portal.sysCode";

    /// <summary>
    /// Whose applications this browser may open: the user the portal sent in (see
    /// EntryController), so an application only ever opens for them. Before anyone
    /// has come in, a random id nobody's applications are held under.
    /// </summary>
    public static string Owner(this ISession session)
    {
        var owner = session.GetString(OwnerKey);
        if (owner is null)
        {
            owner = Guid.NewGuid().ToString("n");
            session.SetString(OwnerKey, owner);
        }
        return owner;
    }

    /// <summary>
    /// Keeps the user the portal sent in, the session started for them and who they
    /// are. Whatever the browser held before goes: a session is never carried over
    /// from one user to another.
    /// </summary>
    public static void SignIn(this ISession session, UserSession user)
    {
        session.Clear();
        session.SetString(OwnerKey, user.UserId);
        session.SetString(SessionIdKey, user.SessionId);
        session.SetString(ExpiresKey, user.ExpiresAt.ToString("o"));
        session.SetString(ProfileKey, JsonSerializer.Serialize(user.Partner));
        session.SetString(UserKey, JsonSerializer.Serialize(user.User));
    }

    /// <summary>The user and their session, whole, as the auth API returned them; null before anyone has come in.</summary>
    public static AgencyUserModel? User(this ISession session)
    {
        var json = session.GetString(UserKey);
        if (json is null) return null;
        return JsonSerializer.Deserialize<AgencyUserModel>(json);
    }

    /// <summary>Who the signed-in user is, as the auth API said when their session started; null before anyone has come in.</summary>
    public static PartnerProfile? Profile(this ISession session)
    {
        var json = session.GetString(ProfileKey);
        if (json is null) return null;
        return JsonSerializer.Deserialize<PartnerProfile>(json);
    }

    /// <summary>Whether a user came in from the portal and their backend session has not ended.</summary>
    public static bool SignedIn(this ISession session) =>
        session.GetString(SessionIdKey) is not null
        && DateTime.TryParse(session.GetString(ExpiresKey), null, System.Globalization.DateTimeStyles.RoundtripKind, out var until)
        && until > DateTime.Now;

    /// <summary>The user the portal sent in, while they are signed in; null otherwise.</summary>
    public static string? SignedInUser(this ISession session) => session.SignedIn() ? session.GetString(OwnerKey) : null;

    /// <summary>
    /// Keeps the encrypted UserId and SysCode the portal sent in, for the way back to it
    /// (EntryController.Home). Called after SignIn, which clears the session.
    /// </summary>
    public static void KeepPortalValues(this ISession session, string userId, string sysCode)
    {
        session.SetString(PortalUserKey, userId);
        session.SetString(PortalSysCodeKey, sysCode);
    }

    /// <summary>The encrypted values the portal sent in, or null when the session holds none.</summary>
    public static (string UserId, string SysCode)? PortalValues(this ISession session)
    {
        var userId = session.GetString(PortalUserKey);
        var sysCode = session.GetString(PortalSysCodeKey);
        if (userId is null || sysCode is null) return null;
        return (userId, sysCode);
    }

    /// <summary>The backend session the user came in with; null before anyone has.</summary>
    public static string? BackendSession(this ISession session) => session.GetString(SessionIdKey);
}

/// <summary>The partner the backend is asked on behalf of: the session's owner.</summary>
public sealed class SessionPartner(IHttpContextAccessor http) : IPartner
{
    private ISession Session => (http.HttpContext ?? throw new InvalidOperationException("No request to take the partner from.")).Session;

    public string Id => Session.Owner();

    public string? SessionId => Session.BackendSession();

    public string IpAddress => http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";
}

/// <summary>Who the partner is: what the auth API said of them when their session started, kept with the sign-in.</summary>
public sealed class SessionPartnerApi(IHttpContextAccessor http) : IPartnerApi
{
    public Task<PartnerProfile> MeAsync(CancellationToken ct = default)
    {
        var profile = http.HttpContext?.Session.Profile();
        if (profile is null) throw new KeyNotFoundException("Nobody is signed in.");
        return Task.FromResult(profile);
    }
}

/// <summary>
/// The partner the app is being used by, as the backend knows them (GET me), read
/// once a request.
/// </summary>
public sealed class CurrentPartner(IPartnerApi partners)
{
    private PartnerProfile? profile;

    public async Task<PartnerProfile> ProfileAsync(CancellationToken ct = default)
    {
        if (profile is not null) return profile;
        profile = await partners.MeAsync(ct);
        return profile;
    }
}
