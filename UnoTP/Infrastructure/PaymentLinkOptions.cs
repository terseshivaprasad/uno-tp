namespace UnoTP.Infrastructure;

/// <summary>
/// The page the investor is sent to, from the "PaymentLink" section of appsettings:
/// one template for a purchase, another for a renewal, each with <c>{appNo}</c> where
/// the application's number goes, e.g. <c>https://pay.example.com/fd/{appNo}</c>.
/// A blank template builds no link for that kind of application.
/// </summary>
public sealed class PaymentLinkOptions
{
    public const string Section = "PaymentLink";
    public const string AppNoToken = "{appNo}";

    /// <summary>The page a purchase's investor pays on.</summary>
    public string Template { get; set; } = "";

    /// <summary>The page a renewal's investor is sent to.</summary>
    public string RenewalTemplate { get; set; } = "";

    /// <summary>
    /// The link for an application - a renewal's from its own template - with the key the
    /// investor's page opens it by (?key=, as the FD system's own app builds it); or null when
    /// that template is not set.
    /// </summary>
    public string? For(string appNo, bool renewal, string key)
    {
        var template = renewal ? RenewalTemplate : Template;
        if (string.IsNullOrWhiteSpace(template)) return null;
        var page = template.Trim().Replace(AppNoToken, Uri.EscapeDataString(appNo));
        if (key.Length == 0) return page;
        return page + (page.Contains('?') ? "&" : "?") + "key=" + Uri.EscapeDataString(key);
    }

    /// <summary>
    /// A key for a link, as the FD system's own app makes one: two GUIDs and the clock's ticks,
    /// one after the other. (That app takes its first GUID from the database, NEWID(); it is
    /// the same kind of GUID made here.)
    /// </summary>
    public static string NewKey() => Guid.NewGuid().ToString() + Guid.NewGuid().ToString() + DateTime.Now.Ticks;
}
