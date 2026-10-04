using Microsoft.Extensions.Configuration;
using UnoTP.Services;

namespace UnoTP.Tests;

public class PanCheckSwitchTests
{
    private static OutsideSwitches With(string? panCheck) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Backend:Switches:PanCheck"] = panCheck }).Build());

    [Fact]
    public void The_pan_check_is_on_unless_it_is_set_false()
    {
        Assert.True(With(null).IsOn(OutsideSwitches.PanCheck));
        Assert.True(With("true").IsOn(OutsideSwitches.PanCheck));
    }

    [Fact]
    public void Set_false_the_pan_check_is_off()
    {
        Assert.False(With("false").IsOn(OutsideSwitches.PanCheck));
    }
}
