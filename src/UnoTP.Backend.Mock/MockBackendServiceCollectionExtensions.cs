using Microsoft.Extensions.DependencyInjection;
using UnoTP.Backend.External;
using UnoTP.Backend.Mock.External;

namespace UnoTP.Backend.Mock;

public static class MockBackendServiceCollectionExtensions
{
    /// <summary>
    /// Every contract, answered in memory from mock data: the backend's own data,
    /// and each outside service by a mock of its own. For running the app with no
    /// backend behind it; the app supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddMockBackend(this IServiceCollection services)
    {
        services.AddSingleton<MockStore>();
        services.AddScoped<MockApplications>();
        services.AddScoped<IApplicationApi>(sp => sp.GetRequiredService<MockApplications>());
        services.AddScoped<IDocumentApi>(sp => sp.GetRequiredService<MockApplications>());
        services.AddSingleton<IInvestorApi, MockInvestors>();
        services.AddSingleton<ISourcingApi, MockSourcing>();
        services.AddSingleton<IPayInSlipApi, MockPayInSlips>();
        services.AddSingleton<ILinkApi, MockLinks>();
        services.AddSingleton<IConsoleApi, MockConsole>();
        services.AddSingleton<IReferenceApi, MockReference>();
        services.AddSingleton<IPartnerApi, MockPartner>();
        services.AddSingleton<IDepositApi, MockDeposits>();
        services.AddSingleton<IDemoApi, MockDemo>();

        // The outside services.
        services.AddSingleton<INsdlService, MockNsdl>();
        services.AddSingleton<IDocumentIdentifier, MockDocumentIdentifier>();
        services.AddSingleton<IMaskingService, MockMasking>();
        services.AddSingleton<IOcrService, MockOcr>();
        services.AddSingleton<IVerificationService, MockVerification>();
        services.AddSingleton<IPanAadhaarLinkService, MockPanAadhaarLink>();
        services.AddSingleton<IFaceMatchService, MockFaceMatch>();
        return services;
    }
}
