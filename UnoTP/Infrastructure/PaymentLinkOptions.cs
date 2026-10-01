namespace UnoTP.Infrastructure;

/// <summary>
/// The page the investor pays on, from the "PaymentLink" section of appsettings:
/// a template with <c>{appNo}</c> where the application's number goes, e.g.
/// <c>https://pay.example.com/fd/{appNo}</c>. Blank, the app builds no link and the
/// backend makes its own on submit.
/// </summary>
public sealed class PaymentLinkOptions
{
    public const string Section = "PaymentLink";
    public const string AppNoToken = "{appNo}";

    public string Template { get; set; } = "";

    public bool Configured => !string.IsNullOrWhiteSpace(Template);

    /// <summary>The payment link for an application, or null when no template is set.</summary>
    public string? For(string appNo) =>
        Configured ? Template.Trim().Replace(AppNoToken, Uri.EscapeDataString(appNo)) : null;
}
