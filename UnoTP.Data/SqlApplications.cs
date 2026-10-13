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
/// The partner's applications, kept in SQL Server (db/create_new_tables.sql, and the FD system's own tables in db/create_transaction_tables.sql).
///
/// A save is checked against the version the page read, under a lock on the
/// application's row, and inserts its section afresh at the next version with
/// status PEN. Submitting writes every section once more, as APR - the
/// application as it was submitted - and locks the rate. A detail row is never
/// updated or deleted, so every earlier save stays on record.
/// </summary>
public sealed class SqlApplications(Db db, IPartner partner, IDepositApi deposits, SqlMasters masters, SqlReference reference, IConfiguration config,
    ILogger<SqlApplications> log)
    : IApplicationApi, IRenewalOpener
{
    // The document store's root, recorded with each copy's path (f_Doc_Filepath).
    private readonly string dmsRoot = DmsPaths.Root(config);

    // ----- Opening -----------------------------------------------------------

    public Task<Application> OpenAsync(Holder holder, CancellationToken ct = default) =>
        OpenAsync(holder, null, null, null, null, ct);

    /// <summary>
    /// A renewal request cancelled: the application opened from the deposit is marked
    /// cancelled. A draft can be; a submitted one only while it is a digital
    /// application the investor has not accepted. A physical one, once submitted, cannot.
    /// </summary>
    public async Task<bool> CancelRenewalAsync(string depositNumber)
    {
        await using var connection = await db.OpenAsync(CancellationToken.None);
        var rows = await connection.ExecuteAsync(ApplicationQueries.CancelRenewal, new { Number = depositNumber, Partner = partner.Id, Digital = ApplicationType.Digital });
        return rows > 0;
    }

    /// <summary>A renewal's application, with what comes over from the deposit saved on it as its first version.</summary>
    public Task<Application> OpenRenewalAsync(Holder holder, RenewalOf renewal, UploadState upload, PaymentDetails payment, DepositDetails deposit) =>
        OpenAsync(holder, renewal, upload, payment, deposit, CancellationToken.None);

    private async Task<Application> OpenAsync(Holder holder, RenewalOf? renewal, UploadState? upload, PaymentDetails? payment,
        DepositDetails? deposit, CancellationToken ct)
    {
        var branches = payment is null ? new Dictionary<string, BankBranch>() : await BranchesAsync(payment, ct);
        var documentCodes = await reference.DocumentCodesAsync(ct);
        var onRecord = await CopiesOnRecordAsync(holder, upload, ct);
        var lists = await reference.ReferenceAsync(ct);

        // Not numbered yet: only whose rate card the deposit takes is read off this.
        var unnumbered = new Application { AppNo = "", Holder = holder, Renewal = renewal, Upload = upload };
        var line = deposit is null ? null : await RateLineAsync(unnumbered, deposit, ct);
        var asking = await AskingAsync(ct);

        await using var connection = await db.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var appNo = await ApplicationNumber.NextAsync(connection, tx, asking, renewal is not null);
        const int version = 1;
        await connection.ExecuteAsync(ApplicationQueries.InsertApplication, new
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
        if (upload is not null) await Sections.WriteUploadAsync(connection, tx, at, upload, documentCodes, dmsRoot, onRecord);
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
            ApplicationQueries.Header(locked),
            new { AppNo = appNo, Partner = partner.Id }, tx);

    // The application as it stands: each section from its active rows in the FD
    // system's tables, and the app's own rows at the version the header holds.
    private static async Task<Application> AssembleAsync(IDbConnection connection, IDbTransaction? tx, HeaderRow h, int cancellationDays,
        ReferenceData lists)
    {
        using var read = await connection.QueryMultipleAsync(ApplicationTableQueries.WholeApplication, new { h.AppNo, h.UploadVer, h.DetailsVer, h.PaymentVer, h.DepositVer }, tx);

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
                FatcaTaxResident: false, FatcaPermanentResident: false, k.Pep, k.PepRelated, communication, permanent.City));
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
        var asSaved = await FindAsync(appNo, ct);
        var addresses = await AddressesOnRecordAsync(asSaved?.Holder, asSaved?.Upload, ct);
        return Version(await SaveAsync(appNo, version, (c, tx, h, at, u) => Sections.WriteDetailsAsync(c, tx, at, HolderOf(h), u, details, minorUnder, masters, addresses), "f_Details_Ver", ct));
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
        var onRecord = await CopiesOnRecordAsync(await InvestorOfAsync(appNo, ct), upload, ct);
        return await SaveAsync(appNo, version, (c, tx, _, at, _) => Sections.WriteUploadAsync(c, tx, at, upload, documentCodes, dmsRoot, onRecord), "f_Upload_Ver", ct);
    }

    // Who the application is for, as its row has them; null when it is not the partner's.
    private async Task<Holder?> InvestorOfAsync(string appNo, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var header = await HeaderAsync(connection, null, appNo, locked: false);
        return header is null ? null : HolderOf(header);
    }

    // The copies on record for the application's holders who are on a folio - the
    // investor and each joint holder. Read before the save's transaction opens: where
    // such a holder uploads no photograph or proof of address, the copy on record is
    // written against the application in its place (Sections.WriteUploadAsync).
    private async Task<IReadOnlyList<HolderCopyOnRecord>> CopiesOnRecordAsync(Holder? investor, UploadState? upload, CancellationToken ct)
    {
        var found = new List<HolderCopyOnRecord>();
        foreach (var (type, who) in HoldersOf(investor, upload))
        {
            foreach (var copy in await masters.CopiesOnRecordAsync(who, ct))
                found.Add(new HolderCopyOnRecord(type, who.Source, copy));
        }
        return found;
    }

    // The permanent address on record for each of the application's holders who is on
    // a folio, by holder: written against the application, part for part, where the
    // holder files no proof of address on it (Sections.WriteDetailsAsync).
    private async Task<IReadOnlyDictionary<string, TypedAddress>> AddressesOnRecordAsync(Holder? investor, UploadState? upload, CancellationToken ct)
    {
        var found = new Dictionary<string, TypedAddress>();
        foreach (var (type, who) in HoldersOf(investor, upload))
        {
            if (await masters.PermanentAddressOnRecordAsync(who, ct) is { } address) found[type] = address;
        }
        return found;
    }

    // The application's holders: the investor, and each joint holder on the upload step.
    private static List<(string Type, Holder Who)> HoldersOf(Holder? investor, UploadState? upload)
    {
        var holders = new List<(string Type, Holder Who)>();
        if (investor is not null) holders.Add((HolderType.Investor, investor));
        if (upload is not null) holders.AddRange(upload.Joint.Select(j => (j.Key, j.Value.Holder)));
        return holders;
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
            ApplicationQueries.NextVersion(sectionColumn),
            new { Next = next, Partner = partner.Id, AppNo = appNo }, tx);
        await tx.CommitAsync(ct);
        return (SaveOutcome.Saved, next);
    }

    private static async Task<UploadState?> UploadAsync(IDbConnection connection, IDbTransaction tx, HeaderRow header) =>
        header.UploadVer is null ? null
            : await connection.QuerySingleOrDefaultAsync<string>(
                ApplicationQueries.UploadState,
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
        return await connection.ExecuteAsync(ApplicationQueries.SavePage, new { AppNo = appNo, Partner = partner.Id, Page = page, State = state }) > 0;
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
        var onRecord = await CopiesOnRecordAsync(seen.Holder, seen.Upload, ct);
        var addresses = await AddressesOnRecordAsync(seen.Holder, seen.Upload, ct);
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
        if (app.Upload is { } upload) await Sections.WriteUploadAsync(connection, tx, at, upload, documentCodes, dmsRoot, onRecord);
        if (app.Details is { } details) await Sections.WriteDetailsAsync(connection, tx, at, app.Holder, app.Upload, details, minorUnder, lists.Masters ?? MasterLists.None, addresses);
        if (app.Payment is { } payment) await Sections.WritePaymentAsync(connection, tx, at, app.Upload, payment, branches);
        if (app.Deposit is { } deposit) await Sections.WriteDepositAsync(connection, tx, at, app.Upload, deposit, header.RenewDepNo, line, lists);

        // Submitted on the database's clock, as every row on the application is dated.
        var submitted = await connection.QuerySingleAsync<HeaderRow>(ApplicationQueries.Submit, new
        {
            Next = next, quote?.InterestEach, quote?.MaturityAmount,
            MaturesOn = quote?.MaturesOn.ToDateTime(TimeOnly.MinValue), RateAsOn = quote?.RateAsOn.ToDateTime(TimeOnly.MinValue),
            Resends = resends, Partner = partner.Id, AppNo = appNo,
        }, tx);

        // Paid by cheque: now that the application stands as APR, it is put in the
        // pay-in slip staging table to wait for its slip, whichever way it was submitted.
        if (PaidByInstrument(app.Upload, lists))
            await PayInSlipStaging.InsertAsync(connection, tx, at, PayInSlipStagingRow.For(app, branches, partner.AgencyCode));

        var submission = SubmissionOf(submitted, await CancellationDaysAsync())!;
        await tx.CommitAsync(ct);

        app.Version = next;
        app.Submitted = submission;
        return (SaveOutcome.Saved, app);
    }

    // Whether the application is paid by an instrument - a cheque - rather than
    // electronically: the payment mode chosen is one a copy of the instrument is filed for.
    private static bool PaidByInstrument(UploadState? upload, ReferenceData lists) =>
        upload is not null && lists.PaymentModes.Any(mode => mode.Document is not null && mode.Name == upload.PayMode);

    /// <summary>
    /// The link a submitted application's investor is sent, put on record once the
    /// application itself is saved: on the application, and as a row of the link
    /// request table for a purchase or of the one for a renewal (LinkTables). It
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
            header = await connection.QuerySingleOrDefaultAsync<HeaderRow>(ApplicationQueries.RecordLink, new
            {
                LinkSentTo = Masks.Mobile(investor?.Mobile ?? ""), LinkEmailedTo = Masks.Email(investor?.Email ?? ""),
                Hours = hours, ShortUrl = link?.ShortUrl ?? "", Partner = partner.Id, AppNo = appNo, Days = days,
            });
        }
        if (header is null) return null;
        var sent = SubmissionOf(header, days)!;
        // Written once the application's own save is committed.
        await using (var links = await db.OpenAsync(ct))
        {
            await LinkTables.SendAsync(links, app.Renewal is not null, LinkRequestRow.For(app, link, hours), partner);
        }

        // No SMS or e-mail gateway is wired in yet: the link is logged for now.
        log.LogInformation("Payment link for {AppNo} to {Mobile} and {Email}: {Link}", appNo, sent.LinkSentTo,
            sent.LinkEmailedTo, link?.ShortUrl ?? link?.Url ?? "(the backend's own)");
        return sent;
    }

    // Regenerate, allowed until cancellationDays after the application was created,
    // while it is unpaid and not cancelled. No new link is made: the same short link
    // is sent again, open for LinkRequestRow.RegeneratedForHours from now.
    public async Task<Submission?> ResendLinkAsync(string appNo, CancellationToken ct = default)
    {
        var days = await CancellationDaysAsync();
        await using var connection = await db.OpenAsync(ct);
        var header = await connection.QuerySingleOrDefaultAsync<HeaderRow>(ApplicationQueries.ResendLink, new { AppNo = appNo, Partner = partner.Id, Hours = LinkRequestRow.RegeneratedForHours, Days = days });
        if (header is null) return null;
        var submission = SubmissionOf(header, days)!;

        var renewal = header.RenewDepNo is not null;
        if (!await LinkTables.SendAgainAsync(connection, renewal, appNo, partner))
            log.LogWarning("Payment link for {AppNo} sent again, but {Table} holds no live link for it", appNo, LinkTables.For(renewal));
        log.LogInformation("Payment link for {AppNo} sent again to {Mobile} and {Email}", appNo, submission.LinkSentTo, submission.LinkEmailedTo);
        return submission;
    }

    // ----- Lists -----------------------------------------------------------------

    // Only a draft - not submitted, not cancelled already - and only the partner's own.
    public async Task<bool> CancelDraftAsync(string appNo, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.ExecuteAsync(ApplicationQueries.CancelDraft, new { AppNo = appNo, Partner = partner.Id });
        if (rows > 0) log.LogInformation("Draft {AppNo} cancelled by {Partner}", appNo, partner.Id);
        return rows > 0;
    }

    // Kept for draftDays from the last save.
    public async Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default)
    {
        var days = (await reference.ConfigAsync(ct)).DraftDays;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<DraftRow>(ApplicationQueries.Drafts, new { Partner = partner.Id, Days = days });
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
        var rows = (await connection.QueryAsync<ListRow>(ApplicationQueries.Applications, new { Partner = partner.Id })).ToList();

        return rows.Select(r =>
        {
            var payout = payouts.FirstOrDefault(p => p.Code == r.Payout);
            var digital = r.AppType != "PHYSICAL";
            return new ApplicationRecord(
                r.AppNo, r.Folio.Length > 0 ? r.Folio : null, Masks.Name(r.Name), Masks.Pan(r.Pan),
                r.Amount ?? 0, payout?.PerYear == 0, r.TenureMonths ?? 0, payout?.Name ?? r.Payout ?? "",
                1 + r.JointHolders, r.SubmittedOn ?? r.CreatedOn, digital, r.PayMode ?? "",
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
