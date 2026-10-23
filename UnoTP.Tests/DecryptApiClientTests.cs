using System.Net;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.Auth;
using UnoTP.Services.Decrypt;

namespace UnoTP.Tests;

/// <summary>The decryption API client: the one call the way in makes first.</summary>
public class DecryptApiClientTests
{
    private static DecryptApiClient ClientOver(StubNetwork network) => new(network.Client(), Options.Create(new DecryptApiOptions()));

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

}
