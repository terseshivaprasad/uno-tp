using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// t_FD_BT_Payinslip_Hdr and t_FD_BT_Payinslip_Dtl, the FD system's own tables: a
/// pay-in slip generated on Pay In Slip Generation is one row in each, the slip and
/// the cheque on it, under the same f_Payinslip_No - written as the FD system's own
/// procedure (usp_BT_Save_axis_payinslip_hdr_details_v3_1) writes them, every value
/// copied from the application's staging row. A slip is made once: an application
/// that has one is printed again, and nothing is written.
/// </summary>
internal static class PayInSlipHeaderAndDetail
{
    /// <summary>Whether the application has a slip already.</summary>
    public static async Task<bool> AlreadyMadeAsync(IDbConnection db, IDbTransaction tx, string appNo) =>
        await db.ExecuteScalarAsync<int>(PayInSlipQueries.SlipAlreadyMade, new { AppNo = appNo }, tx) > 0;

    /// <summary>The next slip number, from the FD system's own procedure, asked with the staging row's values.</summary>
    public static async Task<string> NextSlipNoAsync(IDbConnection db, IDbTransaction tx, PayInSlipStagingRow staged)
    {
        var parameters = new DynamicParameters();
        parameters.Add("CmsLocationCode", staged.CmsLocationCode);
        parameters.Add("Ip", staged.CreatedIp);
        parameters.Add("UserName", staged.CreatedByName);
        parameters.Add("SessionId", staged.SessionId);
        parameters.Add("FormCode", staged.FormCode);
        parameters.Add("SlipNo", dbType: DbType.String, direction: ParameterDirection.Output, size: 50);
        await db.ExecuteAsync(PayInSlipQueries.NextSlipNo, parameters, tx);
        var slipNo = parameters.Get<string?>("SlipNo")?.Trim();
        if (string.IsNullOrEmpty(slipNo)) throw new InvalidOperationException("USP_FD_BTP_GetPayinSlipNo gave no slip number.");
        return slipNo;
    }

    /// <summary>The header row, under the slip number; its sequence (FPIH + id), or null when no row was written.</summary>
    public static Task<string?> InsertHeaderAsync(IDbConnection db, IDbTransaction tx, string appNo, string slipNo) =>
        db.ExecuteScalarAsync<string?>(PayInSlipQueries.InsertHeader, new { AppNo = appNo, SlipNo = slipNo }, tx);

    /// <summary>The detail row, under the slip number and the header's sequence.</summary>
    public static Task InsertDetailAsync(IDbConnection db, IDbTransaction tx, string appNo, string slipNo, string hdrSequence) =>
        db.ExecuteAsync(PayInSlipQueries.InsertDetail, new { AppNo = appNo, SlipNo = slipNo, HdrSequence = hdrSequence }, tx);

    /// <summary>The header marked as generated: f_Req_State PRC, f_Req_Status GEN.</summary>
    public static Task MarkGeneratedAsync(IDbConnection db, IDbTransaction tx, string hdrSequence) =>
        db.ExecuteAsync(PayInSlipQueries.MarkHeaderGenerated, new { HdrSequence = hdrSequence }, tx);
}
