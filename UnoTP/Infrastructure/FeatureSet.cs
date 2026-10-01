namespace UnoTP.Infrastructure;

/// <summary>
/// The features in force for one request: the appsettings switches, with the ones
/// the user's menu does not open switched off.
/// </summary>
public sealed class FeatureSet(FeatureFlags flags)
{
    public const string ItemKey = "unotp.features";

    public FeatureFlags Flags { get; } = flags;

    /// <summary>The features the user's menu does not open. They are off whatever else says.</summary>
    public IReadOnlySet<string> NotInMenu { get; private init; } = new HashSet<string>();

    /// <summary>The console's own features, which the user's menu opens or not.</summary>
    public static readonly string[] MenuKeys = ["new-fd", "pis", "view-app", "short-url", "app-status", "renew", "admin"];

    /// <summary>These features, with the ones the user's menu does not open switched off.</summary>
    public FeatureSet WithMenu(IReadOnlySet<string> menu)
    {
        var missing = MenuKeys.Where(k => !menu.Contains(k)).ToHashSet();
        if (missing.Count == 0) return this;
        var flags = Flags.Clone();
        foreach (var key in missing) flags.SwitchOff(key);
        return new FeatureSet(flags) { NotInMenu = missing };
    }
}
