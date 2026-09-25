namespace UnoTP.Features;

/// <summary>
/// The prototype's feature switches, bound from the "Features" section of
/// appsettings.json. Each one is a slice of the flow that can be demoed on or off
/// independently - see <see cref="FeatureSet"/> for the per-session override.
///
/// Adding a feature is two lines: a property here and an entry in
/// <see cref="FeatureSet.Switches"/> so the ?ff= override can reach it.
/// </summary>
public sealed class FeatureFlags
{
    // The classic dashboard's features. One that is off shows as a greyed tile
    // and its pages send anyone who opens them back to the dashboard.

    /// <summary>Create New FD: investor search through to the upload step.</summary>
    public bool NewFd { get; set; } = true;

    /// <summary>PIS Generation - Axis pay-in slips.</summary>
    public bool PisGeneration { get; set; } = true;

    /// <summary>View existing application.</summary>
    public bool ViewApplication { get; set; } = true;

    /// <summary>Short URL - the payment and acceptance links sent to investors.</summary>
    public bool ShortUrl { get; set; } = true;

    /// <summary>Application status. Off while it is rebuilt.</summary>
    public bool ApplicationStatus { get; set; }

    /// <summary>Renew FD. Off until it is built.</summary>
    public bool RenewFd { get; set; }

    /// <summary>The Console Admin tile and page, for administrators only.</summary>
    public bool Admin { get; set; }

    /// <summary>The test data card at the foot of Investor Identification. Off in production.</summary>
    public bool DemoData { get; set; } = true;

    /// <summary>The switch behind a console feature key, as ?ff= and the tiles name it.</summary>
    public bool IsOn(string key) => key switch
    {
        "new-fd" => NewFd,
        "pis" => PisGeneration,
        "view-app" => ViewApplication,
        "short-url" => ShortUrl,
        "app-status" => ApplicationStatus,
        "renew" => RenewFd,
        "admin" => Admin,
        _ => true,
    };

    public FeatureFlags Clone() => (FeatureFlags)MemberwiseClone();
}
