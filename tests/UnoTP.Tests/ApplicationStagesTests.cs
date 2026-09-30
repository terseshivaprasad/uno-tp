using UnoTP.Backend;

namespace UnoTP.Tests;

/// <summary>
/// View Application's status list: every application reads the same stages from entry to the FDR,
/// in order, each applying or not by how the application came in and how it pays.
/// </summary>
public class ApplicationStagesTests
{
    private static readonly DateTime Day = new(2026, 9, 1);

    private static List<MilestoneRecord> Stages(bool digital, string payMode, DateTime? submitted = null, DateTime? paid = null,
        string pennyDrop = "", string kyc = "", DateTime? booked = null, string? fdr = null, DateTime? cancelled = null) =>
        ApplicationStages.Of(digital, payMode, Day, submitted, null, null, null, null, pennyDrop, paid, null, kyc, booked, fdr, cancelled);

    private static MilestoneRecord Stage(List<MilestoneRecord> stages, string name) => stages.Single(s => s.Step.StartsWith(name));

    [Fact]
    public void Every_application_reads_the_same_eight_stages_in_order()
    {
        string[] order = ["Application entry", "Short link sent", "Investor acceptance", "Pay-in slip generated",
            "Penny drop · repayment account", "Payment received", "KYC verification", "FDR created"];
        foreach (var (digital, mode) in new[] { (true, "Online"), (true, "RTGS"), (true, "Cheque"), (false, "Cheque"), (false, "RTGS"), (true, "") })
            Assert.Equal(order, Stages(digital, mode).Select(s => s.Step));
    }

    [Theory]
    [InlineData(true, "RTGS", true)]
    [InlineData(true, "Cheque", true)]
    [InlineData(true, "Online", false)]
    [InlineData(false, "Cheque", false)]
    [InlineData(false, "RTGS", false)]
    public void Acceptance_applies_to_a_digital_application_paid_by_RTGS_or_cheque(bool digital, string mode, bool applies) =>
        Assert.Equal(applies, Stage(Stages(digital, mode), "Investor acceptance").Applies);

    [Theory]
    [InlineData("Cheque", true)]
    [InlineData("DD", true)]
    [InlineData("RTGS", false)]
    [InlineData("Online", false)]
    public void A_pay_in_slip_applies_to_an_instrument_only(string mode, bool applies) =>
        Assert.Equal(applies, Stage(Stages(true, mode), "Pay-in slip").Applies);

    [Theory]
    [InlineData(true, "Cheque", true)]
    [InlineData(true, "Online", true)]
    [InlineData(false, "Online", true)]
    [InlineData(false, "Cheque", false)]
    public void A_short_link_goes_to_a_digital_application_or_one_paid_online(bool digital, string mode, bool applies) =>
        Assert.Equal(applies, Stage(Stages(digital, mode), "Short link").Applies);

    [Fact]
    public void Before_the_payment_mode_is_chosen_nothing_that_depends_on_it_is_ruled_out()
    {
        var stages = Stages(true, "");
        foreach (var name in new[] { "Investor acceptance", "Pay-in slip" })
        {
            var s = Stage(stages, name);
            Assert.True(s.Applies);
            Assert.Null(s.At);
            Assert.Contains("payment mode", s.Note);
        }
    }

    [Fact]
    public void A_failed_penny_drop_and_rejected_KYC_are_marked_as_failed()
    {
        var stages = Stages(true, "RTGS", pennyDrop: "FAILED", kyc: "REJECTED");
        Assert.True(Stage(stages, "Penny drop").Failed);
        Assert.True(Stage(stages, "KYC verification").Failed);
        Assert.False(Stage(Stages(true, "RTGS", pennyDrop: "OK", kyc: "OK"), "Penny drop").Failed);
    }

    [Fact]
    public void The_FDR_stage_names_the_receipt_and_a_cancelled_application_ends_on_its_cancellation()
    {
        var booked = Stages(true, "Online", submitted: Day, paid: Day, booked: Day.AddDays(3), fdr: "FD25123456");
        Assert.Equal("FDR FD25123456", Stage(booked, "FDR created").Note);
        Assert.Equal(Day.AddDays(3), Stage(booked, "FDR created").At);

        var cancelled = Stages(true, "Cheque", submitted: Day, cancelled: Day.AddDays(14));
        Assert.Equal("Cancelled", cancelled[^1].Step);
        Assert.True(cancelled[^1].Failed);
    }
}
