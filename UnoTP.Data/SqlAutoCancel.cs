using Dapper;
using Microsoft.Extensions.Hosting;

namespace UnoTP.Data;

/// <summary>
/// An unpaid application cancels itself cancellationDays after it was created:
/// once an hour, every application older than that, not paid, not booked and not
/// cancelled already, is marked cancelled. The lists then show it as cancelled and
/// no link can be sent for it. A renewal still being entered is cancelled sooner:
/// when its deposit's renewal window closes.
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
        return cancelled + await CancelRenewalDraftsAsync(connection, ct);
    }

    // A renewal still being entered when its deposit's renewal window closes -
    // renewUntilDays before maturity, earlier for a deposit tagged for auto renewal -
    // is cancelled: from then the renewal is Operations'. The tag is on the deposit's
    // own application, where it was booked through this app.
    private async Task<int> CancelRenewalDraftsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var config = await reference.ConfigAsync(ct);
        var cancelled = await connection.ExecuteAsync("""
            UPDATE m
            SET d_Cancelled_On = SYSDATETIME(), c_Sub_Status = 'cancelled', c_Updated_By = 'AUTO', d_Updated_On = SYSDATETIME()
            FROM dbo.t_Unotp_Application_Mst m
            OUTER APPLY (
                SELECT TOP (1) i.f_Is_Auto_Renewal AS AutoRenewal
                FROM dbo.t_Unotp_Application_Mst o
                JOIN dbo.t_FD_BT_Investment_Dtl i ON i.f_Appl_No = o.c_App_No AND i.f_Active = 1
                WHERE o.c_Fdr_No = m.c_Renew_Dep_No AND o.f_Active = 1) held
            WHERE m.f_Active = 1 AND m.d_Cancelled_On IS NULL AND m.d_Submitted_On IS NULL
              AND m.c_Renew_Dep_No IS NOT NULL AND m.d_Renew_Matures_On IS NOT NULL
              AND m.d_Renew_Matures_On < DATEADD(DAY, CASE WHEN ISNULL(held.AutoRenewal, 0) = 1 THEN @UntilAutoRenewal ELSE @Until END, @Today)
            """, new { Until = config.RenewUntilDays, UntilAutoRenewal = config.RenewUntilDaysAutoRenewal, DateTime.Today });
        if (cancelled > 0) log.LogInformation("Auto-cancel: {Count} renewal draft(s) past the renewal window cancelled.", cancelled);
        return cancelled;
    }
}
