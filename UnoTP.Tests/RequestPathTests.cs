using Microsoft.AspNetCore.Http;
using UnoTP.Infrastructure;

namespace UnoTP.Tests;

public class RequestPathTests
{
    [Theory]
    [InlineData("//wa_fd_uno_tp", "/wa_fd_uno_tp")]
    [InlineData("/wa_fd_uno_tp", "/wa_fd_uno_tp")]
    [InlineData("/wa_fd_uno_tp//Home///Index", "/wa_fd_uno_tp/Home/Index")]
    [InlineData("//", "/")]
    [InlineData("", "")]
    public void Runs_of_slashes_are_made_one(string asReceived, string expected)
    {
        Assert.Equal(expected, RequestPaths.SingleSlashes(new PathString(asReceived)).Value ?? "");
    }

    [Theory]
    [InlineData("/WA_FD_UNOTP", "/WA_FD_UNOTP")]
    [InlineData("WA_FD_UNOTP", "/WA_FD_UNOTP")]
    [InlineData("/WA_FD_UNOTP/", "/WA_FD_UNOTP")]
    [InlineData("//WA_FD_UNOTP", "/WA_FD_UNOTP")]
    [InlineData(" /apps//unotp/ ", "/apps/unotp")]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData(null, "")]
    public void The_directory_setting_is_taken_however_it_is_typed(string? setting, string expected)
    {
        Assert.Equal(expected, RequestPaths.Directory(setting));
    }
}
