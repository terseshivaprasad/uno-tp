namespace UnoTP.Infrastructure;

/// <summary>
/// The feature switches, bound from the "Features" section of appsettings.json.
/// Each one is a slice of the flow that can be switched on or off on its own.
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

    /// <summary>
    /// Document identification says which proof of address a copy is, and that
    /// sets its type after the upload. Off, the type is chosen from a drop-down
    /// first, and identification only checks the copy is that proof.
    /// </summary>
    public bool DocIdentification { get; set; } = true;

    /// <summary>
    /// A holder whose post goes to an address other than the permanent one uploads a
    /// proof of it, read and checked like the proof of address. Off for this release:
    /// the box is hidden, and the address is typed on Investor Information instead.
    /// </summary>
    public bool CommProofUpload { get; set; }

    /// <summary>The switch behind a console feature key, as the tiles name it.</summary>
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

    /// <summary>Switches off the feature behind a console feature key.</summary>
    public void SwitchOff(string key)
    {
        switch (key)
        {
            case "new-fd": NewFd = false; break;
            case "pis": PisGeneration = false; break;
            case "view-app": ViewApplication = false; break;
            case "short-url": ShortUrl = false; break;
            case "app-status": ApplicationStatus = false; break;
            case "renew": RenewFd = false; break;
            case "admin": Admin = false; break;
        }
    }

    public FeatureFlags Clone() => (FeatureFlags)MemberwiseClone();
}
