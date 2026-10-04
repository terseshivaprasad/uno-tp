using System.Threading.Channels;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UnoTP.Data;

/// <summary>
/// Errors to dbo.t_Unotp_Logs (db/create_new_tables.sql): every entry an ILogger writes at
/// Logging:Sql:MinLevel (Error) or above, with the request it happened in and who
/// was signed in. The logger only queues the row; <see cref="SqlLogWriter"/> writes
/// the queue in the background, so a request never waits on the log, and a full
/// queue drops the newest rather than holding anything up.
/// </summary>
public static class SqlErrorLog
{
    /// <summary>The application every row is written under.</summary>
    public const string AppName = "UnoTP";

    /// <summary>HttpContext.Items key the app sets to the signed-in partner's user id.</summary>
    public const string UserItem = "log.user";

    public static IServiceCollection AddSqlErrorLog(this IServiceCollection services)
    {
        services.AddSingleton(Channel.CreateBounded<LogRow>(new BoundedChannelOptions(5_000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        }));
        services.AddSingleton<ILoggerProvider, SqlLoggerProvider>();
        services.AddHostedService<SqlLogWriter>();
        return services;
    }
}

/// <summary>One row of dbo.t_Unotp_Logs, as it is queued.</summary>
public sealed record LogRow(
    string AppName, string Environment, string Level, string Category, int EventId, string Message, string? Exception,
    string? TraceId, string? RequestMethod, string? RequestPath, string? AppNo, string? ClientIp, string MachineName,
    DateTime LoggedAt, string CreatedBy);

[ProviderAlias("Sql")]
internal sealed class SqlLoggerProvider(Channel<LogRow> queue, IHttpContextAccessor http, IConfiguration config, IHostEnvironment env) : ILoggerProvider
{
    private readonly LogLevel min = Enum.TryParse<LogLevel>(config["Logging:Sql:MinLevel"], out var level) ? level : LogLevel.Error;
    private readonly Channel<LogRow> rows = queue;
    private readonly IHttpContextAccessor context = http;
    private readonly string environment = env.EnvironmentName;

    public ILogger CreateLogger(string categoryName) => new SqlLogger(this, categoryName);

    public void Dispose() { }

    private sealed class SqlLogger(SqlLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // The writer's own failures are never written back through it.
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= owner.min && logLevel != LogLevel.None && !category.StartsWith(typeof(SqlLogWriter).FullName!, StringComparison.Ordinal);

        /// <summary>Queues one log entry as a t_Unotp_Logs row, with the request it happened in.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var ctx = owner.context.HttpContext;
            var request = ctx?.Request;
            var message = formatter(state, exception);
            owner.rows.Writer.TryWrite(new LogRow(
                SqlErrorLog.AppName, owner.environment, logLevel.ToString(), Truncate(category, 200)!, eventId.Id,
                Truncate(message.Length > 0 ? message : exception?.Message ?? "", 4000)!, exception?.ToString(),
                Truncate(ctx?.TraceIdentifier, 64), request?.Method, Truncate(request is null ? null : (request.PathBase + request.Path).Value, 400),
                Truncate(ctx?.GetRouteValue("appNo") as string, 20), Truncate(ctx?.Connection.RemoteIpAddress?.ToString(), 45),
                Truncate(Environment.MachineName, 100)!, DateTime.Now,
                Truncate(ctx?.Items[SqlErrorLog.UserItem] as string, 50) ?? "system"));
        }

        /// <summary>The value cut to the column's length.</summary>
        private static string? Truncate(string? value, int length) => value is null || value.Length <= length ? value : value[..length];
    }
}

/// <summary>
/// Writes the queued rows to dbo.t_Unotp_Logs, a batch at a time, and what is left when the
/// app stops. A row it cannot write goes to the console instead: never back through
/// ILogger, which would queue it again.
/// </summary>
internal sealed class SqlLogWriter(Channel<LogRow> queue, Db db) : BackgroundService
{
    private const string Insert = """
        INSERT INTO dbo.t_Unotp_Logs (c_App_Name, c_Environment, c_Level, c_Category, n_Event_Id, c_Message, c_Exception,
            c_Trace_Id, c_Request_Method, c_Request_Path, c_App_No, c_Client_Ip, c_Machine_Name, d_Logged_At, c_Created_By)
        VALUES (@AppName, @Environment, @Level, @Category, @EventId, @Message, @Exception,
            @TraceId, @RequestMethod, @RequestPath, @AppNo, @ClientIp, @MachineName, @LoggedAt, @CreatedBy)
        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<LogRow>(50);
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < 50 && queue.Reader.TryRead(out var row)) batch.Add(row);
                await WriteAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        // Stopping: what is still queued is written before the app goes.
        while (queue.Reader.TryRead(out var row)) batch.Add(row);
        await WriteAsync(batch, CancellationToken.None);
    }

    private async Task WriteAsync(List<LogRow> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(Insert, batch, cancellationToken: ct));
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync($"t_Unotp_Logs: {batch.Count} error row(s) not written: {e.Message}");
            foreach (var row in batch) await Console.Error.WriteLineAsync($"  {row.LoggedAt:o} {row.Level} {row.Category}: {row.Message}");
        }
        batch.Clear();
    }
}
