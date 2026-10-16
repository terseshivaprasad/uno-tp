using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// A submitted application as the registers see it: the application as submitted
/// (its APR rows), where it has got to since, and the partner's branch.
/// </summary>
internal sealed class SubmittedRow
{
    public string AppNo { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime SubmittedOn { get; set; }
    public DateTime? AcceptedOn { get; set; }
    /// <summary>Whether the payment log holds a payment for the application (status Y): paid online.</summary>
    public bool Paid { get; set; }
    public DateTime? BookedOn { get; set; }
    /// <summary>Cancelled by the app (a renewal) or by the FD system (an active row in t_FD_BT_Cancelled_Application_Dtl).</summary>
    public bool Cancelled { get; set; }
    public string? AppType { get; set; }
    /// <summary>The deposit row's status: PEN_E when the investor's KYC was fetched from CKYC and they have still to confirm it.</summary>
    public string? Status { get; set; }
    public long? Amount { get; set; }
    /// <summary>Whether the application renews a deposit: its link is kept in the renewal's link request table.</summary>
    public bool Renewal { get; set; }
    public string? PayMode { get; set; }
    public string? ChequeNo { get; set; }
    public string? BankName { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    /// <summary>The Axis CMS branch the cheque is presented at, as Bank Details &amp; Payment saved it; empty for a payment with no cheque.</summary>
    public string Branch { get; set; } = "";
    public string? Pan { get; set; }

    public bool Digital => AppType != "PHYSICAL";

    /// <summary>
    /// Whether the investor has to accept the application: a digital one, as it is not signed; or one
    /// whose rows are PEN_E, as the KYC fetched from CKYC has to be confirmed by them.
    /// </summary>
    public bool NeedsAcceptance => Digital || Status == RowStatus.PendingCkyc;

    /// <summary>Whether the application is accepted: one that needs no acceptance is, the other once the investor has.</summary>
    public bool Accepted => !NeedsAcceptance || AcceptedOn is not null;

    public bool Closed => BookedOn is not null || Cancelled;
}

/// <summary>
/// A cheque application as Pay In Slip Generation lists it: its row in the staging
/// table (t_FD_BT_Payinslip_Staging_Hdr), where the application itself has got to,
/// and the number of its newest slip (t_FD_BT_Payinslip_Dtl), if one was generated.
/// </summary>
internal sealed class StagedSlipRow
{
    public string AppNo { get; set; } = "";
    public string Name { get; set; } = "";
    public long? Amount { get; set; }
    public string? ChequeNo { get; set; }
    public string? BankName { get; set; }
    /// <summary>The Axis CMS branch the cheque is presented at.</summary>
    public string Branch { get; set; } = "";
    public string? PayMode { get; set; }
    public string? AppType { get; set; }
    public DateTime SubmittedOn { get; set; }
    public DateTime? AcceptedOn { get; set; }
    public DateTime? PaidOn { get; set; }
    public DateTime? CancelledOn { get; set; }
    /// <summary>The number of the newest slip generated for the application; null when none was.</summary>
    public string? SlipNo { get; set; }

    public bool Digital => AppType != "PHYSICAL";

    /// <summary>
    /// A digital application waits on the investor to accept it - the four flags on
    /// its newest link row (ApplicationQueries.AcceptedOn) - and has no slip until then; one on paper
    /// is signed already.
    /// </summary>
    public bool Accepted => !Digital || AcceptedOn is not null;
}

/// <summary>
/// Axis pay-in slips for the partner's applications paid by cheque. The list is the
/// staging table's rows (t_FD_BT_Payinslip_Staging_Hdr), one put there for each
/// such application on its final submit. A slip generated is a row in
/// t_FD_BT_Payinslip_Hdr and one in t_FD_BT_Payinslip_Dtl.
/// </summary>
public sealed class SqlPayInSlips(Db db, IPartner partner, SqlReference reference) : IPayInSlipApi
{
    // Only what the search reaches is read: the days or the number go to SQL Server as
    // parameters of the one query, and those the search does not use as NULL. One row
    // more than is listed is asked for, to know whether the search reaches more than
    // the list shows.
    public async Task<SlipsFound> SearchAsync(ApplicationSearch search, CancellationToken ct = default)
    {
        var rows = await ReadAsync(ApplicationSearch.Most + 1, search, null, ct);
        return new SlipsFound(rows.Take(ApplicationSearch.Most).ToList(), rows.Count > ApplicationSearch.Most);
    }

    // A count must count every one, so no row is left off: the days bound what is read.
    public async Task<IReadOnlyList<SlipRecord>> StillOpenAsync(CancellationToken ct = default)
    {
        var days = (await reference.ConfigAsync(ct)).CancellationDays;
        var today = DateTime.Today;
        return await ReadAsync(int.MaxValue, ApplicationSearch.RaisedBetween(today.AddDays(-(days - 1)), today), null, ct);
    }

    // One application's row, whatever its age.
    private async Task<SlipRecord?> SlipOfAsync(string appNo, CancellationToken ct) =>
        (await ReadAsync(1, null, appNo, ct)).FirstOrDefault();

    // A slip is issued only for an application still open and, for a digital one,
    // accepted - and once: as the FD system's own procedure does it, an application that
    // has a slip is printed again, and nothing is written. The number comes from the FD
    // system's procedure; the rows are copied from the staging row (PayInSlipQueries).
    public async Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default)
    {
        if (await SlipOfAsync(appNo, ct) is not { State: "pending" or "generated", Accepted: true }) return null;

        await using (var connection = await db.OpenAsync(ct))
        {
            await using var tx = await connection.BeginTransactionAsync(ct);
            if (!await PayInSlipHeaderAndDetail.AlreadyMadeAsync(connection, tx, appNo))
            {
                var staged = await PayInSlipStaging.FindAsync(connection, tx, appNo);
                if (staged is null) return null;
                var slipNo = await PayInSlipHeaderAndDetail.NextSlipNoAsync(connection, tx, staged);
                // No header means no deposit row standing as APR: nothing is written.
                if (await PayInSlipHeaderAndDetail.InsertHeaderAsync(connection, tx, appNo, slipNo) is not { } hdrSequence) return null;
                await PayInSlipHeaderAndDetail.InsertDetailAsync(connection, tx, appNo, slipNo, hdrSequence);
                await PayInSlipStaging.MarkGeneratedAsync(connection, tx, appNo);
                await PayInSlipHeaderAndDetail.MarkGeneratedAsync(connection, tx, hdrSequence);
                await tx.CommitAsync(ct);
            }
        }
        return await SlipOfAsync(appNo, ct);
    }

    public async Task<SlipPrint?> SlipToPrintAsync(string appNo, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SlipPrint?>(PayInSlipQueries.SlipToPrint, new { AppNo = appNo, Partner = partner.Id });
    }

    // The cheque applications a search reaches, or the one application named, each with
    // where its slip has got to. At most `most` of them.
    private async Task<IReadOnlyList<SlipRecord>> ReadAsync(int most, ApplicationSearch? search, string? appNo, CancellationToken ct)
    {
        var cancellationDays = (await reference.ConfigAsync(ct)).CancellationDays;
        var paymentModes = (await reference.ReferenceAsync(ct)).PaymentModes;
        var asked = new DynamicParameters();
        asked.Add("Partner", partner.Id);
        asked.Add("Most", most);
        asked.AddSearch(search);
        asked.Add("AppNo", appNo, DbType.String);

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<StagedSlipRow>(PayInSlipQueries.StagedSlips, asked);
        var today = DateTime.Today;

        return rows.Select(r =>
        {
            // A cancelled application reads as lapsed: the page has one state for both.
            var state = r.CancelledOn is not null ? "lapsed"
                : r.PaidOn is not null ? "deposited"
                : (today - r.SubmittedOn.Date).Days >= cancellationDays ? "lapsed"
                : r.SlipNo is not null ? "generated"
                : "pending";
            // The payment row holds the mode's code; the page shows its name.
            return new SlipRecord(r.AppNo, Masks.Name(r.Name), r.Amount ?? 0, PaymentModeOption.NameOf(paymentModes, r.PayMode ?? ""), r.ChequeNo ?? "", r.BankName ?? "",
                r.SubmittedOn, r.Branch, r.Digital, r.Accepted, state, r.SlipNo, r.Digital ? r.AcceptedOn : null);
        }).ToList();
    }
}

/// <summary>
/// The links sent to investors (LinkTables: the link request table for a purchase,
/// the one for a renewal), and the submitted applications waiting on one: to pay,
/// for an application paid online through the gateway, until the payment log says it
/// is paid; to accept, for one paid offline (a cheque, RTGS) or a renewal, until it is
/// accepted. A physical application paid offline is accepted by its signed form (unless
/// its rows are PEN_E), needs neither, and is in neither list.
/// </summary>
public sealed class SqlLinks(Db db, IPartner partner, SqlReference reference, ILogger<SqlLinks> log) : ILinkApi
{
    private sealed class LinkRow
    {
        public string AppNo { get; set; } = "";
        public string Name { get; set; } = "";
        public string? PayMode { get; set; }
        public string? AppType { get; set; }
        public string? Status { get; set; }
        public bool Renewal { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public DateTime SentOn { get; set; }
        public DateTime ExpiresOn { get; set; }
        public DateTime SubmittedOn { get; set; }
        public DateTime? AcceptedOn { get; set; }
        public bool Paid { get; set; }
    }

