using Microsoft.Extensions.DependencyInjection;
using UnoTP.Models;

namespace UnoTP.Data;

public static class SqlDataServiceCollectionExtensions
{
    public const string ConnectionName = "UnoTP";

    /// <summary>Whether ConnectionStrings:UnoTP names the database.</summary>
    public static bool Configured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config.GetConnectionString(ConnectionName));

    /// <summary>
    /// The contracts answered from SQL Server (db/): the applications and their
    /// documents, the lists and rules, the purchase journey's masters, the
    /// registers and the console. Sign-in and the menus are the auth API's. The
    /// deposits Renew FD lists have no table: they are the FD system's, and until it
    /// answers no folio is shown as holding any. The app supplies <see cref="IPartner"/>.
    /// </summary>
    public static IServiceCollection AddSqlData(this IServiceCollection services)
    {
        services.AddSingleton<Db>();
        services.AddSingleton<SqlReference>();
        services.AddSingleton<IReferenceApi>(sp => sp.GetRequiredService<SqlReference>());
        services.AddSingleton<SqlMasters>();
        services.AddSingleton<IInvestorApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddSingleton<ISourcingApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddSingleton<IDepositApi>(sp => sp.GetRequiredService<SqlMasters>());
        services.AddSingleton<IPlaceApi>(sp => sp.GetRequiredService<SqlMasters>());
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
        // The deposits a folio holds are the FD system's: none are known until it answers.
        services.AddSingleton<IRenewalApi, NoHeldDeposits>();
        return services;
    }

    private sealed class NoHeldDeposits : IRenewalApi
    {
        private static readonly IReadOnlyList<HeldDeposit> None = [];

        public Task<IReadOnlyList<HeldDeposit>?> DepositsByFolioAsync(string folio, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HeldDeposit>?>(None);

        public Task<IReadOnlyList<HeldDeposit>?> DepositsByPanAsync(string pan, string dob, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HeldDeposit>?>(None);

        public Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default) =>
            Task.FromResult<HeldDeposit?>(null);

        public Task<Application?> StartAsync(string depositNumber, CancellationToken ct = default) =>
            Task.FromResult<Application?>(null);

        public Task<bool> CancelAsync(string depositNumber, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
