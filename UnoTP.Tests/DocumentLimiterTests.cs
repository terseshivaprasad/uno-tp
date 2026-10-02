using Microsoft.Extensions.Options;
using UnoTP.Infrastructure;

namespace UnoTP.Tests;

public class DocumentLimiterTests
{
    private static DocumentLimiter LimiterOf(int perMinute) =>
        new(Options.Create(new RateLimitOptions { PerDocumentPerMinute = perMinute }));

    [Fact]
    public void The_fourth_try_at_a_holders_document_in_a_minute_is_turned_back()
    {
        using var limiter = LimiterOf(3);

        Assert.True(limiter.Allows("session-1", "APP001", "01", "pan"));
        Assert.True(limiter.Allows("session-1", "APP001", "01", "pan"));
        Assert.True(limiter.Allows("session-1", "APP001", "01", "pan"));
        Assert.False(limiter.Allows("session-1", "APP001", "01", "pan"));
        Assert.False(limiter.Allows("session-1", "APP001", "01", "pan"));
    }

    [Fact]
    public void Each_holder_and_document_type_is_counted_on_its_own_within_a_session_and_application()
    {
        using var limiter = LimiterOf(1);
        Assert.True(limiter.Allows("session-1", "APP001", "01", "pan"));
        Assert.False(limiter.Allows("session-1", "APP001", "01", "pan"));

        // The same holder's other document, another holder's document of the same
        // type, another application and another session are all untouched by it.
        Assert.True(limiter.Allows("session-1", "APP001", "01", "poa"));
        Assert.True(limiter.Allows("session-1", "APP001", "02", "pan"));
        Assert.True(limiter.Allows("session-1", "APP002", "01", "pan"));
        Assert.True(limiter.Allows("session-2", "APP001", "01", "pan"));
    }

    [Fact]
    public void Zero_takes_the_limit_off()
    {
        using var limiter = LimiterOf(0);

        for (var i = 0; i < 100; i++) Assert.True(limiter.Allows("session-1", "APP001", "01", "pan"));
    }

    [Fact]
    public void What_the_page_says_names_the_limit()
    {
        using var limiter = LimiterOf(3);

        Assert.Contains("more than 3 tries in a minute", limiter.Said);
    }
}
