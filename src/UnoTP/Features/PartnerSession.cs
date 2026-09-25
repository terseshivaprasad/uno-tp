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

    private const string AgencyKey = "partner.agency";
    private const string BrokerKey = "partner.broker";

    /// <summary>The agency type that sources through any mode and books any category.</summary>
    public const string SourcingAgency = "1033";

    /// <summary>
    /// The partner's agency type. 1033 chooses how an application is sourced and
    /// what it is booked as; every other type sources as a broker under its own
    /// business broker code. Set at sign-in; until the app has one, it comes from
    /// the "Partner" settings, or from ?agency= while the demo data is on.
    /// </summary>
    public static string AgencyType(this ISession session) => session.GetString(AgencyKey) ?? "";

    /// <summary>The partner's business broker code, which every application a
    /// non-1033 partner sources is filed under.</summary>
    public static string BrokerCode(this ISession session) => session.GetString(BrokerKey) ?? "";

    public static bool IsSourcingAgency(this ISession session) => session.AgencyType() == SourcingAgency;

    public static void SetPartner(this ISession session, string agencyType, string brokerCode)
    {
        session.SetString(AgencyKey, agencyType.Trim());
        session.SetString(BrokerKey, brokerCode.Trim().ToUpperInvariant());
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

/// <summary>Who the partner is until the app has a sign-in, from the "Partner" settings.</summary>
public sealed class PartnerOptions
{
    public const string Section = "Partner";

    public string AgencyType { get; set; } = PartnerSession.SourcingAgency;

    public string BrokerCode { get; set; } = "";
}

public static class PartnerMiddleware
{
    /// <summary>
    /// Gives a session with no partner yet the configured one. While the demo data
    /// is on, ?agency=1033 or ?agency=2001&amp;broker=BR10874 switches the partner, so
    /// both kinds can be shown in one sitting.
    /// </summary>
    public static IApplicationBuilder UsePartner(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var session = ctx.Session;
            var options = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<PartnerOptions>>().Value;
            if (session.GetString("partner.agency") is null) session.SetPartner(options.AgencyType, options.BrokerCode);

            var demo = ctx.Items[FeatureSet.ItemKey] is FeatureSet { Flags.DemoData: true };
            if (demo && ctx.Request.Query.TryGetValue("agency", out var agency) && agency.ToString().Trim().Length > 0)
            {
                var broker = ctx.Request.Query.TryGetValue("broker", out var b) && b.ToString().Trim().Length > 0 ? b.ToString() : options.BrokerCode;
                session.SetPartner(agency.ToString(), broker);
            }
            await next();
        });
}
