using UnoTP.Infrastructure;

namespace UnoTP.Tests;

public class UploadProgressTests
{
    [Fact]
    public void The_stage_said_last_is_the_one_the_wait_shows()
    {
        var progress = new UploadProgress();

        progress.Say("session-1", "APP001", "Identifying the document");
        progress.Say("session-1", "APP001", "Reading the document (OCR)");

        Assert.Equal("Reading the document (OCR)", progress.Of("session-1", "APP001"));
    }

    [Fact]
    public void An_upload_is_its_own_sessions_and_its_own_applications()
    {
        var progress = new UploadProgress();
        progress.Say("session-1", "APP001", "Filing the copy");

        Assert.Null(progress.Of("session-2", "APP001"));
        Assert.Null(progress.Of("session-1", "APP002"));
    }

    [Fact]
    public void Once_the_upload_is_over_there_is_nothing_to_say()
    {
        var progress = new UploadProgress();
        progress.Say("session-1", "APP001", "Filing the copy");

        progress.Done("session-1", "APP001");

        Assert.Null(progress.Of("session-1", "APP001"));
    }

    [Fact]
    public void With_nothing_under_way_there_is_nothing_to_say()
    {
        Assert.Null(new UploadProgress().Of("session-1", "APP001"));
    }
}
