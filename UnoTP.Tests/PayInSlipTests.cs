using UnoTP.Models;

namespace UnoTP.Tests;

/// <summary>The two rows a generated pay-in slip is written as (Models/PayInSlip.cs).</summary>
public class PayInSlipTests
{
    private sealed class SignedIn : IPartner
    {
        public string Id => "100002225";
        public string? SessionId { get; init; } = "987654321";
        public string IpAddress => "127.0.0.1";
        public string UserName => "Dev User";
    }

    private static readonly PayInSlipStagingRow Staged = new()
    {
        ApplicationNumber = "DEVBB26F10001",
        ApplicantName = "Asha Test",
        AgencyCode = "AG001",
        ChequeNumber = "123456",
        ChequeDate = "05-10-2026",
        ChequeAmount = "250000",
        DrawnBankName = "Test Bank",
        DrawnBranchName = "Shivajinagar",
        DrawnBankMicr = "411000001",
        DrawnBankIfsc = "TEST0001234",
        CmsBankName = "Axis Bank",
        CmsLocationCode = "PN01",
        CmsLocationName = "Pune Camp",
    };

    [Fact]
    public void The_slip_and_its_cheque_carry_the_same_number()
    {
        var slip = PayInSlipHeaderRow.For(Staged, "AXPIS0042", new SignedIn());
        var cheque = PayInSlipDetailRow.For(Staged, "AXPIS0042", new SignedIn());

        Assert.Equal("AXPIS0042", slip.SlipNo);
        Assert.Equal("AXPIS0042", cheque.SlipNo);
    }

    [Fact]
    public void A_slip_carries_one_cheque_to_the_CMS_location_staged()
    {
        var slip = PayInSlipHeaderRow.For(Staged, "AXPIS0042", new SignedIn());

        Assert.Equal(1, slip.ChequeCount);
        Assert.Equal("AG001", slip.AgencyCode);
        Assert.Equal("Axis Bank", slip.CmsBankName);
        Assert.Equal("PN01", slip.CmsLocationCode);
        Assert.Equal("Pune Camp", slip.CmsLocationName);
    }

    [Fact]
    public void The_cheque_is_the_staged_one_with_its_date_as_a_date_and_its_amount_as_a_number()
    {
        var cheque = PayInSlipDetailRow.For(Staged, "AXPIS0042", new SignedIn());

        Assert.Equal("DEVBB26F10001", cheque.ApplicationNumber);
        Assert.Equal("Asha Test", cheque.ApplicantName);
        Assert.Equal("123456", cheque.ChequeNumber);
        Assert.Equal(new DateTime(2026, 10, 5), cheque.ChequeDate);
        Assert.Equal(250000, cheque.ChequeAmount);
        Assert.Equal("Test Bank", cheque.DrawnBankName);
        Assert.Equal("Shivajinagar", cheque.DrawnBranchName);
        Assert.Equal("411000001", cheque.DrawnBankMicr);
        Assert.Equal("TEST0001234", cheque.DrawnBankIfsc);
        Assert.Equal("PN01", cheque.CmsLocationCode);
        Assert.Equal("Pune Camp", cheque.CmsLocationName);
    }

    [Fact]
    public void A_date_or_an_amount_that_does_not_read_is_left_empty()
    {
        var staged = Staged with { ChequeDate = "5 Oct", ChequeAmount = "2,50,000" };

        var cheque = PayInSlipDetailRow.For(staged, "AXPIS0042", new SignedIn());

        Assert.Null(cheque.ChequeDate);
        Assert.Null(cheque.ChequeAmount);
    }

    [Fact]
    public void Both_rows_say_who_generated_the_slip()
    {
        var slip = PayInSlipHeaderRow.For(Staged, "AXPIS0042", new SignedIn());
        var cheque = PayInSlipDetailRow.For(Staged, "AXPIS0042", new SignedIn());

        Assert.Equal(("Dev User", "127.0.0.1", 987654321L), (slip.CreatedByName, slip.CreatedIp, slip.SessionId));
        Assert.Equal(("Dev User", "127.0.0.1", 987654321L), (cheque.CreatedByName, cheque.CreatedIp, cheque.SessionId));
    }

    [Fact]
    public void A_session_that_is_not_a_number_is_left_empty()
    {
        var slip = PayInSlipHeaderRow.For(Staged, "AXPIS0042", new SignedIn { SessionId = "not-a-number" });

        Assert.Null(slip.SessionId);
    }
}
