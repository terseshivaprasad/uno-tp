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

    private const string DemoAgencyKey = "partner.demo.agency";
    private const string DemoBrokerKey = "partner.demo.broker";

    /// <summary>
    /// The agency type and broker code a demo is showing the app as, in place of the
    /// signed-in partner's own; null when none is set (see <see cref="PartnerMiddleware"/>).
    /// </summary>
    public static (string Agency, string? Broker)? DemoPartner(this ISession session) =>
        session.GetString(DemoAgencyKey) is { } agency ? (agency, session.GetString(DemoBrokerKey)) : null;

    public static void SetDemoPartner(this ISession session, string agency, string? broker)
    {
        session.SetString(DemoAgencyKey, agency.Trim());
        if (broker is { Length: > 0 }) session.SetString(DemoBrokerKey, broker.Trim().ToUpperInvariant());
        else session.Remove(DemoBrokerKey);
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

/// <summary>
/// The partner the app is being used by, as the backend knows them (GET me), read
/// once a request. While the demo data is on, a demo partner set with ?agency=
/// stands in for their agency type and broker code.
/// </summary>
public sealed class CurrentPartner(IPartnerApi partners, IHttpContextAccessor http, FeatureSet features)
{
    private PartnerProfile? profile;

    public async Task<PartnerProfile> ProfileAsync(CancellationToken ct = default)
    {
        if (profile is not null) return profile;
        var me = await partners.MeAsync(ct);
        if (features.Flags.DemoData && http.HttpContext?.Session.DemoPartner() is { } demo)
            me = me with { AgencyType = demo.Agency, BrokerCode = demo.Broker ?? me.BrokerCode };
        return profile = me;
    }
}

public static class PartnerMiddleware
{
    /// <summary>
    /// While the demo data is on, ?agency=1033 or ?agency=2001&amp;broker=BR10874 shows
    /// the app as that kind of partner for the rest of the session, so both kinds can
    /// be shown in one sitting. The partner's own details come from the backend.
    /// </summary>
    public static IApplicationBuilder UsePartner(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var demo = ctx.Items[FeatureSet.ItemKey] is FeatureSet { Flags.DemoData: true };
            if (demo && ctx.Request.Query.TryGetValue("agency", out var agency) && agency.ToString().Trim().Length > 0)
                ctx.Session.SetDemoPartner(agency.ToString(), ctx.Request.Query.TryGetValue("broker", out var b) ? b.ToString() : null);
            await next();
        });
}
