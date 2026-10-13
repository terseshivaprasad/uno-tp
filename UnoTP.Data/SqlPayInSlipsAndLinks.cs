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
    public DateTime? PaidOn { get; set; }
    public DateTime? BookedOn { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string? AppType { get; set; }
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

    /// <summary>A digital application waits on the investor to accept it; one on paper is signed already.</summary>
    public bool Accepted => !Digital || AcceptedOn is not null;

    public bool Closed => BookedOn is not null || CancelledOn is not null;
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
    /// its link row (LinkQueries.AcceptedOn) - and has no slip until then; one on paper
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
    public async Task<IReadOnlyList<SlipRecord>> SlipsAsync(CancellationToken ct = default) =>
        await SlipsAsync(null, ct);

    // A slip is issued only for an application still open and, for a digital one,
    // accepted. A reprint issues a fresh number; the old one stands on record.
    public async Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default)
    {
        if ((await SlipsAsync(appNo, ct)).FirstOrDefault() is not { State: "pending" or "generated", Accepted: true }) return null;
        var prefix = await reference.SettingAsync("slipNoPrefix", ct);

        await using (var connection = await db.OpenAsync(ct))
        {
            await using var tx = await connection.BeginTransactionAsync(ct);
            var staged = await PayInSlipStaging.FindAsync(connection, tx, appNo);
            if (staged is null) return null;
            var slipNo = await PayInSlipHeaderAndDetail.NextSlipNoAsync(connection, tx, prefix);
            await PayInSlipHeaderAndDetail.InsertAsync(connection, tx,
                PayInSlipHeaderRow.For(staged, slipNo, partner), PayInSlipDetailRow.For(staged, slipNo, partner));
            await tx.CommitAsync(ct);
        }
        return (await SlipsAsync(appNo, ct)).FirstOrDefault();
    }

    private async Task<IReadOnlyList<SlipRecord>> SlipsAsync(string? appNo, CancellationToken ct)
    {
        var cancellationDays = (await reference.ConfigAsync(ct)).CancellationDays;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<StagedSlipRow>(
            PayInSlipQueries.StagedSlips(oneApplication: appNo is not null),
            new { Partner = partner.Id, AppNo = appNo });
        var today = DateTime.Today;

        return rows.Select(r =>
        {
            var state = r.CancelledOn is not null ? "cancelled"
                : r.PaidOn is not null ? "deposited"
                : (today - r.SubmittedOn.Date).Days >= cancellationDays ? "lapsed"
                : r.SlipNo is not null ? "generated"
                : "pending";
            return new SlipRecord(r.AppNo, Masks.Name(r.Name), r.Amount ?? 0, r.PayMode ?? "", r.ChequeNo ?? "", r.BankName ?? "",
                r.SubmittedOn, r.Branch, r.Digital, r.Accepted, state, r.SlipNo, r.Digital ? r.AcceptedOn : null);
        }).ToList();
    }
}

/// <summary>
/// The links sent to investors (LinkTables: the link request table for a purchase,
/// the one for a renewal), and the submitted applications waiting on one: to pay
/// online, or to accept a digital application paid on paper.
/// </summary>
public sealed class SqlLinks(Db db, IPartner partner, SqlReference reference, ILogger<SqlLinks> log) : ILinkApi
{
    private sealed class LinkRow
    {
        public string AppNo { get; set; } = "";
        public string Name { get; set; } = "";
        public string? PayMode { get; set; }
        public string? AppType { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public DateTime SentOn { get; set; }
        public DateTime ExpiresOn { get; set; }
        public DateTime SubmittedOn { get; set; }
        public DateTime? AcceptedOn { get; set; }
        public DateTime? PaidOn { get; set; }
    }

