namespace UnoTp.Features;

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
    /// <summary>Consent to process the holder's personal data (DPDP Act).</summary>
    public bool DpdpConsent { get; set; } = true;

    /// <summary>Authorisation to pull the holder's CKYC record from CERSAI.</summary>
    public bool CkycConsent { get; set; } = true;

    /// <summary>The digital route - one accept link instead of signed forms and an OTP.</summary>
    public bool DigitalConsent { get; set; } = true;

    public FeatureFlags Clone() => (FeatureFlags)MemberwiseClone();
}
