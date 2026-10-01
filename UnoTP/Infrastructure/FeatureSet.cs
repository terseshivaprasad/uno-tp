namespace UnoTP.Infrastructure;

/// <summary>The features in force: the switches in the "Features" section of appsettings, and nothing else.</summary>
public sealed class FeatureSet(FeatureFlags flags)
{
    public FeatureFlags Flags { get; } = flags;
}
