using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Idfy;

/// <summary>Where Idfy.Api is served from, from the "Idfy" section of appsettings.</summary>
public sealed class IdfyOptions
{
    public const string Section = "Idfy";

    public string BaseUrl { get; set; } = "";

    /// <summary>Idfy.Api waits up to about 60 s for IDfy, so the app waits a little longer.</summary>
    public int TimeoutSeconds { get; set; } = 75;

    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config[$"{Section}:{nameof(BaseUrl)}"]);
}

public static class IdfyServiceCollectionExtensions
{
    /// <summary>The key the service each IDfy adapter hands over to is kept under.</summary>
    public const string Fallback = "fallback";

    /// <summary>
    /// Puts IDfy behind document identification, masking, OCR, verification and the
    /// PAN-Aadhaar link. Call it after the backend (or the mock) is added: whatever
    /// answered those before is kept, and answers for the documents IDfy has no
    /// endpoint for. NSDL is not IDfy's, and is left as it is.
    /// </summary>
    public static IServiceCollection AddIdfy(this IServiceCollection services)
    {
        services.AddHttpClient<IdfyClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<IdfyOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.Handover<IDocumentIdentifier, IdfyDocumentIdentifier>();
        services.Handover<IOcrService, IdfyOcr>();
        services.Handover<IVerificationService, IdfyVerification>();
        services.Take<IMaskingService>();
        services.AddTransient<IMaskingService, IdfyMasking>();
        services.Take<IPanAadhaarLinkService>();
        services.AddTransient<IPanAadhaarLinkService, IdfyPanAadhaarLink>();
        return services;
    }

    // The service as registered so far becomes the fallback, and the adapter takes its place.
    private static void Handover<TService, TAdapter>(this IServiceCollection services)
        where TService : class
        where TAdapter : class, TService
    {
        var before = services.Take<TService>();
        services.Add(before.ImplementationType is { } type
            ? new ServiceDescriptor(typeof(TService), Fallback, type, before.Lifetime)
            : before.ImplementationFactory is { } factory
                ? new ServiceDescriptor(typeof(TService), Fallback, (sp, _) => factory(sp), before.Lifetime)
                : new ServiceDescriptor(typeof(TService), Fallback, before.ImplementationInstance!));
        services.AddTransient<TService, TAdapter>();
    }

    private static ServiceDescriptor Take<TService>(this IServiceCollection services)
    {
        var before = services.LastOrDefault(d => d.ServiceType == typeof(TService) && !d.IsKeyedService)
            ?? throw new InvalidOperationException($"Add the backend or the mock before IDfy: nothing answers {typeof(TService).Name} yet.");
        services.Remove(before);
        return before;
    }
}
