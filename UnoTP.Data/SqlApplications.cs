using System.Data;
using System.Text.Json;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>What became of a save made against a version.</summary>
public enum SaveOutcome
{
    Saved,

    /// <summary>The application moved on since that version was read, or is submitted: nothing was saved.</summary>
    Conflict,

    /// <summary>No such application, or not the partner's.</summary>
    NotFound,
}

/// <summary>
/// The partner's applications, kept in SQL Server (db/create_new_tables.sql, db/create_existing_tables.sql).
///
/// A save is checked against the version the page read, under a lock on the
/// application's row, and inserts its section afresh at the next version with
/// status PEN. Submitting writes every section once more, as APR - the
/// application as it was submitted - and locks the rate. A detail row is never
/// updated or deleted, so every earlier save stays on record.
/// </summary>
public sealed class SqlApplications(Db db, IPartner partner, IDepositApi deposits, SqlReference reference, ILogger<SqlApplications> log)
    : IApplicationApi, IRenewalOpener
{
    // ----- Opening -----------------------------------------------------------

    public Task<Application> OpenAsync(Holder holder, CancellationToken ct = default) =>
        OpenAsync(holder, null, null, null, null, ct);

    /// <summary>A renewal's application, with what comes over from the deposit saved on it as its first version.</summary>
    /// <summary>A renewal request cancelled: the application opened from the deposit, while not submitted, is marked cancelled.</summary>
    public async Task<bool> CancelRenewalAsync(string depositNumber)
    {
        await using var connection = await db.OpenAsync(CancellationToken.None);
        var rows = await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Application_Mst
            SET d_Cancelled_On = SYSDATETIME(), c_Sub_Status = 'cancelled', c_Updated_By = @Partner, d_Updated_On = SYSDATETIME()
            WHERE c_Renew_Dep_No = @Number AND c_Partner_Id = @Partner AND d_Submitted_On IS NULL AND d_Cancelled_On IS NULL AND f_Active = 1
            """, new { Number = depositNumber, Partner = partner.Id });
        return rows > 0;
    }

    public Task<Application> OpenRenewalAsync(Holder holder, RenewalOf renewal, UploadState upload, PaymentDetails payment, DepositDetails deposit) =>
        OpenAsync(holder, renewal, upload, payment, deposit, CancellationToken.None);

    private async Task<Application> OpenAsync(Holder holder, RenewalOf? renewal, UploadState? upload, PaymentDetails? payment,
        DepositDetails? deposit, CancellationToken ct)
    {
        var branches = payment is null ? new Dictionary<string, BankBranch>() : await BranchesAsync(payment, ct);
        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var appNo = await NextAppNoAsync(connection, tx, await reference.SettingAsync("appNoPrefix", ct));
        const int version = 1;
        await connection.ExecuteAsync("""
            INSERT dbo.t_Unotp_Application_Mst (c_App_No, c_Partner_Id, c_Status, n_Version,
                n_Upload_Ver, n_Payment_Ver, n_Deposit_Ver,
                c_Pan, d_Dob, c_Name, c_Folio, c_Gender, c_Address, f_Pan_Filed, f_Rec_Pan, f_Rec_Photo, f_Rec_Poa,
                c_Renew_Dep_No, n_Renew_Amount, d_Renew_Matures_On, n_Renew_Rate, n_Renew_Tenure, c_Renew_Payout, c_Created_By)
            VALUES (@AppNo, @Partner, 'PEN', @Version,
                @UploadVer, @PaymentVer, @DepositVer,
                @Pan, @Dob, @Name, @Folio, @Gender, @Address, @PanFiled, @RecPan, @RecPhoto, @RecPoa,
                @RenewDepNo, @RenewAmount, @RenewMaturesOn, @RenewRate, @RenewTenure, @RenewPayout, @Partner)
            """, new
        {
            AppNo = appNo, Partner = partner.Id, Version = version,
            UploadVer = upload is null ? (int?)null : version, PaymentVer = payment is null ? (int?)null : version,
            DepositVer = deposit is null ? (int?)null : version,
            holder.Pan, Dob = Dates.ParseDdMmYyyy(holder.Dob), holder.Name, holder.Folio, holder.Gender, holder.Address, holder.PanFiled,
            RecPan = holder.OnRecord?.Pan, RecPhoto = holder.OnRecord?.Photo, RecPoa = holder.OnRecord?.Poa,
            RenewDepNo = renewal?.DepositNumber, RenewAmount = renewal?.Amount,
            RenewMaturesOn = renewal?.MaturesOn.ToDateTime(TimeOnly.MinValue), RenewRate = renewal?.Rate,
            RenewTenure = renewal?.TenureMonths, RenewPayout = renewal?.Payout,
        }, tx);

        var at = new Stamp(appNo, version, RowStatus.Pending, partner.Id);
        if (upload is not null) await Sections.WriteUploadAsync(connection, tx, at, upload);
        if (payment is not null) await Sections.WritePaymentAsync(connection, tx, at, upload, payment, branches);
        if (deposit is not null) await Sections.WriteDepositAsync(connection, tx, at, upload, deposit, renewal?.DepositNumber, null);
        await tx.CommitAsync(ct);

        return new Application
        {
            AppNo = appNo, Holder = holder, Version = version, Renewal = renewal, Upload = upload, Payment = payment, Deposit = deposit,
        };
    }

    // {appNoPrefix}{yy}F{n}, FBBMFL26F10001: the year, and the next number off the sequence.
    private static async Task<string> NextAppNoAsync(IDbConnection connection, IDbTransaction tx, string prefix) =>
        $"{prefix}{DateTime.Today:yy}F{await connection.ExecuteScalarAsync<long>("SELECT NEXT VALUE FOR dbo.s_App_No", transaction: tx):D5}";

    // ----- Reading -----------------------------------------------------------

    public async Task<Application?> FindAsync(string appNo, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var header = await HeaderAsync(connection, null, appNo, locked: false);
        return header is null ? null : await AssembleAsync(connection, null, header, await CancellationDaysAsync());
    }

    private async Task<HeaderRow?> HeaderAsync(IDbConnection connection, IDbTransaction? tx, string appNo, bool locked) =>
        await connection.QuerySingleOrDefaultAsync<HeaderRow>(
            $"SELECT {HeaderRow.Columns} FROM dbo.t_Unotp_Application_Mst {(locked ? "WITH (UPDLOCK, ROWLOCK)" : "")} " +
            "WHERE c_App_No = @AppNo AND c_Partner_Id = @Partner AND f_Active = 1",
            new { AppNo = appNo, Partner = partner.Id }, tx);

    // The application as it stands: each section from its rows at the version the
    // header holds for it.
    private static async Task<Application> AssembleAsync(IDbConnection connection, IDbTransaction? tx, HeaderRow h, int cancellationDays)
    {
        using var read = await connection.QueryMultipleAsync($"""
            SELECT j_Upload FROM dbo.t_Unotp_Upload_State WHERE c_App_No = @AppNo AND n_App_Version = @UploadVer AND f_Active = 1;
            SELECT {KycRow.Columns} FROM dbo.t_Unotp_Kyc_Dtls WHERE c_App_No = @AppNo AND n_App_Version = @DetailsVer AND f_Active = 1 ORDER BY c_Holder_Type;
            SELECT {AddressRow.Columns} FROM dbo.t_Unotp_Address_Dtls WHERE c_App_No = @AppNo AND n_App_Version = @DetailsVer AND f_Active = 1;
            SELECT {NomineeRow.Columns} FROM dbo.t_Unotp_Nominee_Dtls WHERE c_App_No = @AppNo AND n_App_Version = @DetailsVer AND f_Active = 1;
            SELECT {PaymentBankRow.Columns} FROM dbo.t_Unotp_Payment_Bank_Dtls WHERE c_App_No = @AppNo AND n_App_Version = @PaymentVer AND f_Active = 1;
            SELECT {RepaymentBankRow.Columns} FROM dbo.t_Unotp_Bank_Dtls WHERE c_App_No = @AppNo AND n_App_Version = @PaymentVer AND f_Active = 1;
            SELECT {InvestmentRow.Columns} FROM dbo.t_Unotp_Investment_Dtls WHERE c_App_No = @AppNo AND n_App_Version = @DepositVer AND f_Active = 1;
            SELECT c_Page AS Page, j_State AS State FROM dbo.t_Unotp_Page_State WHERE c_App_No = @AppNo AND f_Active = 1;
            """, new { h.AppNo, h.UploadVer, h.DetailsVer, h.PaymentVer, h.DepositVer }, tx);

        var uploadJson = await read.ReadSingleOrDefaultAsync<string>();
        var kyc = (await read.ReadAsync<KycRow>()).ToList();
        var addresses = (await read.ReadAsync<AddressRow>()).ToList();
        var nominee = await read.ReadSingleOrDefaultAsync<NomineeRow>();
        var pay = await read.ReadSingleOrDefaultAsync<PaymentBankRow>();
        var repay = await read.ReadSingleOrDefaultAsync<RepaymentBankRow>();
        var deposit = await read.ReadSingleOrDefaultAsync<InvestmentRow>();
        var pages = (await read.ReadAsync<(string Page, string State)>()).ToDictionary(p => p.Page, p => p.State);

        return new Application
        {
            AppNo = h.AppNo,
            Holder = HolderOf(h),
            Version = h.Version,
            Upload = uploadJson is null ? null : JsonSerializer.Deserialize<UploadState>(uploadJson, Sections.Json),
            Details = h.DetailsVer is null ? null : DetailsOf(kyc, addresses, nominee),
            Payment = pay is null || repay is null ? null : PaymentOf(pay, repay),
            Deposit = deposit is null ? null : new DepositDetails(deposit.Amount, deposit.TenureMonths, deposit.Payout,
                deposit.AutoRenewal, deposit.RenewInstruction, deposit.NoTds, deposit.DeliveryType,
                deposit.SourceOfFunds, deposit.SourceOfFundsRemark),
            Submitted = SubmissionOf(h, cancellationDays),
            Renewal = h.RenewDepNo is null ? null : new RenewalOf(h.RenewDepNo, h.RenewAmount ?? 0,
                DateOnly.FromDateTime(h.RenewMaturesOn ?? DateTime.MinValue), h.RenewRate ?? 0, h.RenewTenure ?? 0, h.RenewPayout ?? ""),
            Pages = pages,
        };
    }

    private static Holder HolderOf(HeaderRow h) =>
        new(h.Pan, Dates.FromDb(h.Dob), h.Name, h.Folio, h.PanFiled, h.Address,
            h.RecPan is null ? null : new DocsOnRecord(h.RecPan.Value, h.RecPhoto ?? false, h.RecPoa ?? false), h.Gender);

    private static ApplicationDetails DetailsOf(List<KycRow> kyc, List<AddressRow> addresses, NomineeRow? n) => new()
    {
        Holders = kyc.Select(k =>
        {
            var c = addresses.FirstOrDefault(a => a.HolderType == k.HolderType && a.AddrType == "COR");
            return new HolderDetails(k.HolderType, k.Gender, k.NameType, k.ParentName, k.AnnualIncome, k.Occupation,
                k.SubOccupation, k.MaritalStatus, k.Mobile, k.Email, k.FatcaTaxResident, k.FatcaPermanentResident, k.Pep, k.PepRelated,
                c is null ? null : new TypedAddress(c.Line1, c.Line2, c.Line3, c.City, c.PinCode, c.District, c.State));
        }).ToList(),
        Nominee = n is null ? null : new NomineeDetails(n.Name, Dates.FromDb(n.Dob), n.Relation, n.GuardianName,
            n.GuardianLine1, n.GuardianLine2, n.GuardianLine3, n.GuardianPinCode, n.GuardianCity),
    };

    // An account with neither an IFSC nor a number was not given.
    private static PaymentDetails PaymentOf(PaymentBankRow pay, RepaymentBankRow repay) => new(
        pay.Ifsc is null && pay.AccountNo is null ? null : new BankAccount(pay.Ifsc ?? "", pay.AccountNo ?? ""),
        repay.Ifsc is null && repay.AccountNo is null ? null : new BankAccount(repay.Ifsc ?? "", repay.AccountNo ?? ""),
        repay.SameAsPayment,
        pay.ChequeNo is null ? null : new ChequeDetails(pay.ChequeNo, Dates.FromDb(pay.ChequeDate), pay.CmsLocation ?? ""));

    private static Submission? SubmissionOf(HeaderRow h, int cancellationDays) => h.SubmittedOn is not { } at ? null
        : new Submission(at, h.SubStatus ?? "", h.LinkSentTo ?? "", h.LinkValidUntil ?? at, h.ResendsLeft ?? 0,
            h.LinkEmailedTo ?? "", h.ShortUrl ?? "", RegenerateUntil: h.CreatedOn.AddDays(cancellationDays));

    // Days after its creation an unpaid application cancels itself: until then a new link may be sent.
    private async Task<int> CancellationDaysAsync() => (await reference.ConfigAsync(CancellationToken.None)).CancellationDays;

    // ----- Saving a step -----------------------------------------------------

    public async Task<int?> SaveUploadAsync(string appNo, int version, UploadState upload, CancellationToken ct = default) =>
        Version(await SaveUploadOutcomeAsync(appNo, version, upload, ct));

    public async Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default) =>
        Version(await SaveAsync(appNo, version, (c, tx, h, at, u) => Sections.WriteDetailsAsync(c, tx, at, HolderOf(h), u, details), "n_Details_Ver", ct));

    public async Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default) =>
        Version(await SavePaymentOutcomeAsync(appNo, version, payment, ct));

    public async Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default) =>
        Version(await SaveAsync(appNo, version, (c, tx, h, at, u) => Sections.WriteDepositAsync(c, tx, at, u, deposit, h.RenewDepNo, null), "n_Deposit_Ver", ct));

    // The routes, which answer 404 and 409 apart, call these; the interface folds both into null.
    public async Task<(SaveOutcome, int?)> SaveUploadOutcomeAsync(string appNo, int version, UploadState upload, CancellationToken ct) =>
        await SaveAsync(appNo, version, (c, tx, _, at, _) => Sections.WriteUploadAsync(c, tx, at, upload), "n_Upload_Ver", ct);

    public async Task<(SaveOutcome, int?)> SavePaymentOutcomeAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct)
    {
        var branches = await BranchesAsync(payment, ct);
        return await SaveAsync(appNo, version, (c, tx, _, at, u) => Sections.WritePaymentAsync(c, tx, at, u, payment, branches), "n_Payment_Ver", ct);
    }

    private static int? Version((SaveOutcome Outcome, int? Version) saved) => saved.Outcome == SaveOutcome.Saved ? saved.Version : null;

    private delegate Task Write(IDbConnection connection, IDbTransaction tx, HeaderRow header, Stamp at, UploadState? upload);

    // One section saved against the version the page read: the application's row
    // is locked, its version checked, moved on, and the section written at it.
    private async Task<(SaveOutcome, int?)> SaveAsync(string appNo, int version, Write write, string sectionColumn, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var header = await HeaderAsync(connection, tx, appNo, locked: true);
        if (header is null) return (SaveOutcome.NotFound, null);
        if (header.Version != version || header.Submitted) return (SaveOutcome.Conflict, null);

        var next = header.Version + 1;
        var upload = await UploadAsync(connection, tx, header);
        await write(connection, tx, header, new Stamp(appNo, next, RowStatus.Pending, partner.Id), upload);
        await connection.ExecuteAsync(
            $"UPDATE dbo.t_Unotp_Application_Mst SET n_Version = @Next, {sectionColumn} = @Next, c_Updated_By = @Partner, d_Updated_On = SYSDATETIME() WHERE c_App_No = @AppNo",
            new { Next = next, Partner = partner.Id, AppNo = appNo }, tx);
        await tx.CommitAsync(ct);
        return (SaveOutcome.Saved, next);
    }

    private static async Task<UploadState?> UploadAsync(IDbConnection connection, IDbTransaction tx, HeaderRow header) =>
        header.UploadVer is null ? null
            : await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT j_Upload FROM dbo.t_Unotp_Upload_State WHERE c_App_No = @AppNo AND n_App_Version = @UploadVer AND f_Active = 1",
                new { header.AppNo, header.UploadVer }, tx) is { } json
                ? JsonSerializer.Deserialize<UploadState>(json, Sections.Json)
                : null;

    // Each IFSC's branch, so the rows name the bank; one not found is saved without.
    private async Task<Dictionary<string, BankBranch>> BranchesAsync(PaymentDetails payment, CancellationToken ct)
    {
        Dictionary<string, BankBranch> found = [];
        foreach (var ifsc in new[] { payment.Payment?.Ifsc, payment.Repayment?.Ifsc }.OfType<string>().Select(i => i.Trim().ToUpperInvariant()).Distinct())
        {
            if (ifsc.Length > 0 && await deposits.BranchAsync(ifsc, ct) is { } branch) found[ifsc] = branch;
        }
        return found;
    }

    public async Task<bool> SavePageAsync(string appNo, string page, string state, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync("""
            MERGE dbo.t_Unotp_Page_State WITH (HOLDLOCK) AS t
            USING (SELECT c_App_No FROM dbo.t_Unotp_Application_Mst WHERE c_App_No = @AppNo AND c_Partner_Id = @Partner AND f_Active = 1) AS a
                ON t.c_App_No = a.c_App_No AND t.c_Page = @Page
            WHEN MATCHED THEN UPDATE SET j_State = @State, d_Updated_On = SYSDATETIME()
            WHEN NOT MATCHED THEN INSERT (c_App_No, c_Page, j_State) VALUES (a.c_App_No, @Page, @State);
            """, new { AppNo = appNo, Partner = partner.Id, Page = page, State = state }) > 0;
    }

    // ----- Submitting ------------------------------------------------------------

    public async Task<Application?> SubmitAsync(string appNo, int version, PaymentLink? link = null, CancellationToken ct = default) =>
        (await SubmitOutcomeAsync(appNo, version, link, ct)).Application;

    /// <summary>
    /// Every section written once more, as APR, at the next version: the application
    /// as submitted, with the rate locked on it. The payment link goes to the
    /// investor's mobile and e-mail, open for linkValidityHours["payment"].
    /// </summary>
    public async Task<(SaveOutcome Outcome, Application? Application)> SubmitOutcomeAsync(string appNo, int version, PaymentLink? link, CancellationToken ct)
    {
        // The quote is asked before the lock: a renewal takes the rate on its maturity date.
        var seen = await FindAsync(appNo, ct);
        if (seen is null) return (SaveOutcome.NotFound, null);
        if (seen.Version != version || seen.Submitted is not null) return (SaveOutcome.Conflict, null);
        var quote = seen.Deposit is { } d
            ? await deposits.QuoteAsync(new QuoteRequest(d.Amount, d.TenureMonths, d.Payout, seen.RateCardRequest(seen.Upload?.Category ?? "")), ct)
            : null;
        var hours = (await reference.ConfigAsync(ct)).LinkValidityHours.GetValueOrDefault("payment");
        var resends = await reference.NumberAsync("linkResends", ct);
        var branches = seen.Payment is null ? [] : await BranchesAsync(seen.Payment, ct);

        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var header = await HeaderAsync(connection, tx, appNo, locked: true);
        if (header is null) return (SaveOutcome.NotFound, null);
        if (header.Version != version || header.Submitted) return (SaveOutcome.Conflict, null);

        // Read again under the lock, so what is written as submitted is what stands.
        var app = await AssembleAsync(connection, tx, header, await CancellationDaysAsync());
        var next = header.Version + 1;
        var at = new Stamp(appNo, next, RowStatus.Approved, partner.Id);
        if (app.Upload is { } upload) await Sections.WriteUploadAsync(connection, tx, at, upload);
        if (app.Details is { } details) await Sections.WriteDetailsAsync(connection, tx, at, app.Holder, app.Upload, details);
        if (app.Payment is { } payment) await Sections.WritePaymentAsync(connection, tx, at, app.Upload, payment, branches);
        if (app.Deposit is { } deposit) await Sections.WriteDepositAsync(connection, tx, at, app.Upload, deposit, header.RenewDepNo, quote);

        // Submitted on the database's clock, as every row on the application is dated.
        var investor = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
        var submitted = await connection.QuerySingleAsync<HeaderRow>($"""
            UPDATE dbo.t_Unotp_Application_Mst SET c_Status = 'APR', n_Version = @Next,
                n_Upload_Ver = CASE WHEN n_Upload_Ver IS NULL THEN NULL ELSE @Next END,
                n_Details_Ver = CASE WHEN n_Details_Ver IS NULL THEN NULL ELSE @Next END,
                n_Payment_Ver = CASE WHEN n_Payment_Ver IS NULL THEN NULL ELSE @Next END,
                n_Deposit_Ver = CASE WHEN n_Deposit_Ver IS NULL THEN NULL ELSE @Next END,
                d_Submitted_On = SYSDATETIME(), c_Sub_Status = 'payment-pending',
                c_Link_Sent_To = @LinkSentTo, c_Link_Emailed_To = @LinkEmailedTo,
                d_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), n_Resends_Left = @Resends, c_Short_Url = @ShortUrl,
                c_Updated_By = @Partner, d_Updated_On = SYSDATETIME()
            OUTPUT {Inserted}
            WHERE c_App_No = @AppNo
            """, new
        {
            Next = next, LinkSentTo = Masks.Mobile(investor?.Mobile ?? ""), LinkEmailedTo = Masks.Email(investor?.Email ?? ""),
            Hours = hours, Resends = resends, ShortUrl = link?.ShortUrl ?? "", Partner = partner.Id, AppNo = appNo,
        }, tx);
        var submission = SubmissionOf(submitted, await CancellationDaysAsync())!;
        await tx.CommitAsync(ct);
        await RecordLinkAsync(appNo, link?.Url ?? "", link?.ShortUrl ?? "", submission, ct);

        // No SMS or e-mail gateway is wired in yet: the link is logged for now.
        log.LogInformation("Payment link for {AppNo} to {Mobile} and {Email}: {Link}", appNo, submission.LinkSentTo,
            submission.LinkEmailedTo, link?.ShortUrl ?? link?.Url ?? "(the backend's own)");

        app.Version = next;
        app.Submitted = submission;
        return (SaveOutcome.Saved, app);
    }

    // The header's columns as an UPDATE leaves them.
    private static readonly string Inserted = string.Join(", ", HeaderRow.Columns.Split(',').Select(c => "inserted." + c.Trim()));

    // The payment link as sent, on record in t_Unotp_Payment_Link (the links database):
    // every send is a row. Written once the application's own transaction is committed.
    private async Task RecordLinkAsync(string appNo, string url, string shortUrl, Submission sent, CancellationToken ct)
    {
        await using var links = await db.OpenAsync(Db.Links, ct);
        await links.ExecuteAsync("""
            INSERT dbo.t_Unotp_Payment_Link (c_App_No, c_Purpose, c_Url, c_Short_Url, c_Mobile, c_Email, d_Expires_On, c_Sent_By)
            VALUES (@AppNo, 'payment', @Url, @ShortUrl, @Mobile, @Email, @ExpiresOn, @Partner)
            """, new { AppNo = appNo, Url = url, ShortUrl = shortUrl, Mobile = sent.LinkSentTo, Email = sent.LinkEmailedTo,
                ExpiresOn = sent.LinkValidUntil, Partner = partner.Id });
    }

    // The last payment link sent for an application: its addresses are sent again as they are.
    private async Task<(string Url, string ShortUrl)> LastLinkAsync(string appNo, CancellationToken ct)
    {
        await using var links = await db.OpenAsync(Db.Links, ct);
        var last = await links.QuerySingleOrDefaultAsync<(string Url, string ShortUrl)>("""
            SELECT TOP 1 c_Url, c_Short_Url FROM dbo.t_Unotp_Payment_Link WHERE c_App_No = @AppNo AND c_Purpose = 'payment' AND f_Active = 1 ORDER BY n_Id DESC
            """, new { AppNo = appNo });
        return (last.Url ?? "", last.ShortUrl ?? "");
    }

    // A new link, allowed until cancellationDays after the application was created,
    // while it is unpaid and not cancelled; it runs for the full validity again.
    public async Task<Submission?> ResendLinkAsync(string appNo, CancellationToken ct = default)
    {
        var days = await CancellationDaysAsync();
        var hours = (await reference.ConfigAsync(ct)).LinkValidityHours.GetValueOrDefault("payment");
        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var header = await connection.QuerySingleOrDefaultAsync<HeaderRow>($"""
            UPDATE dbo.t_Unotp_Application_Mst
            SET d_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), c_Updated_By = @Partner, d_Updated_On = SYSDATETIME()
            OUTPUT {Inserted}
            WHERE c_App_No = @AppNo AND c_Partner_Id = @Partner AND c_Status = 'APR' AND f_Active = 1
              AND d_Paid_On IS NULL AND d_Cancelled_On IS NULL AND d_Created_On >= DATEADD(DAY, -@Days, SYSDATETIME())
            """, new { AppNo = appNo, Partner = partner.Id, Hours = hours, Days = days }, tx);
        if (header is null) return null;
        var submission = SubmissionOf(header, days)!;
        await tx.CommitAsync(ct);
        var last = await LastLinkAsync(appNo, ct);
        await RecordLinkAsync(appNo, last.Url, last.ShortUrl, submission, ct);
        log.LogInformation("Payment link for {AppNo} sent again to {Mobile} and {Email}", appNo, submission.LinkSentTo, submission.LinkEmailedTo);
        return submission;
    }

    // ----- Lists -----------------------------------------------------------------

    // The investor's name as far as the application knows it: as NSDL verified it,
    // else as read off the PAN copy (or typed over it), else as the folio has it.
    // Empty for an investor with no folio whose PAN copy is not read yet.
    private const string KnownName = """
        COALESCE(NULLIF(JSON_VALUE(u.j_Upload, '$.name'), ''), NULLIF(JSON_VALUE(u.j_Upload, '$.nsdlName'), ''), m.c_Name)
        """;

    // Kept for draftDays from the last save.
    public async Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default)
    {
        var days = (await reference.ConfigAsync(ct)).DraftDays;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<DraftRow>($"""
            SELECT m.c_App_No AS AppNo, {KnownName} AS Name, m.c_Pan AS Pan, m.d_Dob AS Dob, ISNULL(i.n_Amount, 0) AS Amount,
                m.n_Upload_Ver AS UploadVer, m.n_Details_Ver AS DetailsVer, m.n_Payment_Ver AS PaymentVer, m.n_Deposit_Ver AS DepositVer,
                -- On the database's clock, where the time was written; turned into the app's below.
                DATEDIFF(MINUTE, COALESCE(m.d_Updated_On, m.d_Created_On), SYSDATETIME()) AS MinutesAgo
            FROM dbo.t_Unotp_Application_Mst m
            LEFT JOIN dbo.t_Unotp_Investment_Dtls i ON i.c_App_No = m.c_App_No AND i.n_App_Version = m.n_Deposit_Ver AND i.f_Active = 1
            LEFT JOIN dbo.t_Unotp_Upload_State u ON u.c_App_No = m.c_App_No AND u.n_App_Version = m.n_Upload_Ver AND u.f_Active = 1
            WHERE m.c_Partner_Id = @Partner AND m.c_Status = 'PEN' AND m.f_Active = 1
              AND COALESCE(m.d_Updated_On, m.d_Created_On) > DATEADD(DAY, -@Days, SYSDATETIME())
            ORDER BY COALESCE(m.d_Updated_On, m.d_Created_On) DESC
            """, new { Partner = partner.Id, Days = days });
        var now = DateTime.Now;
        return rows.Select(r =>
        {
            var (done, next) = DraftSummary.Progress(r.UploadVer is not null, r.DetailsVer is not null, r.PaymentVer is not null, r.DepositVer is not null);
            return new DraftSummary(r.AppNo, Masks.Name(r.Name), Masks.Pan(r.Pan), Masks.Dob(Dates.FromDb(r.Dob)), r.Amount,
                done, next, now.AddMinutes(-Math.Max(0, r.MinutesAgo)));
        }).ToList();
    }

    public async Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default)
    {
        var payouts = (await reference.ReferenceAsync(ct)).Payouts;
        await using var connection = await db.OpenAsync(ct);
        var rows = (await connection.QueryAsync<ListRow>($"""
            SELECT m.c_App_No AS AppNo, m.c_Status AS Status, m.c_Folio AS Folio, {KnownName} AS Name, m.c_Pan AS Pan, m.d_Dob AS Dob,
                i.n_Amount AS Amount, i.n_Tenure_Months AS TenureMonths, i.c_Payout AS Payout,
                JSON_VALUE(u.j_Upload, '$.appType') AS AppType, JSON_VALUE(u.j_Upload, '$.payMode') AS PayMode,
                ISNULL((SELECT COUNT(*) FROM OPENJSON(u.j_Upload, '$.joint')), 0) AS JointHolders,
                m.n_Upload_Ver AS UploadVer, m.n_Details_Ver AS DetailsVer, m.n_Payment_Ver AS PaymentVer, m.n_Deposit_Ver AS DepositVer,
                m.d_Created_On AS CreatedOn, m.d_Submitted_On AS SubmittedOn,
                m.d_Accepted_On AS AcceptedOn, m.d_Paid_On AS PaidOn, m.d_Booked_On AS BookedOn, m.c_Fdr_No AS FdrNo,
                m.d_Cancelled_On AS CancelledOn, ISNULL(pm.c_Branch, N'') AS Branch,
                (SELECT MIN(f.d_Created_On) FROM dbo.t_Unotp_Upload_State f WHERE f.c_App_No = m.c_App_No AND f.f_Active = 1) AS UploadedOn,
                (SELECT MIN(p.d_Generated_On) FROM dbo.t_Unotp_Pay_In_Slip p WHERE p.c_App_No = m.c_App_No AND p.f_Active = 1) AS SlipOn,
                m.d_Penny_Drop_On AS PennyDropOn, m.c_Penny_Drop_Status AS PennyDropStatus,
                m.d_Kyc_Verified_On AS KycVerifiedOn, m.c_Kyc_Status AS KycStatus
            FROM dbo.t_Unotp_Application_Mst m
            LEFT JOIN dbo.t_Unotp_Investment_Dtls i ON i.c_App_No = m.c_App_No AND i.n_App_Version = m.n_Deposit_Ver AND i.f_Active = 1
            LEFT JOIN dbo.t_Unotp_Upload_State u ON u.c_App_No = m.c_App_No AND u.n_App_Version = m.n_Upload_Ver AND u.f_Active = 1
            LEFT JOIN dbo.t_Unotp_Partner_Mst pm ON pm.c_User_Id = m.c_Partner_Id
            WHERE m.c_Partner_Id = @Partner AND m.f_Active = 1
            ORDER BY m.d_Created_On DESC
            """, new { Partner = partner.Id })).ToList();

        // When each application's first link went out, from the links database.
        if (rows.Count > 0)
        {
            await using var links = await db.OpenAsync(Db.Links, ct);
            var firstSent = (await links.QueryAsync<(string AppNo, DateTime SentOn)>("""
                SELECT c_App_No, MIN(d_Sent_On) FROM dbo.t_Unotp_Payment_Link
                WHERE f_Active = 1 AND c_App_No IN @AppNos GROUP BY c_App_No
                """, new { AppNos = rows.Select(r => r.AppNo).ToList() })).ToDictionary(x => x.AppNo, x => x.SentOn);
            foreach (var row in rows)
            {
                if (firstSent.TryGetValue(row.AppNo, out var sentOn)) row.LinkSentOn = sentOn;
            }
        }

        return rows.Select(r =>
        {
            var payout = payouts.FirstOrDefault(p => p.Code == r.Payout);
            var digital = r.AppType != "PHYSICAL";
            return new ApplicationRecord(
                r.AppNo, r.Folio.Length > 0 ? r.Folio : null, Masks.Name(r.Name), Masks.Pan(r.Pan),
                r.Amount ?? 0, payout?.PerYear == 0, r.TenureMonths ?? 0, payout?.Name ?? r.Payout ?? "",
                1 + r.JointHolders, r.SubmittedOn ?? r.CreatedOn, digital, r.PayMode ?? "", r.Branch,
                StateOf(r), r.FdrNo, StepOf(r), "",
                ApplicationStages.Of(digital, r.PayMode ?? "", r.CreatedOn, r.SubmittedOn, r.LinkSentOn, r.AcceptedOn,
                    r.SlipOn, r.PennyDropOn, r.PennyDropStatus, r.PaidOn, r.KycVerifiedOn, r.KycStatus,
                    r.BookedOn, r.FdrNo, r.CancelledOn));
        }).ToList();
    }

    // Where an application stands, as far as what has happened to it says.
    private static string StateOf(ListRow r) =>
        r.CancelledOn is not null ? "cancelled"
        : r.BookedOn is not null ? "booked"
        : r.PaidOn is not null ? "review"
        : r.Status == RowStatus.Approved ? "awaiting"
        : "progress";

    // The step an unfinished application stopped on; a submitted one waits on payment.
    private static string StepOf(ListRow r) =>
        r.Status == RowStatus.Approved ? "Payment"
        : r.UploadVer is null ? "Upload Documents"
        : r.DetailsVer is null ? "Investor Information"
        : r.PaymentVer is null ? "Bank Details & Payment"
        : r.DepositVer is null ? "FD Configuration"
        : "Review Summary";
}
