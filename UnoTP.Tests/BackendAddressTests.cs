using Microsoft.Extensions.Configuration;
using UnoTP.Services;
using UnoTP.Services.Ckyc;

namespace UnoTP.Tests;

public class BackendAddressTests
{
    private const string Gateway = "https://gateway.example/";

    private static IConfiguration ConfigOf(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)).Build();

    [Fact]
    public void An_api_with_no_address_of_its_own_is_called_at_the_gateway()
    {
        var api = new CkycOptions { BasePath = "ckyc-api/api/v1" };

        Assert.Equal("https://gateway.example/ckyc-api/api/v1/", BackendHttpClients.AddressOf(api, Gateway));
    }

    [Fact]
    public void An_api_with_its_own_address_is_called_there_and_not_at_the_gateway()
    {
        var api = new CkycOptions { BaseUrl = "https://ckyc.example", BasePath = "/api/v1/" };

        Assert.Equal("https://ckyc.example/api/v1/", BackendHttpClients.AddressOf(api, Gateway));
    }

    [Fact]
    public void An_api_with_no_base_path_is_called_at_the_address_alone()
    {
        var api = new CkycOptions { BaseUrl = "https://ckyc.example/" };

        Assert.Equal("https://ckyc.example/", BackendHttpClients.AddressOf(api, Gateway));
    }

    [Fact]
    public void With_the_gateway_set_no_api_is_without_an_address()
    {
        var config = ConfigOf(("Backend:BaseUrl", Gateway));

        Assert.Empty(BackendHttpClients.Unaddressed(config, "Ckyc", "PanApi"));
    }

    [Fact]
    public void Without_the_gateway_only_an_api_with_its_own_address_has_one()
    {
        var config = ConfigOf(("Backend:BaseUrl", "https://<gateway-host>/"), ("Ckyc:BaseUrl", "https://ckyc.example/"));

        Assert.Equal(["PanApi"], BackendHttpClients.Unaddressed(config, "Ckyc", "PanApi"));
    }

    [Fact]
    public void An_address_that_is_not_a_web_address_does_not_count()
    {
        var config = ConfigOf(("Backend:BaseUrl", Gateway), ("Ckyc:BaseUrl", "ckyc.example"));

        Assert.Equal(["Ckyc"], BackendHttpClients.Unaddressed(config, "Ckyc", "PanApi"));
    }
}
