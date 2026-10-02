using UnoTP.Models;

namespace UnoTP.Tests;

public class RefusedWaitTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromMinutes(15);
    private static readonly DateTime Refused = new(2026, 10, 2, 10, 0, 0);

    private static UploadState RefusedTimes(int times) => new()
    {
        Attempts = { ["pan"] = times },
        RefusedAt = { ["pan"] = Refused },
    };

    [Fact]
    public void A_document_with_tries_left_has_nothing_to_wait_for()
    {
        var state = RefusedTimes(2);

        Assert.Null(state.RetryAt("pan", 3, Wait));
        Assert.Equal(2, state.AttemptsNow("pan", 3, Wait, Refused.AddMinutes(1)));
    }

    [Fact]
    public void Refused_three_times_in_a_row_it_waits_from_the_last_refusal()
    {
        var state = RefusedTimes(3);

        Assert.Equal(Refused + Wait, state.RetryAt("pan", 3, Wait));
        Assert.Equal(3, state.AttemptsNow("pan", 3, Wait, Refused.AddMinutes(14)));
    }

    [Fact]
    public void Once_the_wait_is_over_the_count_starts_again()
    {
        var state = RefusedTimes(3);

        Assert.Equal(0, state.AttemptsNow("pan", 3, Wait, Refused.AddMinutes(15)));
    }

    [Fact]
    public void A_refusal_from_before_refusals_were_dated_has_waited_long_enough()
    {
        var state = new UploadState { Attempts = { ["pan"] = 3 } };

        Assert.Equal(0, state.AttemptsNow("pan", 3, Wait, Refused));
    }

    [Fact]
    public void Another_document_is_untouched_by_it()
    {
        var state = RefusedTimes(3);

        Assert.Null(state.RetryAt("poa", 3, Wait));
        Assert.Equal(0, state.AttemptsNow("poa", 3, Wait, Refused));
    }
}
