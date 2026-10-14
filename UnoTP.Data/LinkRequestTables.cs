using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// What came of sending a link: sent; refused, with the reason the partner is shown;
/// or there was no live link to send again.
/// </summary>
internal sealed record LinkWrite(bool Sent, string? Refused)
{
    public static readonly LinkWrite Done = new(true, null);
    public static readonly LinkWrite NoLiveLink = new(false, null);
    public static LinkWrite RefusedBecause(string reason) => new(false, reason);
}

/// <summary>
/// The queries a send runs: one set per page, each from that page's own file and shared
/// with no other page. The SQL is the same in every set; each file is edited on its own.
/// A page that never does one of the things has null there; SubmittedApplications and
/// SentLinks are the reads of the one application a send is for, on Short URL, which
/// sends from a list.
/// </summary>
internal sealed record LinkQuerySet(
    string? SubmittedApplications, string? SentLinks, string? RecordLink,
    string RetirePurchaseLink, string RetireRenewalLink, string InsertPurchaseLink, string InsertRenewalLink,
    string? SendPurchaseLinkAgain, string? SendRenewalLinkAgain, string MessagesSentLately,
    string TellPurchaseBySms, string TellPurchaseByEmail, string TellRenewalBySms, string TellRenewalByEmail)
{
    /// <summary>The set the page runs.</summary>
    public static LinkQuerySet For(LinkPage page)
    {
        if (page == LinkPage.ApplicationSubmitted) return OfApplicationSubmitted;
        if (page == LinkPage.ShortUrl) return OfShortUrl;
        return OfReviewSummary;
    }

    // Submit and send link: the first link. The page never sends one again.
    private static readonly LinkQuerySet OfReviewSummary = new(
        null, null, ReviewSummaryQueries.RecordLink,
        ReviewSummaryQueries.RetirePurchaseLink, ReviewSummaryQueries.RetireRenewalLink,
        ReviewSummaryQueries.InsertPurchaseLink, ReviewSummaryQueries.InsertRenewalLink,
        null, null, ReviewSummaryQueries.MessagesSentLately,
        ReviewSummaryQueries.TellPurchaseBySms, ReviewSummaryQueries.TellPurchaseByEmail,
        ReviewSummaryQueries.TellRenewalBySms, ReviewSummaryQueries.TellRenewalByEmail);

    // Send again; and the first link, for an application submitted with Try later.
    private static readonly LinkQuerySet OfApplicationSubmitted = new(
        null, null, ApplicationSubmittedQueries.RecordLink,
        ApplicationSubmittedQueries.RetirePurchaseLink, ApplicationSubmittedQueries.RetireRenewalLink,
        ApplicationSubmittedQueries.InsertPurchaseLink, ApplicationSubmittedQueries.InsertRenewalLink,
        ApplicationSubmittedQueries.SendPurchaseLinkAgain, ApplicationSubmittedQueries.SendRenewalLinkAgain,
        ApplicationSubmittedQueries.MessagesSentLately,
        ApplicationSubmittedQueries.TellPurchaseBySms, ApplicationSubmittedQueries.TellPurchaseByEmail,
        ApplicationSubmittedQueries.TellRenewalBySms, ApplicationSubmittedQueries.TellRenewalByEmail);

    // Send link, Resend and New link.
    private static readonly LinkQuerySet OfShortUrl = new(
        ShortUrlQueries.SubmittedApplications, ShortUrlQueries.SentLinks, ShortUrlQueries.RecordLink,
        ShortUrlQueries.RetirePurchaseLink, ShortUrlQueries.RetireRenewalLink,
        ShortUrlQueries.InsertPurchaseLink, ShortUrlQueries.InsertRenewalLink,
        ShortUrlQueries.SendPurchaseLinkAgain, ShortUrlQueries.SendRenewalLinkAgain,
        ShortUrlQueries.MessagesSentLately,
        ShortUrlQueries.TellPurchaseBySms, ShortUrlQueries.TellPurchaseByEmail,
        ShortUrlQueries.TellRenewalBySms, ShortUrlQueries.TellRenewalByEmail);
}

/// <summary>
/// How many messages went out lately, as the page's MessagesSentLately counts them.
/// </summary>
internal sealed class MessagesSentRow
{
    public int ToApplication { get; set; }
    public int? SecondsSinceLast { get; set; }
    public int ToMobile { get; set; }
    public int ToEmail { get; set; }
}

/// <summary>
/// Where the short link sent to an investor is kept, in the FD system's own tables:
/// a purchase's in t_FD_PGW_PaymentURLRequest_dtl (database MMFSL_APP_FDPGW), a renewal's in
/// T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL. The columns written and read here are
/// the same in both. The row is a LinkRequestRow (Models/LinkRequest.cs).
///
/// An application has one live link: its active row. A link sent for the first time
/// is a new row. Regenerate makes no new link and no new row: the same short link
/// goes again, and its row is given a new expiry (LinkRequestRow.RegeneratedForHours
/// from then). The tables hold other systems' rows too: every statement here names
/// the application it is for.
///
/// What sends the link to the investor is the communication table,
/// t_FD_PGW_Comm_Hdr: a row there is an SMS or an e-mail the FD system's sender
/// picks up, so a row is written only once a send has passed the limits
/// (CommLimits) - and the whole send, link row and messages, is one transaction
/// under the application's own lock, so two presses at once cannot both go out.
/// </summary>
internal static class LinkTables
{
    /// <summary>
    /// The table an application's link goes in, by name, for a log line. Each query names
    /// its own table (ReviewSummaryQueries, ApplicationSubmittedQueries).
    /// </summary>
    public static string For(bool renewal) => renewal ? "MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL" : "MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl";

