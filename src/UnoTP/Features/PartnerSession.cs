using System.Text.Json;
using UnoTP.Backend;

namespace UnoTP.Features;

/// <summary>
/// What the server keeps for one partner's browser, so none of it has to travel
/// in an address or a hidden field: who the partner is, the application they are
/// on, and the search that found it. Addresses carry no PAN, date of birth, name,
/// folio or application number - they end up in logs, history and Referer headers,
/// and anything in a hidden field can be changed before it is posted back.
///
/// Held in ASP.NET Core session state behind an HttpOnly, SameSite=Strict cookie.
/// </summary>
public static class PartnerSession
{
    private const string OwnerKey = "partner";
    private const string ApplicationKey = "application";

    /// <summary>
    /// Whose applications this browser may open. With no sign-in in this mock it
    /// is a random id kept for the session; behind a real login it is the
    /// signed-in partner's own code, so an application only ever opens for them.
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

    /// <summary>The application the partner is working on, by number.</summary>
    public static string? CurrentApplication(this ISession session) => session.GetString(ApplicationKey);

    public static void SetCurrentApplication(this ISession session, string? appNo)
    {
        if (appNo is null) session.Remove(ApplicationKey);
        else session.SetString(ApplicationKey, appNo);
    }

    public static T? Read<T>(this ISession session, string key) where T : class =>
        session.GetString(key) is { } json ? JsonSerializer.Deserialize<T>(json) : null;

    public static void Write<T>(this ISession session, string key, T? value) where T : class
    {
        if (value is null) session.Remove(key);
        else session.SetString(key, JsonSerializer.Serialize(value));
    }
}

/// <summary>The partner the backend is asked on behalf of: the session's owner.</summary>
public sealed class SessionPartner(IHttpContextAccessor http) : IPartner
{
    public string Id => (http.HttpContext ?? throw new InvalidOperationException("No request to take the partner from.")).Session.Owner();
}
