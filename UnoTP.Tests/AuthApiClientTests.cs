using System.Net;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.Auth;

namespace UnoTP.Tests;

public class AuthApiClientTests
{
    private static readonly SessionStart Start = new(
        "user-1", "TESTAPP01", "10.0.0.1", "host.test", "10.0.0.7", "020000000000", "Chrome", "120.0", "120", "0", "agent");

    private static AuthApiClient ClientOver(StubNetwork network) => new(network.Client(), Options.Create(new AuthApiOptions()));

    [Fact]
    public async Task Decrypt_sends_the_cipher_text_and_returns_what_it_stands_for()
    {
        var network = new StubNetwork { Answer = """{"success":true,"message":"ok","data":"user-1"}""" };

        var plain = await ClientOver(network).DecryptAsync("CIPHER");

        Assert.Equal("user-1", plain);
        Assert.Equal("http://gateway.test/some-api/api/v1/cipher/decrypt", network.Address!.ToString());
        Assert.Equal("CIPHER", network.Sent.GetProperty("Text").GetString());
    }

    [Fact]
    public async Task A_cipher_text_the_API_turns_down_decrypts_to_nothing()
    {
        var network = new StubNetwork { Status = HttpStatusCode.BadRequest, Answer = """{"success":false}""" };

        Assert.Null(await ClientOver(network).DecryptAsync("CIPHER"));
    }

    [Fact]
    public async Task A_session_started_carries_the_APIs_session_id_and_who_the_user_is()
    {
        var network = new StubNetwork
        {
            Status = HttpStatusCode.Created,
            Answer = """
                {"success":true,"data":{"pk_Agency_Usr_Mst_ID":2,"agency_Cd":"AG1","agency_Type":"AT1","agency_Sub_Type":"ST1",
                 "agency_Usr_Name":"usr_test","entity_Id":"E100","entity_Name":"Test Agency","agency_Usr_EmailID":null,
                 "busi_Broker_Cd":"BRK1","pk_Session_Ref_ID":"ref-1","pk_Session_ID":9041}}
                """,
        };

        var session = await ClientOver(network).StartAsync(Start);

        Assert.NotNull(session);
        Assert.Equal("9041", session.SessionId);
        Assert.Equal("user-1", session.UserId);
        Assert.Equal(new PartnerProfile("Test Agency", "E100", "AT1", "BRK1", "ST1", "AG1", "usr_test", "TESTAPP01"), session.Partner);
        // The API's own model is kept whole.
        Assert.Equal(2, session.User.Pk_Agency_Usr_Mst_ID);
        Assert.Equal("ref-1", session.User.Pk_Session_Ref_ID);
        Assert.Equal(9041, session.User.Pk_Session_ID);

        Assert.Equal("http://gateway.test/some-api/api/v1/auth/sessions", network.Address!.ToString());
        Assert.Equal("user-1", network.Sent.GetProperty("userId").GetString());
        Assert.Equal("TESTAPP01", network.Sent.GetProperty("sysCode").GetString());
        Assert.Equal("10.0.0.7", network.Sent.GetProperty("ipAddress").GetString());
    }

    [Fact]
    public async Task A_user_the_API_refuses_gets_no_session()
    {
        var network = new StubNetwork { Status = HttpStatusCode.Unauthorized, Answer = """{"success":false,"message":"Unknown user."}""" };

        Assert.Null(await ClientOver(network).StartAsync(Start));
    }

    [Fact]
    public async Task An_API_that_is_down_is_an_outage()
    {
        var network = new StubNetwork { Status = HttpStatusCode.BadGateway };

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).StartAsync(Start));
    }

    [Fact]
    public async Task The_menu_is_asked_for_by_user_and_system_and_a_row_with_no_page_is_left_out()
    {
        var network = new StubNetwork
        {
            Answer = """{"success":true,"data":[{"SubModName":"Create New FD","PageName":" SearchInvestor "},{"SubModName":"No page","PageName":""}]}""",
        };

        var menu = await ClientOver(network).MenuAsync("user-1", "TESTAPP01");

        Assert.Equal(HttpMethod.Get, network.Method);
        Assert.Equal("http://gateway.test/some-api/api/v1/app-menus/user-1/TESTAPP01", network.Address!.ToString());
        Assert.Equal([new MenuItem("SearchInvestor", "Create New FD")], menu);
    }
}
