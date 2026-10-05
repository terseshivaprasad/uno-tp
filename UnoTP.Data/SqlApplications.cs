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
/// The partner's applications, kept in SQL Server (db/create_tables.sql).
///
/// A save is checked against the version the page read, under a lock on the
/// application's row, and inserts its section afresh at the next version with
/// status PEN. Submitting writes every section once more, as APR - the
/// application as it was submitted - and locks the rate. A detail row is never
/// updated or deleted, so every earlier save stays on record.
/// </summary>
public sealed class SqlApplications(Db db, IPartner partner, IDepositApi deposits, SqlReference reference, IConfiguration config, ILogger<SqlApplications> log)
    : IApplicationApi, IRenewalOpener
{
    // The document store's root, recorded with each copy's path (f_Doc_Filepath).
    private readonly string dmsRoot = DmsPaths.Root(config);

    // ----- Opening -----------------------------------------------------------

    public Task<Application> OpenAsync(Holder holder, CancellationToken ct = default) =>
        OpenAsync(holder, null, null, null, null, ct);

    /// <summary>A renewal's application, with what comes over from the deposit saved on it as its first version.</summary>
    /// <summary>
    /// A renewal request cancelled: the application opened from the deposit is marked
    /// cancelled. A draft can be; a submitted one only while it is a digital
    /// application the investor has not accepted. A physical one, once submitted, cannot.
    /// </summary>
    public async Task<bool> CancelRenewalAsync(string depositNumber)
    {
        await using var connection = await db.OpenAsync(CancellationToken.None);
        var rows = await connection.ExecuteAsync("""
            UPDATE m
            SET f_Cancelled_On = SYSDATETIME(), f_Sub_Status = 'cancelled', f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
            FROM dbo.t_Unotp_Application_Mst m
            WHERE m.f_Renew_Dep_No = @Number AND m.f_Partner_Id = @Partner AND m.f_Cancelled_On IS NULL AND m.f_Active = 1
              AND (m.f_Submitted_On IS NULL
                   OR (m.f_Accepted_On IS NULL AND EXISTS (
                        SELECT 1 FROM dbo.t_FD_BT_Investment_Dtl i
                        WHERE i.f_Appl_No = m.f_App_No AND i.f_Active = 1 AND i.f_ApplicationDeclarationType = @Digital)))
            """, new { Number = depositNumber, Partner = partner.Id, Digital = ApplicationType.Digital });
        return rows > 0;
    }

    public Task<Application> OpenRenewalAsync(Holder holder, RenewalOf renewal, UploadState upload, PaymentDetails payment, DepositDetails deposit) =>
        OpenAsync(holder, renewal, upload, payment, deposit, CancellationToken.None);

    private async Task<Application> OpenAsync(Holder holder, RenewalOf? renewal, UploadState? upload, PaymentDetails? payment,
        DepositDetails? deposit, CancellationToken ct)
    {
        var branches = payment is null ? new Dictionary<string, BankBranch>() : await BranchesAsync(payment, ct);
        var documentCodes = await reference.DocumentCodesAsync(ct);
        var lists = await reference.ReferenceAsync(ct);

        // Not numbered yet: only whose rate card the deposit takes is read off this.
        var unnumbered = new Application { AppNo = "", Holder = holder, Renewal = renewal, Upload = upload };
        var line = deposit is null ? null : await RateLineAsync(unnumbered, deposit, ct);
        var asking = await AskingAsync(ct);

        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var appNo = await ApplicationNumber.NextAsync(connection, tx, asking, renewal is not null);
        const int version = 1;
        await connection.ExecuteAsync("""
            INSERT dbo.t_Unotp_Application_Mst (f_App_No, f_Partner_Id, f_Status, f_Version,
                f_Upload_Ver, f_Payment_Ver, f_Deposit_Ver,
                f_Pan, f_Dob, f_Name, f_Folio, f_Gender, f_Address, f_Data_Source, f_Pan_Filed, f_Rec_Pan, f_Rec_Photo, f_Rec_Poa,
                f_Renew_Dep_No, f_Renew_Amount, f_Renew_Matures_On, f_Renew_Rate, f_Renew_Tenure, f_Renew_Payout, f_Renew_Principal, f_Created_By)
            VALUES (@AppNo, @Partner, 'PEN', @Version,
                @UploadVer, @PaymentVer, @DepositVer,
                @Pan, @Dob, @Name, @Folio, @Gender, @Address, @Source, @PanFiled, @RecPan, @RecPhoto, @RecPoa,
                @RenewDepNo, @RenewAmount, @RenewMaturesOn, @RenewRate, @RenewTenure, @RenewPayout, @RenewPrincipal, @Partner)
            """, new
        {
            AppNo = appNo, Partner = partner.Id, Version = version,
            UploadVer = upload is null ? (int?)null : version, PaymentVer = payment is null ? (int?)null : version,
            DepositVer = deposit is null ? (int?)null : version,
            holder.Pan, Dob = Dates.ParseDdMmYyyy(holder.Dob), holder.Name, holder.Folio, holder.Gender, holder.Address, holder.Source, holder.PanFiled,
            RecPan = holder.OnRecord?.Pan, RecPhoto = holder.OnRecord?.Photo, RecPoa = holder.OnRecord?.Poa,
            RenewDepNo = renewal?.DepositNumber, RenewAmount = renewal?.Amount,
            RenewMaturesOn = renewal?.MaturesOn.ToDateTime(TimeOnly.MinValue), RenewRate = renewal?.Rate,
            RenewTenure = renewal?.TenureMonths, RenewPayout = renewal?.Payout, RenewPrincipal = renewal?.Principal,
        }, tx);

        var at = StampAt(appNo, version, RowStatus.Pending, holder.Folio);
        if (upload is not null) await Sections.WriteUploadAsync(connection, tx, at, upload, documentCodes, dmsRoot);
        if (payment is not null) await Sections.WritePaymentAsync(connection, tx, at, upload, payment, branches);
        if (deposit is not null) await Sections.WriteDepositAsync(connection, tx, at, upload, deposit, renewal?.DepositNumber, line, lists);
        await tx.CommitAsync(ct);

        return new Application
        {
            AppNo = appNo, Holder = holder, Version = version, Renewal = renewal, Upload = upload, Payment = payment, Deposit = deposit,
        };
    }

    // Who the application number is asked for. A branch user is one of the sourcing
    // agency's (the sourcingAgency setting); anyone else is a partner.
    private async Task<ApplicationNumber.AskedBy> AskingAsync(CancellationToken ct)
    {
        var branchUser = await BranchUserAsync(ct);
        return new ApplicationNumber.AskedBy(partner.UserClusterId, partner.UserName, partner.AgencyCode, partner.IpAddress, SessionNumber(), branchUser);
    }

    // ----- Reading -----------------------------------------------------------

    public async Task<Application?> FindAsync(string appNo, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var header = await HeaderAsync(connection, null, appNo, locked: false);
        return header is null ? null : await AssembleAsync(connection, null, header, await CancellationDaysAsync(), await reference.ReferenceAsync(ct));
    }

    private async Task<HeaderRow?> HeaderAsync(IDbConnection connection, IDbTransaction? tx, string appNo, bool locked) =>
        await connection.QuerySingleOrDefaultAsync<HeaderRow>(
            $"SELECT {HeaderRow.Columns} FROM dbo.t_Unotp_Application_Mst {(locked ? "WITH (UPDLOCK, ROWLOCK)" : "")} " +
            "WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1",
            new { AppNo = appNo, Partner = partner.Id }, tx);

    // The application as it stands: each section from its active rows in the FD
    // system's tables, and the app's own rows at the version the header holds.
    private static async Task<Application> AssembleAsync(IDbConnection connection, IDbTransaction? tx, HeaderRow h, int cancellationDays,
        ReferenceData lists)
    {
        using var read = await connection.QueryMultipleAsync($"""
            SELECT f_Upload FROM dbo.t_Unotp_Upload_State WHERE f_App_No = @AppNo AND f_App_Version = @UploadVer AND f_Active = 1;
            SELECT {KycRow.Columns} FROM dbo.t_FD_BT_Kyc_Data_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Holder_Type;
            SELECT {AddressRow.Columns} FROM dbo.t_FD_BT_Address_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1;
            SELECT TOP (1) {NomineeRow.Columns} FROM dbo.t_FD_BT_Nominee_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Nominee_Dtl_Id DESC;
            SELECT TOP (1) {PaymentBankRow.Columns} FROM dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC;
            SELECT TOP (1) {RepaymentBankRow.Columns} FROM dbo.t_FD_BT_Investor_Bank_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC;
            SELECT TOP (1) {InvestmentRow.Columns} FROM dbo.t_FD_BT_Investment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Investment_Dtl_Id DESC;
            SELECT f_Page AS Page, f_State AS State FROM dbo.t_Unotp_Page_State WHERE f_App_No = @AppNo AND f_Active = 1;
            """, new { h.AppNo, h.UploadVer, h.DetailsVer, h.PaymentVer, h.DepositVer }, tx);

        var uploadJson = await read.ReadSingleOrDefaultAsync<string>();
        var upload = uploadJson is null ? null : JsonSerializer.Deserialize<UploadState>(uploadJson, Sections.Json);
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
            Upload = upload,
            Details = h.DetailsVer is null ? null : DetailsOf(kyc, addresses, nominee, HolderOf(h), upload, lists.Masters ?? MasterLists.None),
            Payment = pay is null || repay is null ? null : PaymentOf(pay, repay),
            Deposit = deposit is null ? null : new DepositDetails(deposit.Amount, deposit.TenureMonths, deposit.Payout,
                deposit.AutoRenewal, deposit.RenewInstruction, deposit.NoTds, deposit.DeliveryType,
                deposit.SourceOfFunds, deposit.SourceOfFundsRemark, deposit.SourceOfFundsReason, deposit.RenewalFor),
            Submitted = SubmissionOf(h, cancellationDays),
            Cancelled = h.Cancelled,
            Renewal = h.RenewDepNo is null ? null : new RenewalOf(h.RenewDepNo, h.RenewAmount ?? 0,
                DateOnly.FromDateTime(h.RenewMaturesOn ?? DateTime.MinValue), h.RenewRate ?? 0, h.RenewTenure ?? 0, h.RenewPayout ?? "",
                h.RenewPrincipal ?? 0),
            Pages = pages,
        };
    }

    private static Holder HolderOf(HeaderRow h) =>
        new(h.Pan, Dates.FromDb(h.Dob), h.Name, h.Folio, h.PanFiled, h.Address,
            h.RecPan is null ? null : new DocsOnRecord(h.RecPan.Value, h.RecPhoto ?? false, h.RecPoa ?? false), h.Gender, h.DataSource ?? "");

    // The marital status and the nominee's relation are kept as the FD system's codes,
    // and read back as the names the pages offer.
    private static ApplicationDetails DetailsOf(List<KycRow> kyc, List<AddressRow> addresses, NomineeRow? n, Holder investor, UploadState? upload,
        MasterLists masters)
    {
        var holders = new List<HolderDetails>();
        foreach (var k in kyc)
        {
            // The holder's mobile and e-mail are on their permanent address's row.
            var permanent = addresses.FirstOrDefault(a => a.HolderType == k.HolderType && a.AddrType == AddressType.Permanent) ?? new AddressRow();
            // A mailing row that says what the permanent one says is post going to the
            // permanent address: the holder gave no other.
            var c = addresses.FirstOrDefault(a => a.HolderType == k.HolderType && a.AddrType == AddressType.Communication);
            var communication = c is null || c.SameAddressAs(permanent) ? null : new TypedAddress(c.Line1, c.Line2, c.Line3, c.City, c.PinCode, c.District, c.State);

            // The gender Investor Information took down: it asks only for a holder with
            // none on record, and it is kept as the prefix to their name.
            var gender = "";
            if (Sections.GenderOnRecord(k.HolderType, investor, upload).Length == 0) gender = NamePrefixes.GenderOf(k.NamePrefix);

            // The FATCA answers are not kept: a "yes" stops the application at Investor
            // Information, so one that has gone on answered no to both.
            holders.Add(new HolderDetails(k.HolderType, gender, k.NameType, k.ParentName, k.AnnualIncome, k.Occupation,
                k.SubOccupation, MasterLists.NameOf(masters.MaritalStatuses, k.MaritalStatus), permanent.Mobile, permanent.Email,
                FatcaTaxResident: false, FatcaPermanentResident: false, k.Pep, k.PepRelated, communication));
        }

        NomineeDetails? nominee = null;
        if (n is not null)
        {
            nominee = new NomineeDetails(n.Name, Dates.FromDb(n.Dob), MasterLists.NameOf(masters.NomineeRelations, n.Relation), n.GuardianName,
                n.GuardianLine1, n.GuardianLine2, n.GuardianLine3, n.GuardianPinCode, n.GuardianCity);
        }
        return new ApplicationDetails { Holders = holders, Nominee = nominee };
    }

    // An account with neither an IFSC nor a number was not given.
    private static PaymentDetails PaymentOf(PaymentBankRow pay, RepaymentBankRow repay) => new(
        pay.Ifsc is null && pay.AccountNo is null ? null : new BankAccount(pay.Ifsc ?? "", pay.AccountNo ?? ""),
        repay.Ifsc is null && repay.AccountNo is null ? null : new BankAccount(repay.Ifsc ?? "", repay.AccountNo ?? ""),
        repay.SameAsPayment,
        pay.ChequeNo is null ? null : new ChequeDetails(pay.ChequeNo, Dates.FromDb(pay.ChequeDate), pay.CmsLocation ?? "", pay.CmsLocationCode ?? ""));

    private static Submission? SubmissionOf(HeaderRow h, int cancellationDays) => h.SubmittedOn is not { } at ? null
        : new Submission(at, h.SubStatus ?? "", h.LinkSentTo ?? "", h.LinkValidUntil ?? at, h.ResendsLeft ?? 0,
            h.LinkEmailedTo ?? "", h.ShortUrl ?? "", RegenerateUntil: h.CreatedOn.AddDays(cancellationDays),
            LinkSent: h.LinkValidUntil is not null);

    // Days after its creation an unpaid application is cancelled - by a process of its own, outside
    // this app, which only reads the result: until then a new link may be sent.
    private async Task<int> CancellationDaysAsync() => (await reference.ConfigAsync(CancellationToken.None)).CancellationDays;

    // ----- Saving a step -----------------------------------------------------

    public async Task<int?> SaveUploadAsync(string appNo, int version, UploadState upload, CancellationToken ct = default) =>
        Version(await SaveUploadOutcomeAsync(appNo, version, upload, ct));

    public async Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default)
    {
        var minorUnder = await MinorUnderAsync(ct);
        var masters = (await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None;
        return Version(await SaveAsync(appNo, version, (c, tx, h, at, u) => Sections.WriteDetailsAsync(c, tx, at, HolderOf(h), u, details, minorUnder, masters), "f_Details_Ver", ct));
    }

    public async Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default) =>
        Version(await SavePaymentOutcomeAsync(appNo, version, payment, ct));

    public async Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default)
    {
        var lists = await reference.ReferenceAsync(ct);
        var line = await FindAsync(appNo, ct) is { } seen ? await RateLineAsync(seen, deposit, ct) : null;
        var (outcome, next) = await SaveAsync(appNo, version, (c, tx, h, at, u) => Sections.WriteDepositAsync(c, tx, at, u, deposit, h.RenewDepNo, line, lists), "f_Deposit_Ver", ct);
        if (outcome != SaveOutcome.Saved || next is null) return null;

        // The source of funds goes on the FD system's log as well, read back as saved
        // so it carries the income and occupation the application holds.
        if (await FindAsync(appNo, ct) is { } saved)
            await SourceOfFundsLog.WriteAsync(db, StampAt(appNo, next.Value, RowStatus.Pending, saved.Holder.Folio), saved, ct);
        return next;
    }

    // The routes, which answer 404 and 409 apart, call these; the interface folds both into null.
    public async Task<(SaveOutcome, int?)> SaveUploadOutcomeAsync(string appNo, int version, UploadState upload, CancellationToken ct)
    {
        var documentCodes = await reference.DocumentCodesAsync(ct);
        return await SaveAsync(appNo, version, (c, tx, _, at, _) => Sections.WriteUploadAsync(c, tx, at, upload, documentCodes, dmsRoot), "f_Upload_Ver", ct);
    }

    public async Task<(SaveOutcome, int?)> SavePaymentOutcomeAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct)
    {
        var branches = await BranchesAsync(payment, ct);
        return await SaveAsync(appNo, version, (c, tx, _, at, u) => Sections.WritePaymentAsync(c, tx, at, u, payment, branches), "f_Payment_Ver", ct);
    }

    // Whether the signed-in user is a branch user: one of the sourcing agency's (the
    // sourcingAgency setting). Anyone else is a partner.
    private async Task<bool> BranchUserAsync(CancellationToken ct) =>
        partner.AgencyType == await reference.SettingAsync("sourcingAgency", ct);

    // Rows written now, by this partner, from their session and address.
    private Stamp StampAt(string appNo, int version, string status, string folio) =>
        new(appNo, version, status, partner.Id, SessionNumber(), partner.IpAddress, partner.UserClusterId, partner.UserName, folio);

    // The partner's backend session, when it is a number; null otherwise.
    private long? SessionNumber()
    {
        if (long.TryParse(partner.SessionId, out var number)) return number;
        return null;
    }

    // What a save looks up before it takes the application's lock.
    // The rate card's row for a deposit on this application: the row its scheme and
    // scheme code are read off. Null where the card offers none.
    private async Task<RateOption?> RateLineAsync(Application app, DepositDetails deposit, CancellationToken ct)
    {
        var card = await deposits.RatesAsync(app.RateCardRequest(app.Upload?.Category ?? "", await BranchUserAsync(ct)), ct);
        return RateCard.Line(card, deposit.TenureMonths, deposit.Payout, deposit.Amount);
    }

    private async Task<int> MinorUnderAsync(CancellationToken ct) => (await reference.ConfigAsync(ct)).MinAge;

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
        if (header.Version != version || header.Submitted || header.Cancelled) return (SaveOutcome.Conflict, null);

        var next = header.Version + 1;
        var upload = await UploadAsync(connection, tx, header);
        await write(connection, tx, header, StampAt(appNo, next, RowStatus.Pending, header.Folio), upload);
        await connection.ExecuteAsync(
            $"UPDATE dbo.t_Unotp_Application_Mst SET f_Version = @Next, {sectionColumn} = @Next, f_Updated_By = @Partner, f_Updated_On = SYSDATETIME() WHERE f_App_No = @AppNo",
            new { Next = next, Partner = partner.Id, AppNo = appNo }, tx);
        await tx.CommitAsync(ct);
        return (SaveOutcome.Saved, next);
    }

    private static async Task<UploadState?> UploadAsync(IDbConnection connection, IDbTransaction tx, HeaderRow header) =>
        header.UploadVer is null ? null
            : await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT f_Upload FROM dbo.t_Unotp_Upload_State WHERE f_App_No = @AppNo AND f_App_Version = @UploadVer AND f_Active = 1",
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
            USING (SELECT f_App_No FROM dbo.t_Unotp_Application_Mst WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1) AS a
                ON t.f_App_No = a.f_App_No AND t.f_Page = @Page
            WHEN MATCHED THEN UPDATE SET f_State = @State, f_Updated_On = SYSDATETIME()
            WHEN NOT MATCHED THEN INSERT (f_App_No, f_Page, f_State) VALUES (a.f_App_No, @Page, @State);
            """, new { AppNo = appNo, Partner = partner.Id, Page = page, State = state }) > 0;
    }

    // ----- Submitting ------------------------------------------------------------

    public async Task<Application?> SubmitAsync(string appNo, int version, CancellationToken ct = default) =>
        (await SubmitOutcomeAsync(appNo, version, ct)).Application;

    /// <summary>
    /// Every section written once more, as APR, at the next version: the application
    /// as submitted, with the rate locked on it. Nothing about a link is written
    /// here: one is made only once this has saved (RecordPaymentLinkAsync), on
    /// Submit &amp; send link straight away, after Try later whenever it is asked for.
    /// </summary>
    public async Task<(SaveOutcome Outcome, Application? Application)> SubmitOutcomeAsync(string appNo, int version, CancellationToken ct)
    {
        // The quote is asked before the lock.
        var seen = await FindAsync(appNo, ct);
        if (seen is null) return (SaveOutcome.NotFound, null);
        if (seen.Version != version || seen.Submitted is not null || seen.Cancelled) return (SaveOutcome.Conflict, null);
        var quote = seen.Deposit is { } d
            ? await deposits.QuoteAsync(new QuoteRequest(d.Amount, d.TenureMonths, d.Payout, seen.RateCardRequest(seen.Upload?.Category ?? "", await BranchUserAsync(ct))), ct)
            : null;
        var line = seen.Deposit is null ? null : await RateLineAsync(seen, seen.Deposit, ct);
        var resends = await reference.NumberAsync("linkResends", ct);
        var branches = seen.Payment is null ? [] : await BranchesAsync(seen.Payment, ct);
        var documentCodes = await reference.DocumentCodesAsync(ct);
        var lists = await reference.ReferenceAsync(ct);
        var minorUnder = await MinorUnderAsync(ct);

        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var header = await HeaderAsync(connection, tx, appNo, locked: true);
        if (header is null) return (SaveOutcome.NotFound, null);
        if (header.Version != version || header.Submitted || header.Cancelled) return (SaveOutcome.Conflict, null);

        // Read again under the lock, so what is written as submitted is what stands.
        var app = await AssembleAsync(connection, tx, header, await CancellationDaysAsync(), lists);
        var next = header.Version + 1;
        // An application whose investor's KYC was fetched from CKYC is submitted with
        // its rows in the FD system's tables as PEN_E, not APR.
        var submittedAs = app.Upload?.Ckyc == true ? RowStatus.PendingCkyc : RowStatus.Approved;
        var at = StampAt(appNo, next, submittedAs, header.Folio);
        if (app.Upload is { } upload) await Sections.WriteUploadAsync(connection, tx, at, upload, documentCodes, dmsRoot);
        if (app.Details is { } details) await Sections.WriteDetailsAsync(connection, tx, at, app.Holder, app.Upload, details, minorUnder, lists.Masters ?? MasterLists.None);
        if (app.Payment is { } payment) await Sections.WritePaymentAsync(connection, tx, at, app.Upload, payment, branches);
        if (app.Deposit is { } deposit) await Sections.WriteDepositAsync(connection, tx, at, app.Upload, deposit, header.RenewDepNo, line, lists);

        // Submitted on the database's clock, as every row on the application is dated.
        var submitted = await connection.QuerySingleAsync<HeaderRow>($"""
            UPDATE dbo.t_Unotp_Application_Mst SET f_Status = 'APR', f_Version = @Next,
                f_Upload_Ver = CASE WHEN f_Upload_Ver IS NULL THEN NULL ELSE @Next END,
                f_Details_Ver = CASE WHEN f_Details_Ver IS NULL THEN NULL ELSE @Next END,
                f_Payment_Ver = CASE WHEN f_Payment_Ver IS NULL THEN NULL ELSE @Next END,
                f_Deposit_Ver = CASE WHEN f_Deposit_Ver IS NULL THEN NULL ELSE @Next END,
                f_Quote_Interest_Each = @InterestEach, f_Quote_Maturity_Amount = @MaturityAmount,
                f_Quote_Matures_On = @MaturesOn, f_Quote_Rate_As_On = @RateAsOn,
                f_Submitted_On = SYSDATETIME(), f_Sub_Status = 'payment-pending', f_Resends_Left = @Resends,
                f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
            OUTPUT {Inserted}
            WHERE f_App_No = @AppNo
            """, new
        {
            Next = next, quote?.InterestEach, quote?.MaturityAmount,
            MaturesOn = quote?.MaturesOn.ToDateTime(TimeOnly.MinValue), RateAsOn = quote?.RateAsOn.ToDateTime(TimeOnly.MinValue),
            Resends = resends, Partner = partner.Id, AppNo = appNo,
        }, tx);
        var submission = SubmissionOf(submitted, await CancellationDaysAsync())!;
        await tx.CommitAsync(ct);

        app.Version = next;
        app.Submitted = submission;
        return (SaveOutcome.Saved, app);
    }

    /// <summary>
    /// The link a submitted application's investor is sent, put on record once the
    /// application itself is saved: on the application, and as a row of the payment
    /// link table for a purchase or of the re-payment link table for a renewal. It
    /// is open for linkValidityHours["payment"] from now. Null when no link may go
    /// any more: the application is paid, cancelled, or past cancellationDays.
    /// </summary>
    public async Task<Submission?> RecordPaymentLinkAsync(string appNo, PaymentLink? link, CancellationToken ct = default)
    {
        var app = await FindAsync(appNo, ct);
        if (app?.Submitted is null) return null;
        var investor = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
        var days = await CancellationDaysAsync();
        var hours = (await reference.ConfigAsync(ct)).LinkValidityHours.GetValueOrDefault("payment");

        HeaderRow? header;
        await using (var connection = await db.OpenAsync(ct))
        {
            header = await connection.QuerySingleOrDefaultAsync<HeaderRow>($"""
                UPDATE dbo.t_Unotp_Application_Mst
                SET f_Link_Sent_To = @LinkSentTo, f_Link_Emailed_To = @LinkEmailedTo,
                    f_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), f_Short_Url = @ShortUrl,
                    f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
                OUTPUT {Inserted}
                WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Status = 'APR' AND f_Active = 1
                  AND f_Paid_On IS NULL AND f_Cancelled_On IS NULL AND f_Created_On >= DATEADD(DAY, -@Days, SYSDATETIME())
                """, new
            {
                LinkSentTo = Masks.Mobile(investor?.Mobile ?? ""), LinkEmailedTo = Masks.Email(investor?.Email ?? ""),
                Hours = hours, ShortUrl = link?.ShortUrl ?? "", Partner = partner.Id, AppNo = appNo, Days = days,
            });
        }
        if (header is null) return null;
        var sent = SubmissionOf(header, days)!;
        await RecordLinkAsync(appNo, app.Renewal is not null, link?.Url ?? "", link?.ShortUrl ?? "", sent, ct);

        // No SMS or e-mail gateway is wired in yet: the link is logged for now.
        log.LogInformation("Payment link for {AppNo} to {Mobile} and {Email}: {Link}", appNo, sent.LinkSentTo,
            sent.LinkEmailedTo, link?.ShortUrl ?? link?.Url ?? "(the backend's own)");
        return sent;
    }

    // The header's columns as an UPDATE leaves them.
    private static readonly string Inserted = string.Join(", ", HeaderRow.Columns.Split(',').Select(c => "inserted." + c.Trim()));

    // The payment link as sent, on record in the payment link table (a purchase) or
    // the re-payment link table (a renewal): every send is a row. Written once the
    // application's own transaction is committed.
    private async Task RecordLinkAsync(string appNo, bool renewal, string url, string shortUrl, Submission sent, CancellationToken ct)
    {
        await using var links = await db.OpenAsync(ct);
        await links.ExecuteAsync($"""
            INSERT {LinkTables.For(renewal)} (f_App_No, f_Purpose, f_Url, f_Short_Url, f_Mobile, f_Email, f_Expires_On, f_Sent_By)
            VALUES (@AppNo, 'payment', @Url, @ShortUrl, @Mobile, @Email, @ExpiresOn, @Partner)
            """, new { AppNo = appNo, Url = url, ShortUrl = shortUrl, Mobile = sent.LinkSentTo, Email = sent.LinkEmailedTo,
                ExpiresOn = sent.LinkValidUntil, Partner = partner.Id });
    }

    // The last payment link sent for an application: its addresses are sent again as they are.
    private async Task<(string Url, string ShortUrl)> LastLinkAsync(string appNo, bool renewal, CancellationToken ct)
    {
        await using var links = await db.OpenAsync(ct);
        var last = await links.QuerySingleOrDefaultAsync<(string Url, string ShortUrl)>($"""
            SELECT TOP 1 f_Url, f_Short_Url FROM {LinkTables.For(renewal)} WHERE f_App_No = @AppNo AND f_Purpose = 'payment' AND f_Active = 1 ORDER BY f_Id DESC
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
            SET f_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
            OUTPUT {Inserted}
            WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Status = 'APR' AND f_Active = 1
              AND f_Paid_On IS NULL AND f_Cancelled_On IS NULL AND f_Created_On >= DATEADD(DAY, -@Days, SYSDATETIME())
              AND f_Link_Valid_Until IS NOT NULL
            """, new { AppNo = appNo, Partner = partner.Id, Hours = hours, Days = days }, tx);
        if (header is null) return null;
        var submission = SubmissionOf(header, days)!;
        await tx.CommitAsync(ct);
        var renewal = header.RenewDepNo is not null;
        var last = await LastLinkAsync(appNo, renewal, ct);
        await RecordLinkAsync(appNo, renewal, last.Url, last.ShortUrl, submission, ct);
        log.LogInformation("Payment link for {AppNo} sent again to {Mobile} and {Email}", appNo, submission.LinkSentTo, submission.LinkEmailedTo);
        return submission;
    }

    // ----- Lists -----------------------------------------------------------------

    // The investor's name as far as the application knows it: as NSDL verified it,
    // else as read off the PAN copy (or typed over it), else as the folio has it.
    // Empty for an investor with no folio whose PAN copy is not read yet.
    private const string KnownName = """
        COALESCE(NULLIF(JSON_VALUE(u.f_Upload, '$.name'), ''), NULLIF(JSON_VALUE(u.f_Upload, '$.nsdlName'), ''), m.f_Name)
        """;

    // Only a draft - not submitted, not cancelled already - and only the partner's own.
    public async Task<bool> CancelDraftAsync(string appNo, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Application_Mst
            SET f_Cancelled_On = SYSDATETIME(), f_Sub_Status = 'cancelled', f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
            WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1
              AND f_Status = 'PEN' AND f_Submitted_On IS NULL AND f_Cancelled_On IS NULL
            """, new { AppNo = appNo, Partner = partner.Id });
        if (rows > 0) log.LogInformation("Draft {AppNo} cancelled by {Partner}", appNo, partner.Id);
        return rows > 0;
    }

    // Kept for draftDays from the last save.
    public async Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default)
    {
        var days = (await reference.ConfigAsync(ct)).DraftDays;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<DraftRow>($"""
            SELECT m.f_App_No AS AppNo, {KnownName} AS Name, m.f_Pan AS Pan, m.f_Dob AS Dob, ISNULL(i.Amount, 0) AS Amount,
                m.f_Upload_Ver AS UploadVer, m.f_Details_Ver AS DetailsVer, m.f_Payment_Ver AS PaymentVer, m.f_Deposit_Ver AS DepositVer,
                m.f_Renew_Dep_No AS Renews,
                -- On the database's clock, where the time was written; turned into the app's below.
                DATEDIFF(MINUTE, COALESCE(m.f_Updated_On, m.f_Created_On), SYSDATETIME()) AS MinutesAgo
            FROM dbo.t_Unotp_Application_Mst m
            {InvestmentRow.CurrentOf}
            LEFT JOIN dbo.t_Unotp_Upload_State u ON u.f_App_No = m.f_App_No AND u.f_App_Version = m.f_Upload_Ver AND u.f_Active = 1
            WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'PEN' AND m.f_Active = 1 AND m.f_Cancelled_On IS NULL
              AND COALESCE(m.f_Updated_On, m.f_Created_On) > DATEADD(DAY, -@Days, SYSDATETIME())
            ORDER BY COALESCE(m.f_Updated_On, m.f_Created_On) DESC
            """, new { Partner = partner.Id, Days = days });
        var now = DateTime.Now;
        return rows.Select(r =>
        {
            var (done, next) = DraftSummary.Progress(r.UploadVer is not null, r.DetailsVer is not null, r.PaymentVer is not null, r.DepositVer is not null);
            return new DraftSummary(r.AppNo, Masks.Name(r.Name), Masks.Pan(r.Pan), Masks.Dob(Dates.FromDb(r.Dob)), r.Amount,
                done, next, now.AddMinutes(-Math.Max(0, r.MinutesAgo)), r.Renews ?? "");
        }).ToList();
    }

    public async Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default)
    {
        var payouts = (await reference.ReferenceAsync(ct)).Payouts;
        await using var connection = await db.OpenAsync(ct);
        var rows = (await connection.QueryAsync<ListRow>($"""
            SELECT m.f_App_No AS AppNo, m.f_Status AS Status, m.f_Folio AS Folio, {KnownName} AS Name, m.f_Pan AS Pan, m.f_Dob AS Dob,
                i.Amount, i.TenureMonths, i.Payout,
                JSON_VALUE(u.f_Upload, '$.appType') AS AppType, JSON_VALUE(u.f_Upload, '$.payMode') AS PayMode,
                ISNULL((SELECT COUNT(*) FROM OPENJSON(u.f_Upload, '$.joint')), 0) AS JointHolders,
                m.f_Upload_Ver AS UploadVer, m.f_Details_Ver AS DetailsVer, m.f_Payment_Ver AS PaymentVer, m.f_Deposit_Ver AS DepositVer,
                m.f_Created_On AS CreatedOn, m.f_Submitted_On AS SubmittedOn,
                m.f_Accepted_On AS AcceptedOn, m.f_Paid_On AS PaidOn, m.f_Booked_On AS BookedOn, m.f_Fdr_No AS FdrNo,
                m.f_Cancelled_On AS CancelledOn, ISNULL(pm.f_Branch, N'') AS Branch,
                (SELECT MIN(f.f_Created_On) FROM dbo.t_Unotp_Upload_State f WHERE f.f_App_No = m.f_App_No AND f.f_Active = 1) AS UploadedOn,
                (SELECT MIN(p.f_Generated_On) FROM dbo.t_Unotp_Pay_In_Slip p WHERE p.f_App_No = m.f_App_No AND p.f_Active = 1) AS SlipOn,
                (SELECT MIN(l.f_Sent_On) FROM {LinkTables.Both} l WHERE l.f_App_No = m.f_App_No AND l.f_Active = 1) AS LinkSentOn,
                m.f_Penny_Drop_On AS PennyDropOn, m.f_Penny_Drop_Status AS PennyDropStatus,
                m.f_Kyc_Verified_On AS KycVerifiedOn, m.f_Kyc_Status AS KycStatus
            FROM dbo.t_Unotp_Application_Mst m
            {InvestmentRow.CurrentOf}
            LEFT JOIN dbo.t_Unotp_Upload_State u ON u.f_App_No = m.f_App_No AND u.f_App_Version = m.f_Upload_Ver AND u.f_Active = 1
            LEFT JOIN dbo.t_Unotp_Partner_Mst pm ON pm.f_User_Id = m.f_Partner_Id
            WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1
            ORDER BY m.f_Created_On DESC
            """, new { Partner = partner.Id })).ToList();

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
