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
        Assert.Equal("https://pay.example.com/fd/FBBMFL26F10138", Both.For("FBBMFL26F10138", renewal: false, key: ""));
    }

    [Fact]
    public void A_renewal_takes_the_renewal_template()
    {
        Assert.Equal("https://pay.example.com/renew/FBBMFL26F10248", Both.For("FBBMFL26F10248", renewal: true, key: ""));
    }

    [Fact]
    public void A_renewal_does_not_borrow_the_purchase_template()
    {
        var purchaseOnly = new PaymentLinkOptions { Template = "https://pay.example.com/fd/{appNo}" };

        Assert.Null(purchaseOnly.For("FBBMFL26F10248", renewal: true, key: ""));
        Assert.NotNull(purchaseOnly.For("FBBMFL26F10248", renewal: false, key: ""));
    }

    [Fact]
    public void A_blank_template_builds_no_link()
    {
        var none = new PaymentLinkOptions { Template = "  ", RenewalTemplate = "" };

        Assert.Null(none.For("FBBMFL26F10138", renewal: false, key: ""));
        Assert.Null(none.For("FBBMFL26F10138", renewal: true, key: ""));
    }

    [Fact]
    public void The_key_goes_on_the_query_string()
    {
        Assert.Equal("https://pay.example.com/fd/FBBMFL26F10138?key=abc123", Both.For("FBBMFL26F10138", renewal: false, key: "abc123"));

        var withQuery = new PaymentLinkOptions { Template = "https://pay.example.com/fd?app={appNo}" };
        Assert.Equal("https://pay.example.com/fd?app=FBBMFL26F10138&key=abc123", withQuery.For("FBBMFL26F10138", renewal: false, key: "abc123"));
    }

    [Fact]
    public void A_key_is_two_guids_and_the_ticks_and_never_the_same_twice()
    {
        var key = PaymentLinkOptions.NewKey();

        Assert.True(key.Length > 72, key);
        Assert.True(Guid.TryParse(key[..36], out _));
        Assert.True(Guid.TryParse(key[36..72], out _));
        Assert.True(long.TryParse(key[72..], out _));
        Assert.NotEqual(key, PaymentLinkOptions.NewKey());
    }
}
