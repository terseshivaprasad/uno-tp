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
        PanAadhaarLinkClient.Name, FaceMatchClient.Name, DecryptionClient.Name,
    ];

    /// <summary>
    /// Each outside service that has an address (Backend:External:{name}) answered by
    /// its own HTTP client, in place of whatever answered for it before. The app
    /// supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddOutsideServices(this IServiceCollection services, IConfiguration config)
    {
        services.External<INsdlService, NsdlClient>(config, NsdlClient.Name);
        services.External<IDocumentIdentifier, DocumentIdentifierClient>(config, DocumentIdentifierClient.Name);
        services.External<IMaskingService, MaskingClient>(config, MaskingClient.Name);
        services.External<IOcrService, OcrClient>(config, OcrClient.Name);
        services.External<IVerificationService, VerificationClient>(config, VerificationClient.Name);
        services.External<IPanAadhaarLinkService, PanAadhaarLinkClient>(config, PanAadhaarLinkClient.Name);
        services.External<IFaceMatchService, FaceMatchClient>(config, FaceMatchClient.Name);
        services.External<IDecryptionService, DecryptionClient>(config, DecryptionClient.Name);
        return services;
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
