namespace UnoTP.Infrastructure;

/// <summary>
/// The "Portal" section of appsettings: where the portal's dashboard and logout page are.
/// Home is where Uno TP sends the user back to, with the encrypted UserId and SysCode the
/// portal sent in; Logout is where Logout ends up. Left blank, Home is the console's
/// /Classic and Logout the login portal's root (see <see cref="AppUrls"/>).
/// </summary>
public sealed class PortalOptions
{
    public const string Section = "Portal";

    public string Home { get; set; } = "";

    public string Logout { get; set; } = "";
}
