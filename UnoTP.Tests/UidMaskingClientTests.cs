using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.UidMasking;

namespace UnoTP.Tests;

public class UidMaskingClientTests
{
    private static readonly byte[] Original = [1, 2, 3, 4];
    private static readonly byte[] Masked = [9, 9, 9, 9];

    private static AadhaarToMask Aadhaar(bool consent) => new(
        "APP001", "", "FORM1", "01", "ABCDE1234F", "01-02-1980",
        new UploadFile("aadhaar.JPG", "image/jpeg", Original), consent);

    private static UidMaskingClient ClientOver(StubNetwork network) =>
        new(network.Client(), new TestPartner(), new TestPartnerApi(), Options.Create(new UidMaskingOptions { MaskPath = "mask" }));

    [Fact]
    public async Task Nothing_is_sent_without_the_holders_consent()
    {
        var network = new StubNetwork();

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MaskAsync(Aadhaar(consent: false)));

        Assert.Equal(0, network.Calls);
    }

    [Fact]
    public async Task The_copy_goes_as_Base64_with_its_type_and_the_masking_settings()
    {
        var network = new StubNetwork { Answer = $$$"""{"IntStatusCode":200,"Result":{"FileData":"{{{Convert.ToBase64String(Masked)}}}","AadhaarSuffix":"1234"}}""" };

        await ClientOver(network).MaskAsync(Aadhaar(consent: true));

        Assert.Equal("http://gateway.test/some-api/api/v1/mask", network.Address!.ToString());
        var sent = network.Sent;
        Assert.Equal("jpg", sent.GetProperty("fileType").GetString());
        Assert.Equal(Convert.ToBase64String(Original), sent.GetProperty("fileData").GetString());
        Assert.Equal(8, sent.GetProperty("maskLength").GetInt32());
        Assert.Equal("APP001", sent.GetProperty("applNo").GetString());
        Assert.Equal("01", sent.GetProperty("holderType").GetString());
        Assert.Equal("9041", sent.GetProperty("sessionID").GetString());
    }

    [Fact]
    public async Task The_masked_copy_comes_back_under_the_same_name_with_the_last_four_digits()
    {
        var network = new StubNetwork { Answer = $$$"""{"IntStatusCode":200,"Result":{"FileData":"{{{Convert.ToBase64String(Masked)}}}","AadhaarSuffix":"1234"}}""" };

        var masked = await ClientOver(network).MaskAsync(Aadhaar(consent: true));

        Assert.Equal(Masked, masked.Copy.Bytes);
        Assert.Equal("aadhaar.JPG", masked.Copy.FileName);
        Assert.Equal("1234", masked.Suffix);
    }

    [Theory]
    [InlineData("200")]
    [InlineData("\"200\"")]
    public async Task The_answer_is_read_whether_its_status_code_is_a_number_or_text(string statusCode)
    {
        var network = new StubNetwork { Answer = $$$"""{"IntStatusCode":{{{statusCode}}},"Status":"OK","Result":{"FileData":"{{{Convert.ToBase64String(Masked)}}}","AadhaarSuffix":"1234"}}""" };

        var masked = await ClientOver(network).MaskAsync(Aadhaar(consent: true));

        Assert.Equal("1234", masked.Suffix);
    }

    [Fact]
    public async Task An_answer_with_no_masked_copy_is_an_outage_so_nothing_unmasked_is_filed()
    {
        var network = new StubNetwork { Answer = """{"IntStatusCode":500,"Error":"could not mask"}""" };

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MaskAsync(Aadhaar(consent: true)));
    }
}
