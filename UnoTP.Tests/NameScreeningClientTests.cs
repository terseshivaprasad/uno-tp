using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services.NameScreening;

namespace UnoTP.Tests;

public class NameScreeningClientTests
{
    private static readonly NameScreeningRequest Holder = new("APP001", "01", "A HOLDER", "01-02-1980", "9000000000");

    private static NameScreeningClient ClientOver(StubNetwork network, string apiCall = "1") =>
        new(network.Client(), new TestPartner(), Options.Create(new NameScreeningOptions { ScreenPath = "screen", ApiKey = "test-key", ApiCall = apiCall }));

    [Fact]
    public async Task Switched_off_nobody_is_screened_and_nothing_is_sent()
    {
        var network = new StubNetwork();

        var result = await ClientOver(network, apiCall: "0").ScreenAsync(Holder);

        Assert.True(result.Allowed);
        Assert.Equal(NameScreeningResult.Skipped, result.Reference);
        Assert.Equal(0, network.Calls);
    }

    [Fact]
    public async Task The_request_carries_the_key_and_the_holder_under_the_APIs_own_names()
    {
        var network = new StubNetwork { Answer = """{"status":"SUCCESS","nameScreeingStatus":"ALLOWED"}""" };

        await ClientOver(network).ScreenAsync(Holder);

        Assert.Equal("http://gateway.test/some-api/api/v1/screen", network.Address!.ToString());
        Assert.Equal("test-key", network.Headers["apikey"]);
        var sent = network.Sent;
        Assert.Equal("A HOLDER", sent.GetProperty("name1").GetString());
        Assert.Equal("01-02-1980", sent.GetProperty("dob").GetString());
        Assert.Equal("APP001", sent.GetProperty("appl_No").GetString());
        Assert.Equal("01", sent.GetProperty("holderType").GetString());
        Assert.Equal("9000000000", sent.GetProperty("mobileNo").GetString());
        Assert.Equal("9041", sent.GetProperty("sessionId").GetString());
        Assert.Equal("1", sent.GetProperty("Api_call").GetString());
    }

    [Theory]
    [InlineData("SUCCESS", "ALLOWED", true)]
    [InlineData("success", "allowed", true)]
    [InlineData("SUCCESS", "NOT ALLOWED", false)]
    [InlineData("FAILED", "ALLOWED", false)]
    [InlineData("", "", false)]
    public async Task A_holder_is_allowed_only_on_SUCCESS_and_ALLOWED(string status, string screening, bool allowed)
    {
        var network = new StubNetwork { Answer = $$$"""{"status":"{{{status}}}","nameScreeingStatus":"{{{screening}}}","data":{"uniqueRequestId":"REF-1"}}""" };

        var result = await ClientOver(network).ScreenAsync(Holder);

        Assert.Equal(allowed, result.Allowed);
        Assert.Equal("REF-1", result.Reference);
    }
}
