namespace UnoTP.Features;

/// <summary>
/// The "Entry" section of appsettings: the user the demo comes in as when the app
/// is opened without the portal's UserId and Syscode. Used only while
/// Features:DemoData is on, and to be emptied once the app reaches a real backend.
/// </summary>
public sealed class EntryOptions
{
    public const string Section = "Entry";

    public string DemoUserId { get; set; } = "";

    public string DemoSysCode { get; set; } = "";
}
