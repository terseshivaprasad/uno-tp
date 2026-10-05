using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// A submitted application as the registers see it: the application as submitted
/// (its APR rows), where it has got to since, and the partner's branch.
/// </summary>
internal sealed class SubmittedRow
{
    public const string Select = $"""
        SELECT m.f_App_No AS AppNo, COALESCE(NULLIF(k.f_Kyc_FullName, N''), m.f_Name) AS Name, m.f_Submitted_On AS SubmittedOn,
            m.f_Accepted_On AS AcceptedOn, m.f_Paid_On AS PaidOn, m.f_Booked_On AS BookedOn, m.f_Cancelled_On AS CancelledOn,
            i.AppType, i.Amount, CAST(CASE WHEN m.f_Renew_Dep_No IS NULL THEN 0 ELSE 1 END AS BIT) AS Renewal,
            p.f_Payment_Mode AS PayMode, p.f_Cheque_DD_No AS ChequeNo, p.f_Drawn_Bank_Name AS BankName,
            a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email, ISNULL(pm.f_Branch, N'') AS Branch
        FROM dbo.t_Unotp_Application_Mst m
        {InvestmentRow.CurrentOf}
        LEFT JOIN dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Kyc_Data_Dtl k ON k.f_Appl_No = m.f_App_No AND k.f_Holder_Type = '01' AND k.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Address_Dtl a ON a.f_Appl_No = m.f_App_No AND a.f_Holder_Type = '01' AND a.f_AddType_Code = 'PER' AND a.f_Active = 1
        LEFT JOIN dbo.t_Unotp_Partner_Mst pm ON pm.f_User_Id = m.f_Partner_Id
        WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'APR' AND m.f_Active = 1
        """;

