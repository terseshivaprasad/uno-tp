using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services.Idfy;
using UnoTP.Services.NameMatch;
using UnoTP.Services.NameScreening;

namespace UnoTP.Services;

public static class BackendServiceCollectionExtensions
{
    /// <summary>
    /// The document checks, each answered by one service: IDfy for identification,
    /// OCR, verification with the issuer, the PAN-Aadhaar link and face match; the
    /// name screening API; and the name match API. A check that is switched off
    /// (Backend:Switches) is not called: its stand-in answers, and the page carries
    /// the check as not asked. The app supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddDocumentChecks(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<OutsideSwitches>();
        var switches = new OutsideSwitches(config);

        // IDfy: one client, and the five checks it answers.
        services.AddApiClient<IdfyClient>(sp => sp.GetRequiredService<IOptions<IdfyOptions>>().Value.BasePath);
        services.Check<IDocumentIdentifier, IdfyDocumentIdentifier, OffDocumentIdentifier>(switches, OutsideSwitches.Identify);
        services.Check<IOcrService, IdfyOcr, OffOcr>(switches, OutsideSwitches.Ocr);
        services.Check<IVerificationService, IdfyVerification, OffVerification>(switches, OutsideSwitches.Verification);
        services.Check<IPanAadhaarLinkService, IdfyPanAadhaarLink, OffPanAadhaarLink>(switches, OutsideSwitches.PanAadhaarLink);
        services.Check<IFaceMatchService, IdfyFaceMatch, OffFaceMatch>(switches, OutsideSwitches.FaceMatch);

        // Name screening and name match: an API of its own each. Screening is
        // switched off by its own setting (NameScreening:ApiCall).
        services.AddApiClient<INameScreeningService, NameScreeningClient>(
            sp => sp.GetRequiredService<IOptions<NameScreeningOptions>>().Value.BasePath);

        if (switches.IsOn(OutsideSwitches.NameMatch))
            services.AddApiClient<INameMatchService, NameMatchClient>(
                sp => sp.GetRequiredService<IOptions<NameMatchOptions>>().Value.BasePath);
        else
            services.AddSingleton<INameMatchService, OffNameMatch>();

        return services;
    }

    // One of IDfy's checks: switched off, its stand-in answers in its place.
    private static void Check<TService, TCheck, TOff>(this IServiceCollection services, OutsideSwitches switches, string name)
        where TService : class
        where TCheck : class, TService
        where TOff : class, TService
    {
        if (switches.IsOn(name)) services.AddTransient<TService, TCheck>();
        else services.AddSingleton<TService, TOff>();
    }
}
