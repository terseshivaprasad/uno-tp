using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// t_FD_BT_Payinslip_Hdr and t_FD_BT_Payinslip_Dtl, the FD system's own tables: a
/// pay-in slip generated on Pay In Slip Generation is one row in each, the slip and
/// the cheque on it, under the same f_Payinslip_No. The rows are a
/// PayInSlipHeaderRow and a PayInSlipDetailRow (Models/PayInSlip.cs). A reprint is
/// a new slip: a new row in each, with a fresh number; the newest is the one in force.
/// </summary>
internal static class PayInSlipHeaderAndDetail
{
    /// <summary>The next slip number: the slipNoPrefix setting and a running number of at least four digits.</summary>
    public static Task<string> NextSlipNoAsync(IDbConnection db, IDbTransaction tx, string prefix) =>
        db.QuerySingleAsync<string>(
            PayInSlipQueries.NextSlipNo,
            new { Prefix = prefix }, tx);

    public static async Task InsertAsync(IDbConnection db, IDbTransaction tx, PayInSlipHeaderRow slip, PayInSlipDetailRow cheque)
    {
        await db.ExecuteAsync(PayInSlipQueries.InsertHeader, slip, tx);

        await db.ExecuteAsync(PayInSlipQueries.InsertDetail, cheque, tx);
    }
}
