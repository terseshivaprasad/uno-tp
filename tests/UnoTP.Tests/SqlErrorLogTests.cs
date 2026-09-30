using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnoTP.Data;

namespace UnoTP.Tests;

/// <summary>
/// Errors reach dbo.t_Unotp_Logs, with the application's name and the audit fields.
/// Needs a database: set UNOTP_TEST_SQL to its connection string (db/006 applied).
/// Without it the test has nothing to write to and passes without running.
/// </summary>
public class SqlErrorLogTests
{
    [Fact]
    public async Task An_error_is_written_to_t_Unotp_Logs()
    {
        var cs = Environment.GetEnvironmentVariable("UNOTP_TEST_SQL");
        if (string.IsNullOrWhiteSpace(cs)) return;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:UnoTP"] = cs }).Build());
        services.AddSingleton<IHostEnvironment>(new Env());
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddLogging();
        services.AddSingleton<Db>();
        services.AddSqlErrorLog();
        await using var provider = services.BuildServiceProvider();
        var writers = provider.GetServices<IHostedService>().ToList();
        foreach (var w in writers) await w.StartAsync(default);

        var probe = Guid.NewGuid().ToString("n");
        provider.GetRequiredService<ILoggerFactory>().CreateLogger("UnoTP.Tests.Probe")
            .LogError(new InvalidOperationException("probe failure"), "Probe {Probe}", probe);
        provider.GetRequiredService<ILoggerFactory>().CreateLogger("UnoTP.Tests.Probe")
            .LogWarning("Below the error level {Probe}", probe);
        // Stopping writes whatever is still queued.
        foreach (var w in writers) await w.StopAsync(default);

        await using var connection = new SqlConnection(cs);
        var rows = (await connection.QueryAsync<(string App, string Level, string Category, string Message, string Exception, string CreatedBy, DateTime CreatedOn, bool Active)>(
            """
            SELECT c_App_Name, c_Level, c_Category, c_Message, c_Exception, c_Created_By, d_Created_On, f_Active
            FROM dbo.t_Unotp_Logs WHERE c_Message LIKE '%' + @probe + '%'
            """, new { probe })).ToList();
        await connection.ExecuteAsync("DELETE FROM dbo.t_Unotp_Logs WHERE c_Message LIKE '%' + @probe + '%'", new { probe });

        var row = Assert.Single(rows);
        Assert.Equal("UnoTP", row.App);
        Assert.Equal("Error", row.Level);
        Assert.Equal("UnoTP.Tests.Probe", row.Category);
        Assert.Contains("probe failure", row.Exception);
        Assert.Equal("system", row.CreatedBy);
        Assert.True(row.Active);
        Assert.True((DateTime.Now - row.CreatedOn).Duration() < TimeSpan.FromHours(14));
    }

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "UnoTP";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
