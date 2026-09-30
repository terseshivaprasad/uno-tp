using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnoTP.Backend;
using UnoTP.Backend.Mock;

namespace UnoTP.Data;

public static class SqlDataServiceCollectionExtensions
{
    public const string ConnectionName = "UnoTP";

    /// <summary>Whether ConnectionStrings:UnoTP names the database.</summary>
    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config.GetConnectionString(ConnectionName));

    /// <summary>
    /// Every contract the pages read, answered from SQL Server (db/): the
    /// applications and their documents, sign-in and menus, the lists and rules,
    /// the purchase journey's masters, the registers and the console. What has no
    /// table yet - the deposits Renew FD lists - is left to the mock, which is
    /// registered first and replaced here. The app supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddSqlData(this IServiceCollection services)
    {
        services.AddMockBackend();

        services.AddSingleton<Db>();
        services.AddSingleton<SqlReference>();
        services.AddSingleton<IReferenceApi>(sp => sp.GetRequiredService<SqlReference>());
        // The register of the folio's earlier deposits (nominees, repayment accounts) is the mock's until the FD system answers.
        services.AddSingleton<UnoTP.Backend.Mock.MockInvestors>();
        services.AddSingleton<SqlMasters>();
        services.AddSingleton<IInvestorApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddSingleton<ISourcingApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddSingleton<IDepositApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddSingleton<IPlaceApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddScoped<SqlPartners>();
        services.AddScoped<IPartnerApi>(sp => sp.GetRequiredService<SqlPartners>());
        services.AddScoped<ISessionApi>(sp => sp.GetRequiredService<SqlPartners>());
        services.AddScoped<IConsoleApi, SqlConsole>();
        services.AddScoped<IPayInSlipApi, SqlPayInSlips>();
        services.AddScoped<ILinkApi, SqlLinks>();
        services.AddScoped<SqlApplications>();
        services.AddScoped<IApplicationApi>(sp => sp.GetRequiredService<SqlApplications>());
        services.AddScoped<IRenewalOpener>(sp => sp.GetRequiredService<SqlApplications>());
        services.AddScoped<IDocumentApi, FileDocuments>();
        // Errors, with the request and the partner, to dbo.t_Unotp_Logs.
        services.AddSqlErrorLog();
        // An unpaid application cancels itself cancellationDays after it was created.
        services.AddHostedService<SqlAutoCancel>();
        // A live database has no test data to show: the Test data cards are left out.
        services.AddSingleton<IDemoApi, NoDemoData>();
        return services;
    }

    private sealed class NoDemoData : IDemoApi
    {
        public Task<DemoCases?> CasesAsync(CancellationToken ct = default) => Task.FromResult<DemoCases?>(null);

        public Task<DemoBanks?> BanksAsync(CancellationToken ct = default) => Task.FromResult<DemoBanks?>(null);
    }
}