    // The live link of each of the partner's submitted applications: its active row.
    // The link tables do not say what a link asks of the investor, nor where it went:
    // both are read off the application.
    public async Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default)
    {
        var paper = await PaperModesAsync(ct);
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<LinkRow>(LinkQueries.SentLinks, new { Partner = partner.Id });

        var now = DateTime.Now;
        var sent = new List<SentLinkRecord>();
        foreach (var r in rows)
        {
            // A digital application paid on paper is sent a link to accept it; any other, to pay.
            var purpose = paper.Contains(r.PayMode ?? "") && r.AppType != ApplicationType.Physical ? "acceptance" : "payment";
            var done = (purpose == "payment" ? r.PaidOn : r.AcceptedOn) is not null;
            var status = done ? "done" : r.ExpiresOn <= now ? "expired" : "open";
            sent.Add(new SentLinkRecord(r.AppNo, Masks.Name(r.Name), Masks.Mobile(r.Mobile ?? ""), Masks.Email(r.Email ?? ""),
                purpose, r.SentOn, r.ExpiresOn, status, r.SubmittedOn));
        }
        return sent;
    }

    public async Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default) =>
        (await WaitingAsync(null, ct)).Select(w => w.Record).ToList();

    // The payment modes paid on paper: those a copy of the instrument is filed for (a cheque).
    private async Task<HashSet<string>> PaperModesAsync(CancellationToken ct) =>
        (await reference.ReferenceAsync(ct)).PaymentModes.Where(m => m.Document is not null).Select(m => m.Name).ToHashSet();

    // What a submitted application is waiting on the investor for, if anything: a
    // digital one paid on paper waits to be accepted; one paid online, to be paid.
    private async Task<List<(PendingRecord Record, SubmittedRow Row)>> WaitingAsync(string? appNo, CancellationToken ct)
    {
        var paper = await PaperModesAsync(ct);
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<SubmittedRow>(
            LinkQueries.SubmittedApplications(oneApplication: appNo is not null),
            new { Partner = partner.Id, AppNo = appNo });
        return rows.Where(r => !r.Closed)
            .Select(r => (Row: r, Due: paper.Contains(r.PayMode ?? "") ? (r.Accepted ? null : "acceptance") : r.PaidOn is null ? "payment" : null))
            .Where(x => x.Due is not null)
            .Select(x => (new PendingRecord(x.Row.AppNo, Masks.Name(x.Row.Name), x.Row.SubmittedOn, Masks.Mobile(x.Row.Mobile ?? ""),
                x.Due!, Masks.Email(x.Row.Email ?? "")), x.Row))
            .ToList();
    }

    // A link goes out only for what the application is waiting on. One that has a
    // link already is sent the same short link again, open for
    // LinkRequestRow.RegeneratedForHours. One that has none gets its first row, open
    // for linkValidityHours; no address is built here, so that row carries none.
    public async Task<SentLinkRecord?> SendAsync(string appNo, string purpose, CancellationToken ct = default)
    {
        var waiting = (await WaitingAsync(appNo, ct)).FirstOrDefault();
        if (waiting.Record is null || waiting.Record.Due != purpose) return null;
        var hours = (await reference.ConfigAsync(ct)).LinkValidityHours.GetValueOrDefault(purpose);
        if (hours <= 0) return null;

        var row = waiting.Row;
        await using (var connection = await db.OpenAsync(ct))
        {
            if (!await LinkTables.SendAgainAsync(connection, row.Renewal, appNo, partner))
            {
                await LinkTables.SendAsync(connection, row.Renewal, new LinkRequestRow
                {
                    AppNo = appNo, InvestorName = row.Name, InvestmentAmount = row.Amount, InvestorPan = row.Pan,
                    ApplicationDate = row.SubmittedOn, ValidityHours = hours,
                }, partner);
            }
        }
        // No SMS or e-mail gateway is wired in yet: the send is logged for now.
        log.LogInformation("{Purpose} link for {AppNo} sent to {Mobile} and {Email}", purpose, appNo, waiting.Record.Mobile, waiting.Record.Email);
        return (await SentAsync(ct)).FirstOrDefault(l => l.AppNo == appNo);
    }
}
