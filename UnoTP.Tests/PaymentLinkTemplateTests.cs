using UnoTP.Infrastructure;

namespace UnoTP.Tests;

public class PaymentLinkTemplateTests
{
    private static readonly PaymentLinkOptions Both = new()
    {
        Template = "https://pay.example.com/fd/{appNo}",
        RenewalTemplate = "https://pay.example.com/renew/{appNo}",
    };

    [Fact]
    public void A_purchase_takes_the_purchase_template()
    {
        Assert.Equal("https://pay.example.com/fd/FBBMFL26F10138", Both.For("FBBMFL26F10138", renewal: false));
    }

    [Fact]
    public void A_renewal_takes_the_renewal_template()
    {
        Assert.Equal("https://pay.example.com/renew/FBBMFL26F10248", Both.For("FBBMFL26F10248", renewal: true));
    }

    [Fact]
    public void A_renewal_does_not_borrow_the_purchase_template()
    {
        var purchaseOnly = new PaymentLinkOptions { Template = "https://pay.example.com/fd/{appNo}" };

        Assert.Null(purchaseOnly.For("FBBMFL26F10248", renewal: true));
        Assert.NotNull(purchaseOnly.For("FBBMFL26F10248", renewal: false));
    }

    [Fact]
    public void A_blank_template_builds_no_link()
    {
        var none = new PaymentLinkOptions { Template = "  ", RenewalTemplate = "" };

        Assert.Null(none.For("FBBMFL26F10138", renewal: false));
        Assert.Null(none.For("FBBMFL26F10138", renewal: true));
    }
}