    // The live link of each of the partner's applications submitted in the days Short
    // URL lists: its active row. Only those days are read.
    public async Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default) =>
        await SentAsync(ShortUrlQueries.SentLinks, null, ct);

    // The one application named, whatever its age; or, with none named, those
    // submitted in the days Short URL lists. The link tables do not say what a link
    // asks of the investor, nor where it went: both are read off the application.
    private async Task<IReadOnlyList<SentLinkRecord>> SentAsync(string query, string? appNo, CancellationToken ct)
    {
        var offline = await OfflineModesAsync(ct);
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<LinkRow>(query,
            new { Partner = partner.Id, AppNo = appNo, From = await ListedFromAsync(appNo, ct), ApplicationType.Physical, OfflineModes = offline });

        var now = DateTime.Now;
        var sent = new List<SentLinkRecord>();
        foreach (var r in rows)
        {
            // A renewal, or an application paid offline, is sent a link to accept it; one paid
            // online through the gateway, to pay.
            var purpose = r.Renewal || offline.Contains(r.PayMode ?? "") ? "acceptance" : "payment";
            var done = purpose == "payment" ? r.Paid : r.AcceptedOn is not null;
            var status = done ? "done" : r.ExpiresOn <= now ? "expired" : "open";
            sent.Add(new SentLinkRecord(r.AppNo, Masks.Name(r.Name), Masks.Mobile(r.Mobile ?? ""), Masks.Email(r.Email ?? ""),
                purpose, r.SentOn, r.ExpiresOn, status, r.SubmittedOn));
        }
        return sent;
    }

