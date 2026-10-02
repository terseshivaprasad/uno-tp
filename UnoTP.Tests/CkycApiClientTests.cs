using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services.Ckyc;

namespace UnoTP.Tests;

public class CkycApiClientTests
{
    private static readonly CkycSearch Investor = new("APP001", "01", "ABCDE1234F", "01-02-1980");

    private static CkycApiClient ClientOver(StubNetwork network) =>
        new(network.Client(), new TestPartner(), Options.Create(new CkycOptions { SearchPath = "search" }));

    [Fact]
    public async Task The_search_is_by_PAN_under_the_APIs_own_names()
    {
        var network = new StubNetwork();

        await ClientOver(network).SearchAsync(Investor);

        Assert.Equal("http://gateway.test/some-api/api/v1/search", network.Address!.ToString());
        Assert.Equal("N", network.Sent.GetProperty("IncludeImages").GetString());
        var search = network.Sent.GetProperty("SearchInCkycSearchParamDetail")[0];
        Assert.Equal("C", search.GetProperty("InputIdType").GetString());
        Assert.Equal("ABCDE1234F", search.GetProperty("InputIdNo").GetString());
        Assert.Equal("01-02-1980", search.GetProperty("DOB").GetString());
        Assert.Equal("APP001", search.GetProperty("ApplicationFormNo").GetString());
        Assert.Equal("APP001019041", search.GetProperty("RecordIdentifier").GetString());
        var transaction = search.GetProperty("TransactionId").GetString()!;
        Assert.Equal(10, transaction.Length);
        Assert.All(transaction, digit => Assert.True(char.IsAsciiDigit(digit)));
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("yes")]
    public async Task A_record_CERSAI_holds_comes_back_with_its_masked_number(string available)
    {
        var network = new StubNetwork
        {
            Answer = $$$"""{"ckycResponse":{"searchInCkycResponseDetail":[{"ckycAvailable":"{{{available}}}","masked_CKYCID":"XXXXXXXXXX1234","ckycName":"A HOLDER","ckycReferenceID":"REF-9"}]}}""",
        };

        var found = await ClientOver(network).SearchAsync(Investor);

        Assert.True(found.Available);
        Assert.Equal("XXXXXXXXXX1234", found.MaskedCkycId);
        Assert.Equal("A HOLDER", found.Name);
        Assert.Equal("REF-9", found.Reference);
    }

    [Fact]
    public async Task No_record_comes_back_with_what_CERSAI_said()
    {
        var network = new StubNetwork
        {
            Answer = """{"ckycResponse":{"searchInCkycResponseDetail":[{"ckycAvailable":"N","transactionRejectionDescription":"No record found"}]}}""",
        };

        var found = await ClientOver(network).SearchAsync(Investor);

        Assert.False(found.Available);
        Assert.Equal("No record found", found.Why);
    }

    [Fact]
    public async Task An_answer_with_no_detail_is_no_record()
    {
        var network = new StubNetwork { Answer = """{"ckycResponse":{"requestRejectionDescription":"Request rejected"}}""" };

        var found = await ClientOver(network).SearchAsync(Investor);

        Assert.False(found.Available);
        Assert.Equal("Request rejected", found.Why);
    }
}
