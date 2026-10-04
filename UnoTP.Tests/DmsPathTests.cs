using Microsoft.Extensions.Configuration;
using UnoTP.Data;

namespace UnoTP.Tests;

public class DmsPathTests
{
    private static IConfiguration Settings(string root) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Dms:Root"] = root }).Build();

    [Fact]
    public void A_copys_path_is_recorded_under_the_root_from_settings()
    {
        var root = Path.Combine(Path.GetTempPath(), "unotp-dms");

        var path = DmsPaths.Under(DmsPaths.Root(Settings(root)), "FBBMFL26F10421", "01", "pan", "card.JPG");

        Assert.Equal(Path.Combine(root, "FBBMFL26F10421", "01", "pan.jpg"), path);
    }

    [Fact]
    public void With_no_root_set_copies_go_to_a_dms_folder_beside_the_app()
    {
        Assert.Equal(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "dms")), DmsPaths.Root(Settings("")));
    }
}
