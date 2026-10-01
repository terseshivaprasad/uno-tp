using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// A submitted application as the registers see it: the application as submitted
/// (its APR rows), where it has got to since, and the partner's branch.
/// </summary>
internal sealed class SubmittedRow
{
    public const string Select = """
        SELECT m.c_App_No AS AppNo, COALESCE(NULLIF(k.c_Name, N''), m.c_Name) AS Name, m.d_Submitted_On AS SubmittedOn,
            m.d_Accepted_On AS AcceptedOn, m.d_Paid_On AS PaidOn, m.d_Booked_On AS BookedOn, m.d_Cancelled_On AS CancelledOn,
            i.c_App_Type AS AppType, i.n_Amount AS Amount,
            p.c_Pay_Mode AS PayMode, p.c_Cheque_No AS ChequeNo, p.c_Bank_Name AS BankName,
            k.c_Mobile AS Mobile, k.c_Email AS Email, ISNULL(pm.c_Branch, N'') AS Branch
        FROM dbo.t_Unotp_Application_Mst m
        LEFT JOIN dbo.t_Unotp_Investment_Dtls i ON i.c_App_No = m.c_App_No AND i.n_App_Version = m.n_Deposit_Ver AND i.f_Active = 1
        LEFT JOIN dbo.t_Unotp_Payment_Bank_Dtls p ON p.c_App_No = m.c_App_No AND p.n_App_Version = m.n_Payment_Ver AND p.f_Active = 1
        LEFT JOIN dbo.t_Unotp_Kyc_Dtls k ON k.c_App_No = m.c_App_No AND k.n_App_Version = m.n_Details_Ver AND k.c_Holder_Type = '01' AND k.f_Active = 1
        LEFT JOIN dbo.t_Unotp_Partner_Mst pm ON pm.c_User_Id = m.c_Partner_Id
        WHERE m.c_Partner_Id = @Partner AND m.c_Status = 'APR' AND m.f_Active = 1
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
            INSERT dbo.t_Unotp_Pay_In_Slip (c_App_No, c_Slip_No, c_Generated_By)
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
            SubmittedRow.Select + " AND p.c_Pay_Mode IN @Paper" + (appNo is null ? "" : " AND m.c_App_No = @AppNo") + " ORDER BY m.d_Submitted_On DESC",
            new { Partner = partner.Id, Paper = paper, AppNo = appNo });
        var slips = (await connection.QueryAsync<(string AppNo, string SlipNo, DateTime? DepositedOn)>("""
            SELECT s.c_App_No, s.c_Slip_No, s.d_Deposited_On FROM dbo.t_Unotp_Pay_In_Slip s
            WHERE s.n_Id IN (SELECT MAX(n_Id) FROM dbo.t_Unotp_Pay_In_Slip WHERE f_Active = 1 GROUP BY c_App_No)
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
/// The links sent to investors (t_Unotp_Payment_Link), and the submitted applications
/// waiting on one: to pay online, or to accept a digital application paid on paper.
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
    }

    private sealed class SentApplicationRow
    {
        public string AppNo { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime SubmittedOn { get; set; }
        public DateTime? AcceptedOn { get; set; }
        public DateTime? PaidOn { get; set; }
    }

    // The live link for each application and purpose: the latest sent. Those before it
    // no longer open. The partner's submitted applications come from the main database
    // and the links from the links database, so the two are put together here.
    public async Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var applications = (await connection.QueryAsync<SentApplicationRow>("""
            SELECT m.c_App_No AS AppNo, COALESCE(NULLIF(k.c_Name, N''), m.c_Name) AS Name, m.d_Submitted_On AS SubmittedOn,
                m.d_Accepted_On AS AcceptedOn, m.d_Paid_On AS PaidOn
            FROM dbo.t_Unotp_Application_Mst m
            LEFT JOIN dbo.t_Unotp_Kyc_Dtls k ON k.c_App_No = m.c_App_No AND k.n_App_Version = m.n_Details_Ver AND k.c_Holder_Type = '01' AND k.f_Active = 1
            WHERE m.c_Partner_Id = @Partner AND m.f_Active = 1 AND m.d_Submitted_On IS NOT NULL
            """, new { Partner = partner.Id })).ToDictionary(a => a.AppNo);
        if (applications.Count == 0) return [];

        await using var links = await db.OpenAsync(Db.Links, ct);
        var rows = await links.QueryAsync<LinkRow>("""
            SELECT l.c_App_No AS AppNo, l.c_Purpose AS Purpose, l.c_Mobile AS Mobile, l.c_Email AS Email, l.d_Sent_On AS SentOn, l.d_Expires_On AS ExpiresOn
            FROM dbo.t_Unotp_Payment_Link l
            WHERE l.c_App_No IN @AppNos
              AND l.n_Id IN (SELECT MAX(n_Id) FROM dbo.t_Unotp_Payment_Link WHERE f_Active = 1 GROUP BY c_App_No, c_Purpose)
            ORDER BY l.d_Sent_On DESC
            """, new { AppNos = applications.Keys.ToList() });

        var now = DateTime.Now;
        var sent = new List<SentLinkRecord>();
        foreach (var r in rows)
        {
            var a = applications[r.AppNo];
            var done = (r.Purpose == "payment" ? a.PaidOn : a.AcceptedOn) is not null;
            var status = done ? "done" : r.ExpiresOn <= now ? "expired" : "open";
            sent.Add(new SentLinkRecord(r.AppNo, Masks.Name(a.Name), r.Mobile, r.Email, r.Purpose, r.SentOn, r.ExpiresOn, status, a.SubmittedOn));
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
            SubmittedRow.Select + (appNo is null ? "" : " AND m.c_App_No = @AppNo") + " ORDER BY m.d_Submitted_On DESC",
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
        await using var connection = await db.OpenAsync(Db.Links, ct);
        await connection.ExecuteAsync("""
            INSERT dbo.t_Unotp_Payment_Link (c_App_No, c_Purpose, c_Url, c_Short_Url, c_Mobile, c_Email, d_Expires_On, c_Sent_By)
            SELECT @AppNo, @Purpose, ISNULL(last.c_Url, ''), ISNULL(last.c_Short_Url, ''), @Mobile, @Email, DATEADD(HOUR, @Hours, SYSDATETIME()), @Partner
            FROM (SELECT 1 AS x) one
            OUTER APPLY (SELECT TOP 1 c_Url, c_Short_Url FROM dbo.t_Unotp_Payment_Link WHERE c_App_No = @AppNo AND c_Purpose = @Purpose AND f_Active = 1 ORDER BY n_Id DESC) last
            """, new { AppNo = appNo, Purpose = purpose, waiting.Record.Mobile, waiting.Record.Email, Hours = hours, Partner = partner.Id });
        // No SMS or e-mail gateway is wired in yet: the send is logged for now.
        log.LogInformation("{Purpose} link for {AppNo} sent to {Mobile} and {Email}", purpose, appNo, waiting.Record.Mobile, waiting.Record.Email);
        return (await SentAsync(ct)).FirstOrDefault(l => l.AppNo == appNo && l.Purpose == purpose);
    }
}
