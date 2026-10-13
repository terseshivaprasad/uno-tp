using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// t_FD_BT_Payinslip_Staging_Hdr, the FD system's own table: an application paid by
/// cheque is put in it on its final submit, once it stands as APR, to wait for its
/// pay-in slip. One row for the application, with f_Queue_Status 'PEN'. The row is
/// a PayInSlipStagingRow (Models/PayInSlipStaging.cs); who wrote it, from which
/// session and address, is the stamp every row of the application carries.
/// Pay In Slip Generation lists these rows, and a slip is made from one
/// (PayInSlipHeaderAndDetail.cs).
/// </summary>
internal static class PayInSlipStaging
{
    public static Task InsertAsync(IDbConnection db, IDbTransaction tx, Stamp at, PayInSlipStagingRow row) =>
        db.ExecuteAsync(PayInSlipQueries.InsertStaging, new
        {
            row.ApplicationNumber, row.ApplicantName, row.AgencyCode,
            row.ChequeNumber, row.ChequeDate, row.ChequeAmount,
            row.DrawnBankName, row.DrawnBranchName, row.DrawnBankMicr, row.DrawnBankIfsc,
            row.CmsBankName, row.CmsLocationCode, row.CmsLocationName,
            row.QueueStatus, at.UserName, at.Ip, SessionId = at.SessionId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, tx);

    /// <summary>The application's row in the staging table, the newest should there be two; null when it has none.</summary>
    public static Task<PayInSlipStagingRow?> FindAsync(IDbConnection db, IDbTransaction tx, string appNo) =>
        db.QuerySingleOrDefaultAsync<PayInSlipStagingRow?>(PayInSlipQueries.FindStaging, new { AppNo = appNo }, tx);
}
