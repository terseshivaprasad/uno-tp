namespace UnoTP.Backend;

/// <summary>
/// How a partner comes into the app: the portal opens it with the user id and the
/// system code, both encrypted; once decrypted, the backend starts a session for
/// them and says which menus they may open.
/// </summary>
public interface ISessionApi
{
    /// <summary>
    /// POST sessions { userId, sysCode }: starts a session for the user on this
    /// system. Null when the backend refuses it - an unknown user, or a system
    /// code that is not this app's.
    /// </summary>
    Task<UserSession?> StartAsync(string userId, string sysCode, CancellationToken ct = default);

    /// <summary>GET menu: the menus the session's user may open, by the console's feature keys.</summary>
    Task<IReadOnlyList<MenuItem>> MenuAsync(CancellationToken ct = default);
}

/// <param name="SessionId">Sent back with every call made for the user, as X-Session-Id.</param>
/// <param name="UserId">The user the session is for; every call is made on their behalf.</param>
/// <param name="ExpiresAt">When the session ends; the user comes in from the portal again after that.</param>
public sealed record UserSession(string SessionId, string UserId, DateTime ExpiresAt);

/// <param name="Key">The console feature key the menu opens: new-fd, pis, view-app, short-url, app-status, renew or admin.</param>
public sealed record MenuItem(string Key, string Name);
