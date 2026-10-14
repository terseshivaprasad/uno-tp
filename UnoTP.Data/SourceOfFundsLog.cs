using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// t_FD_CMN_AML_Source_Of_Funds_Log, written once the deposit's save has committed:
/// the source of funds the investor gave, with
/// the amount, annual income and occupation it was asked for. A save of FD
/// Configuration takes the application's earlier entry out of use and, where a
/// source is given, writes it afresh; nothing is deleted.
/// </summary>
internal static class SourceOfFundsLog
{
    public static async Task WriteAsync(Db db, Stamp at, Application app, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await connection.ExecuteAsync(
            FdConfigurationQueries.RetireSourceOfFunds,
            new { at.AppNo });

        var deposit = app.Deposit;
        if (deposit is null || deposit.SourceOfFunds.Length == 0) return;

        var investor = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
        await connection.ExecuteAsync(FdConfigurationQueries.InsertSourceOfFunds, new
        {
            at.AppNo, HolderType = HolderType.Investor, deposit.Amount, investor?.AnnualIncome,
            deposit.SourceOfFunds, deposit.SourceOfFundsRemark, deposit.SourceOfFundsReason,
            app.Upload?.FormNo, app.Holder.Folio, investor?.Occupation,
            CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        });
    }
}
