using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnoTP.Backend.External;

namespace UnoTP.Backend;

public static class BackendServiceCollectionExtensions
{
    /// <summary>Every outside service by the name its address goes under (Backend:External:{name}).</summary>
    public static readonly string[] OutsideServices =
    [
        NsdlClient.Name, DocumentIdentifierClient.Name, MaskingClient.Name, OcrClient.Name, VerificationClient.Name,
        PanAadhaarLinkClient.Name, FaceMatchClient.Name, DecryptionClient.Name, NameScreeningClient.Name, NameMatchClient.Name,
    ];

    /// <summary>
    /// Each outside service that has an address (Backend:External:{name}) answered by
    /// its own HTTP client, in place of whatever answered for it before. The app
    /// supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddOutsideServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<OutsideSwitches>();
        var switches = new OutsideSwitches(config);

        // Always on: a PAN is always verified, an Aadhaar always masked, the portal's values always decrypted.
        services.External<INsdlService, NsdlClient>(config, NsdlClient.Name);
        services.External<IMaskingService, MaskingClient>(config, MaskingClient.Name);
        services.External<IDecryptionService, DecryptionClient>(config, DecryptionClient.Name);

        // Switchable: off, the service is not called, and its check is carried as not asked.
        services.External<IDocumentIdentifier, DocumentIdentifierClient, OffDocumentIdentifier>(config, switches, DocumentIdentifierClient.Name);
        services.External<IOcrService, OcrClient, OffOcr>(config, switches, OcrClient.Name);
        services.External<IVerificationService, VerificationClient, OffVerification>(config, switches, VerificationClient.Name);
        services.External<IPanAadhaarLinkService, PanAadhaarLinkClient, OffPanAadhaarLink>(config, switches, PanAadhaarLinkClient.Name);
        services.External<IFaceMatchService, FaceMatchClient, OffFaceMatch>(config, switches, FaceMatchClient.Name);
        services.External<INameScreeningService, NameScreeningClient, OffNameScreening>(config, switches, NameScreeningClient.Name);
        services.External<INameMatchService, NameMatchClient, OffNameMatch>(config, switches, NameMatchClient.Name);
        return services;
    }

    // A service with a switch: off, its stand-in answers in place of whatever answered before.
    private static void External<TService, TClient, TOff>(this IServiceCollection services, IConfiguration config, OutsideSwitches switches, string name)
        where TService : class
        where TClient : class, TService
        where TOff : class, TService
    {
        if (!switches.IsOn(name))
        {
            services.AddSingleton<TService, TOff>();
            return;
        }
        services.External<TService, TClient>(config, name);
    }

    private static void External<TService, TClient>(this IServiceCollection services, IConfiguration config, string name)
        where TService : class
        where TClient : class, TService
    {
        if (!BackendOptions.HasAddress(config, name)) return;
        services.AddHttpClient<TService, TClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<BackendOptions>>().Value;
            http.BaseAddress = options.UrlOf(name);
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
    }
}
