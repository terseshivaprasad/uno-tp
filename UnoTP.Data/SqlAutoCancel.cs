using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UnoTP.Data;

/// <summary>
/// An unpaid application cancels itself cancellationDays after it was created:
/// once an hour, every application older than that, not paid, not booked and not
/// cancelled already, is marked cancelled. The lists then show it as cancelled and
/// no link can be sent for it.
/// </summary>
public sealed class SqlAutoCancel(Db db, SqlReference reference, ILogger<SqlAutoCancel> log) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CancelLapsedAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Auto-cancel: the pass failed; tried again in an hour.");
            }
            try
            {
                await Task.Delay(Every, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Marks every lapsed application cancelled, and says how many.</summary>
    public async Task<int> CancelLapsedAsync(CancellationToken ct)
    {
        var days = (await reference.ConfigAsync(ct)).CancellationDays;
        await using var connection = await db.OpenAsync(ct);
        var cancelled = await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Application_Mst
            SET d_Cancelled_On = SYSDATETIME(), c_Sub_Status = 'cancelled', c_Updated_By = 'AUTO', d_Updated_On = SYSDATETIME()
            WHERE f_Active = 1 AND d_Cancelled_On IS NULL AND d_Paid_On IS NULL AND d_Booked_On IS NULL
              AND d_Created_On < DATEADD(DAY, -@Days, SYSDATETIME())
            """, new { Days = days });
        if (cancelled > 0) log.LogInformation("Auto-cancel: {Count} application(s) older than {Days} days cancelled.", cancelled, days);
        return cancelled;
    }
}