    public string AppNo { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime SubmittedOn { get; set; }
    public DateTime? AcceptedOn { get; set; }
    public DateTime? PaidOn { get; set; }
    public DateTime? BookedOn { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string? AppType { get; set; }
    public long? Amount { get; set; }
    /// <summary>Whether the application renews a deposit: its links are kept in the re-payment link table.</summary>
    public bool Renewal { get; set; }
    public string? PayMode { get; set; }
    public string? ChequeNo { get; set; }
    public string? BankName { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string Branch { get; set; } = "";

    public bool Digital => AppType != "PHYSICAL";

    /// <summary>A digital application waits on the investor to accept it; one on paper is signed already.</summary>
    public bool Accepted => !Digital || AcceptedOn is not null;

    public bool Closed => BookedOn is not null || CancelledOn is not null;
}

/// <summary>
/// Axis pay-in slips (t_Unotp_Pay_In_Slip) for the partner's applications paying by an
/// instrument: a payment mode whose reference entry names a document (a cheque).
/// </summary>
public sealed class SqlPayInSlips(Db db, IPartner partner, SqlReference reference) : IPayInSlipApi
{
    public async Task<IReadOnlyList<SlipRecord>> SlipsAsync(CancellationToken ct = default) =>
        await SlipsAsync(null, ct);

    // A slip is issued only for an application still open, paid on paper and, for a
    // digital one, accepted. A reprint issues a fresh number; the old one stands on record.
    public async Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default)
    {
        if ((await SlipsAsync(appNo, ct)).FirstOrDefault() is not { State: "pending" or "generated", Accepted: true }) return null;
        var prefix = await reference.SettingAsync("slipNoPrefix", ct);
        await using var connection = await db.OpenAsync(ct);
        await connection.ExecuteAsync("""
            INSERT dbo.t_Unotp_Pay_In_Slip (f_App_No, f_Slip_No, f_Generated_By)
            VALUES (@AppNo, @Prefix + RIGHT('0000' + CAST(NEXT VALUE FOR dbo.s_Slip_No AS VARCHAR(20)), 4), @Partner)
            """, new { AppNo = appNo, Prefix = prefix, Partner = partner.Id });
        return (await SlipsAsync(appNo, ct)).FirstOrDefault();
    }

    private async Task<IReadOnlyList<SlipRecord>> SlipsAsync(string? appNo, CancellationToken ct)
    {
        var paper = (await reference.ReferenceAsync(ct)).PaymentModes.Where(m => m.Document is not null).Select(m => m.Name).ToList();
        var cancellationDays = (await reference.ConfigAsync(ct)).CancellationDays;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<SubmittedRow>(
            SubmittedRow.Select + " AND p.f_Payment_Mode IN @Paper" + (appNo is null ? "" : " AND m.f_App_No = @AppNo") + " ORDER BY m.f_Submitted_On DESC",
            new { Partner = partner.Id, Paper = paper, AppNo = appNo });
        var slips = (await connection.QueryAsync<(string AppNo, string SlipNo, DateTime? DepositedOn)>("""
            SELECT s.f_App_No, s.f_Slip_No, s.f_Deposited_On FROM dbo.t_Unotp_Pay_In_Slip s
            WHERE s.f_Id IN (SELECT MAX(f_Id) FROM dbo.t_Unotp_Pay_In_Slip WHERE f_Active = 1 GROUP BY f_App_No)
            """)).ToDictionary(s => s.AppNo);
        var today = DateTime.Today;

        return rows.Select(r =>
        {
            var slip = slips.TryGetValue(r.AppNo, out var s) ? s : default;
            var state = r.CancelledOn is not null ? "cancelled"
                : slip.DepositedOn is not null || r.PaidOn is not null ? "deposited"
                : (today - r.SubmittedOn.Date).Days >= cancellationDays ? "lapsed"
                : slip.SlipNo is not null ? "generated"
                : "pending";
            return new SlipRecord(r.AppNo, Masks.Name(r.Name), r.Amount ?? 0, r.PayMode ?? "", r.ChequeNo ?? "", r.BankName ?? "",
                r.SubmittedOn, r.Branch, r.Digital, r.Accepted, state, slip.SlipNo, r.Digital ? r.AcceptedOn : null);
        }).ToList();
    }
}

/// <summary>
/// Where a link sent to an investor is kept: a purchase's in the payment link
/// table, a renewal's in the re-payment link table. The two have the same columns.
/// </summary>
internal static class LinkTables
{
    public const string Payment = "dbo.t_Unotp_Payment_Link";
    public const string RePayment = "dbo.t_Unotp_RePayment_Link";

    /// <summary>The table an application's links go in.</summary>
    public static string For(bool renewal) => renewal ? RePayment : Payment;

    /// <summary>Both tables as one, for a list that runs across purchases and renewals.</summary>
    public const string Both = $"""
        (SELECT f_Id, f_App_No, f_Purpose, f_Mobile, f_Email, f_Sent_On, f_Expires_On, f_Active FROM {Payment}
         UNION ALL
         SELECT f_Id, f_App_No, f_Purpose, f_Mobile, f_Email, f_Sent_On, f_Expires_On, f_Active FROM {RePayment})
        """;
}

/// <summary>
/// The links sent to investors (the payment link table for a purchase, the
/// re-payment link table for a renewal), and the submitted applications waiting on
/// one: to pay online, or to accept a digital application paid on paper.
/// </summary>
public sealed class SqlLinks(Db db, IPartner partner, SqlReference reference, ILogger<SqlLinks> log) : ILinkApi
{
    private sealed class LinkRow
    {
        public string AppNo { get; set; } = "";
        public string Name { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string Mobile { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime SentOn { get; set; }
        public DateTime ExpiresOn { get; set; }
        public DateTime SubmittedOn { get; set; }
        public DateTime? AcceptedOn { get; set; }
        public DateTime? PaidOn { get; set; }
    }

    // The live link for each of the partner's submitted applications and purpose: the
    // latest sent. Those before it no longer open.
    public async Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<LinkRow>($"""
            SELECT l.f_App_No AS AppNo, COALESCE(NULLIF(k.f_Kyc_FullName, N''), m.f_Name) AS Name,
                l.f_Purpose AS Purpose, l.f_Mobile AS Mobile, l.f_Email AS Email, l.f_Sent_On AS SentOn, l.f_Expires_On AS ExpiresOn,
                m.f_Submitted_On AS SubmittedOn, m.f_Accepted_On AS AcceptedOn, m.f_Paid_On AS PaidOn
            FROM {LinkTables.Both} l
            JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = l.f_App_No
            LEFT JOIN dbo.t_FD_BT_Kyc_Data_Dtl k ON k.f_Appl_No = m.f_App_No AND k.f_Holder_Type = '01' AND k.f_Active = 1
            WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1 AND m.f_Submitted_On IS NOT NULL
              AND l.f_Active = 1
              AND l.f_Id = (SELECT MAX(latest.f_Id) FROM {LinkTables.Both} latest
                            WHERE latest.f_App_No = l.f_App_No AND latest.f_Purpose = l.f_Purpose AND latest.f_Active = 1)
            ORDER BY l.f_Sent_On DESC
            """, new { Partner = partner.Id });

        var now = DateTime.Now;
        var sent = new List<SentLinkRecord>();
        foreach (var r in rows)
        {
            var done = (r.Purpose == "payment" ? r.PaidOn : r.AcceptedOn) is not null;
            var status = done ? "done" : r.ExpiresOn <= now ? "expired" : "open";
            sent.Add(new SentLinkRecord(r.AppNo, Masks.Name(r.Name), r.Mobile, r.Email, r.Purpose, r.SentOn, r.ExpiresOn, status, r.SubmittedOn));
        }
        return sent;
    }

    public async Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default) =>
        (await WaitingAsync(null, ct)).Select(w => w.Record).ToList();

    // What a submitted application is waiting on the investor for, if anything: a
    // digital one paid on paper waits to be accepted; one paid online, to be paid.
    private async Task<List<(PendingRecord Record, SubmittedRow Row)>> WaitingAsync(string? appNo, CancellationToken ct)
    {
        var paper = (await reference.ReferenceAsync(ct)).PaymentModes.Where(m => m.Document is not null).Select(m => m.Name).ToHashSet();
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<SubmittedRow>(
            SubmittedRow.Select + (appNo is null ? "" : " AND m.f_App_No = @AppNo") + " ORDER BY m.f_Submitted_On DESC",
            new { Partner = partner.Id, AppNo = appNo });
        return rows.Where(r => !r.Closed)
            .Select(r => (Row: r, Due: paper.Contains(r.PayMode ?? "") ? (r.Accepted ? null : "acceptance") : r.PaidOn is null ? "payment" : null))
            .Where(x => x.Due is not null)
            .Select(x => (new PendingRecord(x.Row.AppNo, Masks.Name(x.Row.Name), x.Row.SubmittedOn, Masks.Mobile(x.Row.Mobile ?? ""),
                x.Due!, Masks.Email(x.Row.Email ?? "")), x.Row))
            .ToList();
    }

    // A link goes out only for what the application is waiting on; any link sent
    // before for that purpose stops working. It opens for linkValidityHours.
    public async Task<SentLinkRecord?> SendAsync(string appNo, string purpose, CancellationToken ct = default)
    {
        var waiting = (await WaitingAsync(appNo, ct)).FirstOrDefault();
        if (waiting.Record is null || waiting.Record.Due != purpose) return null;
        var hours = (await reference.ConfigAsync(ct)).LinkValidityHours.GetValueOrDefault(purpose);
        if (hours <= 0) return null;
        await using var connection = await db.OpenAsync(ct);
        var links = LinkTables.For(waiting.Row.Renewal);
        await connection.ExecuteAsync($"""
            INSERT {links} (f_App_No, f_Purpose, f_Url, f_Short_Url, f_Mobile, f_Email, f_Expires_On, f_Sent_By)
            SELECT @AppNo, @Purpose, ISNULL(last.f_Url, ''), ISNULL(last.f_Short_Url, ''), @Mobile, @Email, DATEADD(HOUR, @Hours, SYSDATETIME()), @Partner
            FROM (SELECT 1 AS x) one
            OUTER APPLY (SELECT TOP 1 f_Url, f_Short_Url FROM {links} WHERE f_App_No = @AppNo AND f_Purpose = @Purpose AND f_Active = 1 ORDER BY f_Id DESC) last
            """, new { AppNo = appNo, Purpose = purpose, waiting.Record.Mobile, waiting.Record.Email, Hours = hours, Partner = partner.Id });
        // No SMS or e-mail gateway is wired in yet: the send is logged for now.
        log.LogInformation("{Purpose} link for {AppNo} sent to {Mobile} and {Email}", purpose, appNo, waiting.Record.Mobile, waiting.Record.Email);
        return (await SentAsync(ct)).FirstOrDefault(l => l.AppNo == appNo && l.Purpose == purpose);
    }
}
