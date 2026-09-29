using UnoTP.Backend.Mock.External;
using UnoTP.Backend.Shortener;
using UnoTP.Infrastructure;

namespace UnoTP.Tests;

public class PaymentLinkTests
{
    [Fact]
    public void The_template_takes_the_application_number()
    {
        var options = new PaymentLinkOptions { Template = "https://pay.example.com/fd/{appNo}" };
        Assert.Equal("https://pay.example.com/fd/FBBMFL26F55QP9", options.For("FBBMFL26F55QP9"));
    }

    [Fact]
    public void A_number_is_escaped_into_the_template()
    {
        var options = new PaymentLinkOptions { Template = "https://pay.example.com/fd?app={appNo}" };
        Assert.Equal("https://pay.example.com/fd?app=A%2FB%26C", options.For("A/B&C"));
    }

    [Fact]
    public void No_template_means_no_link()
    {
        Assert.Null(new PaymentLinkOptions().For("FBBMFL26F55QP9"));
        Assert.Null(new PaymentLinkOptions { Template = "  " }.For("FBBMFL26F55QP9"));
    }

    [Fact]
    public async Task The_mock_shortener_answers_the_same_short_link_for_the_same_url()
    {
        var shortener = new MockShortener();
        var first = await shortener.ShortenAsync("https://pay.example.com/fd/FBBMFL26F55QP9");
        var again = await shortener.ShortenAsync("https://pay.example.com/fd/FBBMFL26F55QP9");
        var other = await shortener.ShortenAsync("https://pay.example.com/fd/OTHER");

        Assert.StartsWith(MockShortener.Host, first);
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Matches("^[A-Za-z0-9]{8}$", first![MockShortener.Host.Length..]);
    }

    [Fact]
    public async Task With_no_shortener_the_link_goes_unshortened()
    {
        Assert.Null(await new UnshortenedLinks().ShortenAsync("https://pay.example.com/fd/X"));
    }
}
