using UnoTP.Models;

namespace UnoTP.Tests;

/// <summary>The row a submitted application's short link is put on record with (Models/LinkRequest.cs).</summary>
public class LinkRequestTests
{
    private static Application Submitted() => new()
    {
        AppNo = "DEVBB26F10001",
        Holder = new Holder("ABCPA1001A", "01-01-1980", "Asha Test", "", PanFiled: true),
        Deposit = new DepositDetails(250000, 12, "M", AutoRenewal: false, RenewInstruction: "", NoTds: false, DeliveryType: ""),
        Submitted = new Submission(new DateTime(2026, 10, 12, 10, 30, 0), "payment-pending", "", new DateTime(2026, 10, 15), 1),
    };

    [Fact]
    public void A_link_sent_again_is_open_for_24_hours()
    {
        Assert.Equal(24, LinkRequestRow.RegeneratedForHours);
    }

    [Fact]
    public void The_row_names_the_application_its_investor_and_the_amount()
    {
        var row = LinkRequestRow.For(Submitted(), new PaymentLink("https://pay.test/DEVBB26F10001", "https://s.test/abc"), validityHours: 72);

        Assert.Equal("DEVBB26F10001", row.AppNo);
        Assert.Equal("Asha Test", row.InvestorName);
        Assert.Equal("ABCPA1001A", row.InvestorPan);
        Assert.Equal(250000, row.InvestmentAmount);
        Assert.Equal(new DateTime(2026, 10, 12, 10, 30, 0), row.ApplicationDate);
    }

    [Fact]
    public void The_row_carries_the_short_link_the_page_it_opens_and_its_hours()
    {
        var row = LinkRequestRow.For(Submitted(), new PaymentLink("https://pay.test/DEVBB26F10001", "https://s.test/abc"), validityHours: 72);

        Assert.Equal("https://s.test/abc", row.ShortUrl);
        Assert.Equal("https://pay.test/DEVBB26F10001", row.FullUrl);
        Assert.Equal(72, row.ValidityHours);
    }

    [Fact]
    public void The_investor_is_named_as_NSDL_verified_them_when_it_did()
    {
        var app = Submitted();
        app.Upload = new UploadState { Name = "ASHA VERIFIED TEST" };

        var row = LinkRequestRow.For(app, link: null, validityHours: 72);

        Assert.Equal("ASHA VERIFIED TEST", row.InvestorName);
    }

    [Fact]
    public void A_link_not_shortened_or_not_built_leaves_its_columns_empty()
    {
        var notShortened = LinkRequestRow.For(Submitted(), new PaymentLink("https://pay.test/DEVBB26F10001", null), validityHours: 72);
        var notBuilt = LinkRequestRow.For(Submitted(), link: null, validityHours: 72);

        Assert.Null(notShortened.ShortUrl);
        Assert.Equal("https://pay.test/DEVBB26F10001", notShortened.FullUrl);
        Assert.Null(notBuilt.ShortUrl);
        Assert.Null(notBuilt.FullUrl);
    }
}