    public async Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default) =>
        (await WaitingAsync(ShortUrlQueries.SubmittedApplications, null, ct)).Select(w => w.Record).ToList();

    // The first day Short URL lists: an application submitted before it has stood
    // unpaid for longer than cancellationDays, and can no longer carry a link. Null
    // when one application is named, which is read whatever its age.
    private async Task<DateTime?> ListedFromAsync(string? appNo, CancellationToken ct)
    {
        if (appNo is not null) return null;
        return DateTime.Today.AddDays(-(await reference.ConfigAsync(ct)).CancellationDays);
    }

    // The payment modes not paid through the gateway - a cheque, RTGS - by their codes, as
    // the payment row holds the mode. An application paid by one of them is sent a link to
    // accept it, never to pay.
    private async Task<HashSet<string>> OfflineModesAsync(CancellationToken ct) =>
        (await reference.ReferenceAsync(ct)).PaymentModes.Where(m => m.Name != PaymentModeOption.Online).Select(m => m.Code).ToHashSet();

    // What a submitted application is waiting on the investor for, if anything: a
    // renewal, or one paid offline, waits to be accepted - not once it is accepted, nor
    // once it is paid; one paid online through the gateway waits to be paid.
    // The one application named, whatever its age; or, with none named, those
    // submitted in the days Short URL lists. The list is never read whole.
    private async Task<List<(PendingRecord Record, SubmittedRow Row)>> WaitingAsync(string query, string? appNo, CancellationToken ct)
    {
        var offline = await OfflineModesAsync(ct);
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<SubmittedRow>(query,
            new { Partner = partner.Id, AppNo = appNo, From = await ListedFromAsync(appNo, ct), ApplicationType.Physical, OfflineModes = offline });
        return rows.Where(r => !r.Closed)
            .Select(r => (Row: r, Due: DueOf(r, offline)))
            .Where(x => x.Due is not null)
            .Select(x => (new PendingRecord(x.Row.AppNo, Masks.Name(x.Row.Name), x.Row.SubmittedOn, Masks.Mobile(x.Row.Mobile ?? ""),
                x.Due!, Masks.Email(x.Row.Email ?? "")), x.Row))
            .ToList();
    }

