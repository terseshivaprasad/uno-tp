using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// t_FD_CMN_AML_Source_Of_Funds_Log, which is kept with the masters, so it is written
/// on the masters connection once the deposit's save has committed: the source of funds the investor gave, with
/// the amount, annual income and occupation it was asked for. A save of FD
/// Configuration takes the application's earlier entry out of use and, where a
/// source is given, writes it afresh; nothing is deleted.
/// </summary>
internal static class SourceOfFundsLog
{
    public static async Task WriteAsync(Db db, Stamp at, Application app, CancellationToken ct)
    {
        await using var connection = await db.OpenMastersAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE dbo.t_FD_CMN_AML_Source_Of_Funds_Log SET f_Active = 0 WHERE f_Appl_No = @AppNo AND f_Active = 1",
            new { at.AppNo });

        var deposit = app.Deposit;
        if (deposit is null || deposit.SourceOfFunds.Length == 0) return;

        var investor = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
        await connection.ExecuteAsync("""
            INSERT dbo.t_FD_CMN_AML_Source_Of_Funds_Log (f_Appl_No, f_Holder_Type, f_Investment_Amt, f_AnnualIncome_Desc,
                f_AML_Source_Of_Funds, f_AML_Source_Of_Funds_Remarks, f_AML_Source_Of_Funds_reason,
                f_FormCode, f_Source, f_Folio_No, f_Occupation_Desc,
                f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
            VALUES (@AppNo, @HolderType, @Amount, @AnnualIncome,
                @SourceOfFunds, @SourceOfFundsRemark, @SourceOfFundsReason,
                @FormNo, 'UNO_TP', @Folio, @Occupation,
                1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            at.AppNo, HolderType = HolderType.Investor, deposit.Amount, investor?.AnnualIncome,
            deposit.SourceOfFunds, deposit.SourceOfFundsRemark, deposit.SourceOfFundsReason,
            app.Upload?.FormNo, app.Holder.Folio, investor?.Occupation,
            CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        });
    }
}
