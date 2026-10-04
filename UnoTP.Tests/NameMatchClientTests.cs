using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.NameMatch;

namespace UnoTP.Tests;

public class NameMatchClientTests
{
    private static NameMatchClient ClientOver(StubNetwork network) =>
        new(network.Client(), new TestPartner(), Options.Create(new NameMatchOptions { MatchPath = "match" }));

    [Fact]
    public async Task The_two_names_go_under_the_APIs_own_names()
    {
        var network = new StubNetwork { Answer = """{"status":"SUCCESS","error_code":"","error_message":""}""" };

        await ClientOver(network).MatchAsync("ESHA K MEHTA", "ESHA KIRAN MEHTA");

        Assert.Equal(HttpMethod.Post, network.Method);
        Assert.Equal("http://gateway.test/some-api/api/v1/match", network.Address!.ToString());
        Assert.Equal("ESHA K MEHTA", network.Sent.GetProperty("SourceName").GetString());
        Assert.Equal("ESHA KIRAN MEHTA", network.Sent.GetProperty("TargetName").GetString());
    }

    [Fact]
    public async Task Success_is_a_match()
    {
        var network = new StubNetwork { Answer = """{"status":"SUCCESS","error_code":"","error_message":""}""" };

        var answer = await ClientOver(network).MatchAsync("A NAME", "A NAME");

        Assert.Equal(NameMatchOutcome.Match, answer.Outcome);
    }

    [Fact]
    public async Task Fail_with_no_error_code_is_the_names_not_matching()
    {
        var network = new StubNetwork { Answer = """{"status":"FAIL","error_code":"","error_message":"Name match criteria does not meet"}""" };

        var answer = await ClientOver(network).MatchAsync("A NAME", "ANOTHER NAME");

        Assert.True(answer.IsMismatch);
    }

    [Fact]
    public async Task Fail_with_an_error_code_is_an_outage_said_with_the_APIs_message()
    {
        var network = new StubNetwork { Answer = """{"status":"FAIL","error_code":"E500","error_message":"Service unavailable"}""" };

        var outage = await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MatchAsync("A NAME", "A NAME"));

        Assert.Contains("Service unavailable", outage.Message);
    }

    [Fact]
    public async Task A_status_that_is_neither_is_an_outage_not_a_mismatch()
    {
        var network = new StubNetwork { Answer = """{"status":"","error_code":"","error_message":""}""" };

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MatchAsync("A NAME", "A NAME"));
    }
}
