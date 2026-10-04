using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services.Idfy;

namespace UnoTP.Tests;

public class IdfyOcrTests
{
    private static readonly UploadFile Copy = new("passport.jpg", "image/jpeg", [1, 2, 3]);

    private static IdfyOcr OcrOver(StubNetwork network) =>
        new(new IdfyClient(network.Client(), Options.Create(new IdfyOptions()), NullLogger<IdfyClient>.Instance));

    [Fact]
    public async Task A_passports_pin_code_comes_from_its_own_field_and_ends_the_address()
    {
        var network = new StubNetwork
        {
            Answer = """{"status":"completed","task_id":"t","result":{"extraction_output":{"address":"22 PARK STREET, KOLKATA, WEST BENGAL","pincode":"700016","name_on_card":"NEHA DAS","file_number":"CA1234567890123"}}}""",
        };

        var reading = await OcrOver(network).ReadAsync(DocumentKind.ProofOfAddress, "Passport", Copy, new OcrSubject("", "", ""), consent: false);

        Assert.Equal("22 PARK STREET, KOLKATA, WEST BENGAL 700016", reading.Address);
    }

    [Fact]
    public async Task A_pin_code_already_in_the_address_is_not_added_again()
    {
        var network = new StubNetwork
        {
            Answer = """{"status":"completed","task_id":"t","result":{"extraction_output":{"address":"12 MG ROAD, PATNA, BIHAR 800 001","pincode":"800001","name_on_card":"A HOLDER","id_number":"BR0120200012345"}}}""",
        };

        var reading = await OcrOver(network).ReadAsync(DocumentKind.ProofOfAddress, "Driving Licence", Copy, new OcrSubject("", "", ""), consent: false);

        Assert.Equal("12 MG ROAD, PATNA, BIHAR 800 001", reading.Address);
    }

    [Fact]
    public async Task With_no_address_read_a_pin_code_alone_is_not_an_address()
    {
        var network = new StubNetwork
        {
            Answer = """{"status":"completed","task_id":"t","result":{"extraction_output":{"address":"","pincode":"700016","name_on_card":"NEHA DAS"}}}""",
        };

        var reading = await OcrOver(network).ReadAsync(DocumentKind.ProofOfAddress, "Passport", Copy, new OcrSubject("", "", ""), consent: false);

        Assert.Equal("", reading.Address);
    }
}
