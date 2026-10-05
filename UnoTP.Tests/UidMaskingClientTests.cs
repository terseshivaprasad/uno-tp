using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Services.UidMasking;

namespace UnoTP.Tests;

public class UidMaskingClientTests
{
    private static readonly byte[] Original = [1, 2, 3, 4];
    private static readonly byte[] Masked = [9, 9, 9, 9];

    private static AadhaarToMask Aadhaar(bool consent) =>
        new("APP001", "01", new UploadFile("aadhaar.JPG", "image/jpeg", Original), consent);

    private static UidMaskingClient ClientOver(StubNetwork network) =>
        new(network.Client(), new TestPartner(), Options.Create(new UidMaskingOptions { MaskPath = "mask" }));

    // The API's own answer, with the copy it sends back.
    private static string Success(byte[] copy) =>
        """
        {"IntStatusCode":"MF-SYS-200","IntStatusDesc":"Success with masking","Status":"Success",
         "Result":{"FileData":"COPY","AadhaarSuffix":null,"total_pages":"1","processed_pages":"1","attempted_pages":"1","succeeded_pages":"1","status":"succeeded"},
         "Error":""}
        """.Replace("COPY", Convert.ToBase64String(copy));

    [Fact]
    public async Task Nothing_is_sent_without_the_holders_consent()
    {
        var network = new StubNetwork();

        await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MaskAsync(Aadhaar(consent: false)));

        Assert.Equal(0, network.Calls);
    }

    [Fact]
    public async Task The_request_is_the_apis_own_six_fields_under_its_own_names()
    {
        var network = new StubNetwork { Answer = Success(Masked) };

        await ClientOver(network).MaskAsync(Aadhaar(consent: true));

        Assert.Equal("http://gateway.test/some-api/api/v1/mask", network.Address!.ToString());
        var sent = network.Sent;
        Assert.Equal(["MaskLength", "Trans_Ref_No", "Source", "CreatedIP", "FileType", "FileData"], sent.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("8", sent.GetProperty("MaskLength").GetString());
        Assert.Equal("APP001_01", sent.GetProperty("Trans_Ref_No").GetString());
        Assert.Equal("UNO_TP", sent.GetProperty("Source").GetString());
        Assert.Equal("10.0.0.7", sent.GetProperty("CreatedIP").GetString());
        Assert.Equal("IMAGE", sent.GetProperty("FileType").GetString());
        Assert.Equal(Convert.ToBase64String(Original), sent.GetProperty("FileData").GetString());
    }

    [Fact]
    public async Task The_masked_copy_comes_back_under_the_same_name_though_no_suffix_does()
    {
        var network = new StubNetwork { Answer = Success(Masked) };

        var masked = await ClientOver(network).MaskAsync(Aadhaar(consent: true));

        Assert.Equal(Masked, masked.Copy.Bytes);
        Assert.Equal("aadhaar.JPG", masked.Copy.FileName);
        Assert.Equal("", masked.Suffix);
    }

    [Fact]
    public async Task A_status_other_than_success_is_masking_failing_said_in_the_apis_words()
    {
        var network = new StubNetwork
        {
            Answer = """{"IntStatusCode":"MF-SYS-500","IntStatusDesc":"Aadhaar not found on the image","Status":"Failure","Result":{"FileData":"AQIDBA=="},"Error":""}""",
        };

        var refused = await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MaskAsync(Aadhaar(consent: true)));

        Assert.Contains("Aadhaar not found on the image", refused.Message);
    }

    [Fact]
    public async Task An_answer_with_no_masked_copy_is_masking_failing_so_nothing_unmasked_is_filed()
    {
        var network = new StubNetwork { Answer = """{"IntStatusCode":"MF-SYS-200","Status":"Success","Result":{"FileData":""},"Error":"could not mask"}""" };

        var refused = await Assert.ThrowsAsync<ExternalServiceException>(() => ClientOver(network).MaskAsync(Aadhaar(consent: true)));

        Assert.Contains("could not mask", refused.Message);
    }
}
