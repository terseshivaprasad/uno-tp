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

    /// <summary>The link for an application - a renewal's from its own template - or null when that template is not set.</summary>
    public string? For(string appNo, bool renewal)
    {
        var template = renewal ? RenewalTemplate : Template;
        if (string.IsNullOrWhiteSpace(template)) return null;
        return template.Trim().Replace(AppNoToken, Uri.EscapeDataString(appNo));
    }
}