    /// <summary>
    /// A link sent for the first time: a new row, open for row.ValidityHours from now,
    /// and the messages that carry it. Any row the application had before goes out of
    /// use, so the new one is its live link. Nothing is written when the send is refused.
    /// </summary>
    public static async Task<LinkWrite> SendAsync(SqlConnection db, LinkQuerySet q, bool renewal, LinkRequestRow row, LinkContact to, IPartner by)
    {
        await using var tx = await db.BeginTransactionAsync();
        if (await RefusedAsync(db, tx, q, row.AppNo, to, by) is { } refused) return refused;

        var retire = renewal ? q.RetireRenewalLink : q.RetirePurchaseLink;
        var insert = renewal ? q.InsertRenewalLink : q.InsertPurchaseLink;
        await db.ExecuteAsync(retire, new { row.AppNo, UpdatedBy = by.UserClusterId, by.UserName, Ip = by.IpAddress }, tx);
        await db.ExecuteAsync(insert, new
        {
            row.AppNo, row.InvestorName, row.InvestmentAmount, row.InvestorPan, row.ApplicationDate,
            row.ShortUrl, row.FullUrl, row.Key, row.ValidityHours,
            CreatedBy = by.UserClusterId, by.UserName, Ip = by.IpAddress, SessionId = SessionNumber(by),
        }, tx);

        var told = await TellAsync(db, tx, q, renewal, row.AppNo, to);
        if (!told.Sent) return told;
        await tx.CommitAsync();
        return LinkWrite.Done;
    }

    /// <summary>
    /// Resend: the application's live link is sent again as it is - the same short
    /// link - open for LinkRequestRow.RegeneratedForHours from now, and the messages that
    /// carry it. NoLiveLink when the application has none to send again. Nothing is
    /// written when the send is refused.
    /// </summary>
    public static async Task<LinkWrite> SendAgainAsync(SqlConnection db, LinkQuerySet q, bool renewal, string appNo, LinkContact to, IPartner by)
    {
        await using var tx = await db.BeginTransactionAsync();
        if (await RefusedAsync(db, tx, q, appNo, to, by) is { } refused) return refused;

        var sendAgain = renewal ? q.SendRenewalLinkAgain : q.SendPurchaseLinkAgain;
        if (sendAgain is null) throw new InvalidOperationException("This page does not send a link again.");
        var rows = await db.ExecuteAsync(sendAgain, new { AppNo = appNo, Hours = LinkRequestRow.RegeneratedForHours, UpdatedBy = by.UserClusterId, by.UserName, Ip = by.IpAddress }, tx);
        if (rows == 0) return LinkWrite.NoLiveLink;

        var told = await TellAsync(db, tx, q, renewal, appNo, to);
        if (!told.Sent) return told;
        await tx.CommitAsync();
        return LinkWrite.Done;
    }

    // The limits a send is held to, checked under the application's own lock so that
    // two sends at once cannot both pass. The reason the send is refused, or null.
    private static async Task<LinkWrite?> RefusedAsync(SqlConnection db, DbTransaction tx, LinkQuerySet q, string appNo, LinkContact to, IPartner by)
    {
        if (!to.AnyUsable) return LinkWrite.RefusedBecause(Messages.ShortUrl.NoUsableContact);

        // The lock: the application's header, read for update, held to the end of the transaction.
        await db.QueryFirstOrDefaultAsync<string>(ApplicationQueries.HeaderLocked, new { AppNo = appNo, Partner = by.Id }, tx);

        var sent = await db.QuerySingleAsync<MessagesSentRow>(q.MessagesSentLately,
            new { AppNo = appNo, Mobile = to.UsableMobile, Email = to.UsableEmail, Hours = CommLimits.DayInHours }, tx);

        if (sent.SecondsSinceLast is { } seconds && seconds < CommLimits.SecondsBetweenSends)
            return LinkWrite.RefusedBecause(Messages.ShortUrl.LinkJustSent(CommLimits.SecondsBetweenSends / 60));
        if (sent.ToApplication >= CommLimits.MostPerApplicationPerDay)
            return LinkWrite.RefusedBecause(Messages.ShortUrl.TooManyForApplication(CommLimits.MostPerApplicationPerDay));
        if (sent.ToMobile >= CommLimits.MostPerContactPerDay || sent.ToEmail >= CommLimits.MostPerContactPerDay)
            return LinkWrite.RefusedBecause(Messages.ShortUrl.TooManyForContact(CommLimits.MostPerContactPerDay));
        return null;
    }

    // The messages that carry the link: an SMS when the mobile number is usable, an
    // e-mail when the address is. Each is a row copied from the application's live
    // link row; a row that does not get written means there is no live link.
    private static async Task<LinkWrite> TellAsync(SqlConnection db, DbTransaction tx, LinkQuerySet q, bool renewal, string appNo, LinkContact to)
    {
        var bySms = renewal ? q.TellRenewalBySms : q.TellPurchaseBySms;
        var byEmail = renewal ? q.TellRenewalByEmail : q.TellPurchaseByEmail;
        // TO CONFIRM: the procedure's @RequestFormCode, which the app does not know yet.
        var message = new { AppNo = appNo, Mobile = to.UsableMobile, Email = to.UsableEmail, RequestFormCode = (string?)null };

        if (to.UsableMobile is not null && await db.ExecuteAsync(bySms, message, tx) == 0) return LinkWrite.NoLiveLink;
        if (to.UsableEmail is not null && await db.ExecuteAsync(byEmail, message, tx) == 0) return LinkWrite.NoLiveLink;
        return LinkWrite.Done;
    }

    // The user's backend session, when it is a number; null otherwise.
    private static long? SessionNumber(IPartner by)
    {
        if (long.TryParse(by.SessionId, out var number)) return number;
        return null;
    }
}
