using Microsoft.Extensions.Configuration;
using UnoTP.Data;

namespace UnoTP.Tests;

public class DmsPathTests
{
    private static IConfiguration Settings(string root) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Dms:Root"] = root }).Build();

    private static readonly DateTime FiledAt = new(2026, 10, 5, 14, 30, 22, 517);

    [Fact]
    public void A_holder_with_no_folio_has_the_application_holder_document_and_time_in_the_name()
    {
        var name = DmsPaths.FileName("", "FBBMFL26F10421", "01", "PAN", FiledAt, "card.JPG");

        Assert.Equal("FBBMFL26F10421_01_PAN_20261005143022517.jpg", name);
    }

    [Fact]
    public void A_holder_on_a_folio_has_the_folio_in_front()
    {
        var name = DmsPaths.FileName("MF0090001", "FBBMFL26F10421", "01", "PASSPORT_A", FiledAt, "passport.jpeg");

        Assert.Equal("MF0090001_FBBMFL26F10421_01_PASSPORT_A_20261005143022517.jpeg", name);
    }

    [Fact]
    public void Two_uploads_of_one_document_get_different_names()
    {
        var first = DmsPaths.FileName("", "FBBMFL26F10421", "01", "PAN", FiledAt, "card.jpg");
        var second = DmsPaths.FileName("", "FBBMFL26F10421", "01", "PAN", FiledAt.AddMilliseconds(1), "card.jpg");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_copys_path_is_the_root_the_applications_folder_and_its_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "unotp-dms");

        var path = DmsPaths.Under(DmsPaths.Root(Settings(root)), "FBBMFL26F10421", "FBBMFL26F10421_01_PAN_20261005143022517.jpg");

        Assert.Equal(Path.Combine(root, "FBBMFL26F10421", "FBBMFL26F10421_01_PAN_20261005143022517.jpg"), path);
    }

    [Fact]
    public void With_no_root_set_copies_go_to_a_dms_folder_beside_the_app()
    {
        Assert.Equal(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "dms")), DmsPaths.Root(Settings("")));
    }
}
