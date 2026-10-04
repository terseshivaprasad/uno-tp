using System.Net;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.Pan;

namespace UnoTP.Tests;

public class PanApiClientTests
{
    private static readonly PanToVerify Holder = new("APP001", "01", "ABCDE1234F", "01-02-1980", "A HOLDER");

    private static PanApiClient ClientOver(StubNetwork network) =>
        new(network.Client(), new TestPartner(), new TestPartnerApi(), Options.Create(new PanApiOptions { VerifyPath = "verify-pan" }));

    [Fact]
    public async Task The_request_goes_to_the_verify_path_with_the_holder_and_the_session()
    {
        var network = new StubNetwork { Answer = """{"PAN_No_Match_Status":"1","PAN_DOB_Match_Status":"1","PAN_Name_Match_Status":"1"}""" };

        await ClientOver(network).VerifyAsync(Holder);

        Assert.Equal(HttpMethod.Post, network.Method);
        Assert.Equal("http://gateway.test/some-api/api/v1/verify-pan", network.Address!.ToString());
        var sent = network.Sent;
        // The names are the API's own, camelCased as every other app sends them.
        Assert.Equal("APP001", sent.GetProperty("appl_No").GetString());
        Assert.Equal("01", sent.GetProperty("holder_Type").GetString());
        Assert.Equal("ABCDE1234F", sent.GetProperty("paN_No").GetString());
        // The date of birth goes as dd/MM/yyyy, the way the API takes it.
        Assert.Equal("01/02/1980", sent.GetProperty("paN_Holder_DOB").GetString());
        Assert.Equal("A HOLDER", sent.GetProperty("paN_Holder_Name").GetString());
        // What the request says of the user comes from the session, not from settings.
        Assert.Equal("TESTAPP01", sent.GetProperty("app_Code").GetString());
        Assert.Equal("AT1", sent.GetProperty("source_Type").GetString());
        Assert.Equal("ST1", sent.GetProperty("source_Sub_Type").GetString());
        Assert.Equal("user-1", sent.GetProperty("createdBy").GetString());
        Assert.Equal("10.0.0.7", sent.GetProperty("createdIP").GetString());
        Assert.Equal(9041, sent.GetProperty("sessionId").GetInt64());
    }

    [Theory]
    [InlineData("1", "1", "1", true, true)]
    [InlineData("1", "1", "0", true, false)]
    // A PAN the API does not hold, or holds against another date of birth: the name is not counted.
    [InlineData("0", "1", "1", false, false)]
    [InlineData("1", "0", "1", false, false)]
    public async Task The_answer_is_a_match_only_where_the_API_says_1(string pan, string dob, string name, bool pairOk, bool nameOk)
    {
        var network = new StubNetwork
        {
            Answer = $$$"""{"PAN_No_Match_Status":"{{{pan}}}","PAN_DOB_Match_Status":"{{{dob}}}","PAN_Name_Match_Status":"{{{name}}}"}""",
        };

        var answer = await ClientOver(network).VerifyAsync(Holder);

        Assert.Equal(pairOk, answer.PairOk);
        Assert.Equal(nameOk, answer.NameOk);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_service_that_is_down_is_an_outage_not_a_mismatch(HttpStatusCode status)
    {
        var network = new StubNetwork { Status = status };

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).VerifyAsync(Holder));
    }

    [Fact]
    public async Task An_answer_with_no_match_status_is_an_outage_said_with_the_APIs_error_message()
    {
        var network = new StubNetwork
        {
            Answer = """{"PAN_No_Match_Status":"","PAN_DOB_Match_Status":null,"PAN_Name_Match_Status":"","ErrorCode":"E101","ErrorMessage":"NSDL service timed out","Status":"Failure"}""",
        };

        var outage = await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).VerifyAsync(Holder));

        Assert.Contains("NSDL service timed out", outage.Message);
    }

    [Fact]
    public async Task The_error_and_status_fields_are_read_as_text_or_as_a_number()
    {
        var network = new StubNetwork
        {
            Answer = """{"PAN_No_Match_Status":"1","PAN_DOB_Match_Status":"1","PAN_Name_Match_Status":"1","ErrorCode":0,"ErrorMessage":"","Status":1}""",
        };

        var answer = await ClientOver(network).VerifyAsync(Holder);

        Assert.True(answer.NameOk);
    }

    [Fact]
    public async Task An_answer_that_cannot_be_read_is_an_outage()
    {
        var network = new StubNetwork { Answer = "<html>gateway error</html>" };

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).VerifyAsync(Holder));
    }
}