    // What the application is waiting on the investor for; null for nothing. A physical
    // application paid offline, or a physical renewal, is accepted by its signed form
    // and waits on nothing - unless its rows are PEN_E, when the investor confirms the CKYC.
    private static string? DueOf(SubmittedRow r, HashSet<string> offline)
    {
        if (!r.Digital && (r.Renewal || offline.Contains(r.PayMode ?? "")) && r.Status != RowStatus.PendingCkyc) return null;
        if (r.Renewal || offline.Contains(r.PayMode ?? ""))
        {
            if (r.Accepted || r.Paid) return null;
            return "acceptance";
        }
        if (r.Paid) return null;
        return "payment";
    }

    // A link goes out only for what the application is waiting on. Resend sends the
    // link the application has again - the same short link, open for
    // LinkRequestRow.RegeneratedForHours; an application with none gets its first row.
    // A new link takes the old row out of use and writes a new one, open for
    // linkValidityHours. No address is built here, so a row written here carries
    // none. Either way the messages that carry it are written under the limits (CommLimits).
    public async Task<LinkSent> SendAsync(string appNo, string purpose, LinkPage from, bool newLink = false, CancellationToken ct = default)
    {
        var q = LinkQuerySet.For(from);
        if (q.SubmittedApplications is null || q.SentLinks is null) throw new InvalidOperationException("This page does not send from a list.");
        var waiting = (await WaitingAsync(q.SubmittedApplications, appNo, ct)).FirstOrDefault();
        if (waiting.Record is null || waiting.Record.Due != purpose) return new LinkSent(null);
        var hours = (await reference.ConfigAsync(ct)).LinkValidityHours.GetValueOrDefault(purpose);
        if (hours <= 0) return new LinkSent(null);

        var row = waiting.Row;
        var to = new LinkContact(row.Mobile ?? "", row.Email ?? "");
        await using (var connection = await db.OpenAsync(ct))
        {
            var wrote = newLink ? LinkWrite.NoLiveLink : await LinkTables.SendAgainAsync(connection, q, row.Renewal, appNo, to, partner);
            if (wrote.Refused is null && !wrote.Sent)
            {
                wrote = await LinkTables.SendAsync(connection, q, row.Renewal, new LinkRequestRow
                {
                    AppNo = appNo, InvestorName = row.Name, InvestmentAmount = row.Amount, InvestorPan = row.Pan,
                    ApplicationDate = row.SubmittedOn, ValidityHours = hours,
                }, to, partner);
            }
            if (!wrote.Sent) return new LinkSent(null, wrote.Refused ?? Messages.ShortUrl.NoLinkSent);
        }
        log.LogInformation("{Purpose} link for {AppNo} sent to {Mobile} and {Email}", purpose, appNo, waiting.Record.Mobile, waiting.Record.Email);
        return new LinkSent((await SentAsync(q.SentLinks, appNo, ct)).FirstOrDefault());
    }
}
