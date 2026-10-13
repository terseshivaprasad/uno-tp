using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// Where the short link sent to an investor is kept, in the FD system's own tables:
/// a purchase's in t_FD_PGW_PaymentUrlRequest_dtl, a renewal's in
/// T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL. The columns written and read here are
/// the same in both. The row is a LinkRequestRow (Models/LinkRequest.cs).
///
/// An application has one live link: its active row. A link sent for the first time
/// is a new row. Regenerate makes no new link and no new row: the same short link
/// goes again, and its row is given a new expiry (LinkRequestRow.RegeneratedForHours
/// from then). The tables hold other systems' rows too: every statement here names
/// the application it is for.
/// </summary>
internal static class LinkTables
{
    /// <summary>The table an application's link goes in.</summary>
    public static string For(bool renewal) => renewal ? LinkQueries.RenewalTable : LinkQueries.PurchaseTable;

    /// <summary>
    /// A link sent for the first time: a new row, open for row.ValidityHours from now.
    /// Any row the application had before goes out of use, so the new one is its live link.
    /// </summary>
    public static async Task SendAsync(IDbConnection db, bool renewal, LinkRequestRow row, IPartner by)
    {
        await db.ExecuteAsync(LinkQueries.Retire(For(renewal)), new { row.AppNo, UpdatedBy = by.UserClusterId, by.UserName, Ip = by.IpAddress });

        await db.ExecuteAsync(LinkQueries.Insert(For(renewal)), new
        {
            row.AppNo, row.InvestorName, row.InvestmentAmount, row.InvestorPan, row.ApplicationDate,
            row.ShortUrl, row.FullUrl, row.ValidityHours,
            CreatedBy = by.UserClusterId, by.UserName, Ip = by.IpAddress, SessionId = SessionNumber(by),
        });
    }

    /// <summary>
    /// Regenerate: the application's live link is sent again as it is - the same short
    /// link - and is open for LinkRequestRow.RegeneratedForHours from now. False when
    /// the application has no live link to send again.
    /// </summary>
    public static async Task<bool> SendAgainAsync(IDbConnection db, bool renewal, string appNo, IPartner by)
    {
        var rows = await db.ExecuteAsync(LinkQueries.SendAgain(For(renewal)), new { AppNo = appNo, Hours = LinkRequestRow.RegeneratedForHours, UpdatedBy = by.UserClusterId, by.UserName, Ip = by.IpAddress });
        return rows > 0;
    }

    // The user's backend session, when it is a number; null otherwise.
    private static long? SessionNumber(IPartner by)
    {
        if (long.TryParse(by.SessionId, out var number)) return number;
        return null;
    }
}
