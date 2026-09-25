using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnoTP.Backend.External;

namespace UnoTP.Backend;

public static class BackendServiceCollectionExtensions
{
    /// <summary>
    /// Every contract, answered over HTTP: the backend's own data by
    /// <see cref="BackendClient"/> at Backend:BaseUrl, and each outside service by
    /// its own client at its own address. The app supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddBackendApi(this IServiceCollection services)
    {
        services.AddHttpClient<BackendClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<BackendOptions>>().Value;
            // Routes are relative, so the base keeps its own path only with a closing slash.
            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddTransient<IInvestorApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IApplicationApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IDocumentApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<ISourcingApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IPayInSlipApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<ILinkApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IConsoleApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IReferenceApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IPartnerApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IDepositApi>(sp => sp.GetRequiredService<BackendClient>());
        services.AddTransient<IDemoApi>(sp => sp.GetRequiredService<BackendClient>());

        // The outside services, one client each.
        services.External<INsdlService, NsdlClient>(NsdlClient.Name);
        services.External<IDocumentIdentifier, DocumentIdentifierClient>(DocumentIdentifierClient.Name);
        services.External<IMaskingService, MaskingClient>(MaskingClient.Name);
        services.External<IOcrService, OcrClient>(OcrClient.Name);
        services.External<IVerificationService, VerificationClient>(VerificationClient.Name);
        services.External<IPanAadhaarLinkService, PanAadhaarLinkClient>(PanAadhaarLinkClient.Name);
        services.External<IFaceMatchService, FaceMatchClient>(FaceMatchClient.Name);
        return services;
    }

    private static void External<TService, TClient>(this IServiceCollection services, string name)
        where TService : class
        where TClient : class, TService =>
        services.AddHttpClient<TService, TClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<BackendOptions>>().Value;
            http.BaseAddress = options.UrlOf(name);
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
}
