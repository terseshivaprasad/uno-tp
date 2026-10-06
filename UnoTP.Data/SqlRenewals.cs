using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// Renew FD: the deposits a folio holds, and opening a renewal of one. The deposits
/// read here are the ones booked through this app - an application the FD system has
/// booked, with its deposit number and booking date on t_Unotp_Application_Mst. A
/// deposit booked elsewhere is the FD system's, and is not read until it answers.
///
/// A deposit runs from the day it was booked for its tenure. A renewal can be entered
/// from renewFromDays before it matures until renewUntilDays before (earlier, for a
/// deposit tagged for auto renewal); nearer than that it is Operations'.
/// </summary>
public sealed class SqlRenewals(Db db, IPartner partner, IRenewalOpener applications, IInvestorApi investors, SqlReference reference) : IRenewalApi
{
    private sealed class BookedRow
    {
        public string Number { get; set; } = "";
        public string AppNo { get; set; } = "";
        public string Folio { get; set; } = "";
        public string Investor { get; set; } = "";
        public string? Category { get; set; }
        public long Amount { get; set; }
        public decimal? Rate { get; set; }
        public int? TenureMonths { get; set; }
        public string? InterestFreq { get; set; }
        public bool AutoRenewal { get; set; }
        public DateTime BookedOn { get; set; }
        public decimal? MaturityAmount { get; set; }
        public string? RepayIfsc { get; set; }
        public string? RepayAccount { get; set; }

        // The application that renews it, where one is open or submitted and not cancelled.
        public string? RenewalAppNo { get; set; }
        public string? RenewalPartner { get; set; }
        public DateTime? RenewalSubmittedOn { get; set; }
        public DateTime? RenewalAcceptedOn { get; set; }
        public string? RenewalAppType { get; set; }
        public int? RenewalUploadVer { get; set; }
        public int? RenewalDetailsVer { get; set; }
        public int? RenewalPaymentVer { get; set; }
        public int? RenewalDepositVer { get; set; }
    }

    // A booked application and its deposit as submitted, with the application that
    // renews it - the latest one open or submitted, and not cancelled.
    private const string Booked = """
        SELECT m.f_Fdr_No AS Number, m.f_App_No AS AppNo, m.f_Folio AS Folio, m.f_Name AS Investor,
               i.f_Category AS Category, CAST(i.f_Amount AS BIGINT) AS Amount, i.f_Int_Rate AS Rate, TRY_CAST(i.f_Tenure AS INT) AS TenureMonths,
               i.f_Int_Freq AS InterestFreq, CAST(ISNULL(i.f_Is_Auto_Renewal, 0) AS BIT) AS AutoRenewal,
               m.f_Booked_On AS BookedOn, m.f_Quote_Maturity_Amount AS MaturityAmount,
               NULLIF(b.f_NEFTCode, N'') AS RepayIfsc, NULLIF(b.f_BankAccountNo, N'') AS RepayAccount,
               rn.f_App_No AS RenewalAppNo, rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn, rn.f_Accepted_On AS RenewalAcceptedOn,
               rn.AppType AS RenewalAppType, rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM dbo.t_Unotp_Application_Mst m
        JOIN dbo.t_FD_BT_Investment_Dtl i ON i.f_Appl_No = m.f_App_No AND i.f_Active = 1 AND i.f_Status IN ('APR', 'PEN_E')
        OUTER APPLY (
            SELECT TOP (1) r.f_NEFTCode, r.f_BankAccountNo FROM dbo.t_FD_BT_Investor_Bank_Dtl r
            WHERE r.f_Appl_No = m.f_App_No AND r.f_Active = 1 ORDER BY r.f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC) b
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver, r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = m.f_Fdr_No AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        WHERE m.f_Active = 1 AND m.f_Booked_On IS NOT NULL AND NULLIF(m.f_Fdr_No, '') IS NOT NULL AND m.f_Cancelled_On IS NULL
        """;

    public async Task<IReadOnlyList<HeldDeposit>?> DepositsByFolioAsync(string folio, CancellationToken ct = default)
    {
        folio = folio.Trim().ToUpperInvariant();
        if (await investors.FolioAsync(folio, ct) is null) return null;
        return await HeldAsync("m.f_Folio = @Folio", new { Folio = folio }, ct);
    }

    public async Task<IReadOnlyList<HeldDeposit>?> DepositsByPanAsync(string pan, string dob, CancellationToken ct = default)
    {
        var held = await HeldAsync("m.f_Pan = @Pan AND m.f_Dob = @Dob", new { Pan = pan.Trim().ToUpperInvariant(), Dob = Dates.ParseDdMmYyyy(dob) }, ct);
        if (held.Count == 0) return null;
        return held;
    }

    public async Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default) =>
        (await HeldAsync("m.f_Fdr_No = @Number", new { Number = number.Trim() }, ct)).FirstOrDefault();

    public async Task<Application?> StartAsync(string depositNumber, CancellationToken ct = default)
    {
        if (await DepositAsync(depositNumber, ct) is not { Renewable: true } deposit) return null;
        // The investor as the register holds them, and the joint holders the deposit carries.
        if (await OnRecordAsync(deposit.Folio, ct) is not { } record) return null;

        var upload = new UploadState();
        var code = 2;
        foreach (var joint in deposit.JointHolders ?? [])
        {
            if (await OnRecordAsync(joint.Folio, ct) is { } on) upload.Joint[code.ToString("00")] = new JointHolder { Holder = HolderOf(on) };
            code++;
        }

        var lists = await reference.ReferenceAsync(ct);
        var renewal = new RenewalOf(deposit.Number, deposit.MaturityAmount, deposit.MaturesOn, deposit.Rate, deposit.TenureMonths, deposit.Payout, deposit.Amount);
        var payment = new PaymentDetails(null, deposit.Repayment, false, null);
        var opened = new DepositDetails(deposit.MaturityAmount, deposit.TenureMonths, deposit.Payout, deposit.AutoRenewal, "", false,
            lists.DeliveryTypes.FirstOrDefault()?.Code ?? "");
        return await applications.OpenRenewalAsync(HolderOf(record), renewal, upload, payment, opened);
    }

    public Task<bool> CancelAsync(string depositNumber, CancellationToken ct = default) =>
        applications.CancelRenewalAsync(depositNumber.Trim());

    // A holder of the deposit as their folio's data source has them, found by the
    // PAN and date of birth the folio master holds.
    private async Task<FolioRecord?> OnRecordAsync(string folio, CancellationToken ct)
    {
        if (await investors.FolioAsync(folio, ct) is not { } f) return null;
        return await investors.FolioOnRecordAsync(f.Folio, f.Pan, f.Dob, ct);
    }

    private static Holder HolderOf(FolioRecord r) =>
        new(r.Pan, r.Dob, r.Name, r.Folio, r.Docs.Pan, r.Address, r.Docs, r.Gender, r.Source);

    // The booked deposits a filter finds, each as it stands today, soonest to mature first.
    private async Task<IReadOnlyList<HeldDeposit>> HeldAsync(string filter, object args, CancellationToken ct)
    {
        var config = await reference.ConfigAsync(ct);

        await using var connection = await db.OpenAsync(ct);
        var rows = (await connection.QueryAsync<BookedRow>($"{Booked} AND {filter}", args)).ToList();
        if (rows.Count == 0) return [];

        // The joint holders on those applications: every holder but the first.
        var joint = (await connection.QueryAsync<(string AppNo, string Pan, DateTime? Dob, string Name, string Folio)>("""
            SELECT f_Appl_No, ISNULL(f_Kyc_PAN, N''), f_Kyc_DOB, ISNULL(f_Kyc_FullName, N''), ISNULL(f_FolioNo, N'')
            FROM dbo.t_FD_BT_Kyc_Data_Dtl
            WHERE f_Appl_No IN @AppNos AND f_Active = 1 AND f_Holder_Type <> @Investor
            ORDER BY f_Appl_No, f_Holder_Type
            """, new { AppNos = rows.Select(r => r.AppNo).ToList(), HolderType.Investor })).ToList();

        var held = new List<HeldDeposit>();
        foreach (var row in rows)
        {
            var holders = joint.Where(j => j.AppNo == row.AppNo).Select(j => new DepositHolder(j.Pan, Dates.FromDb(j.Dob), j.Name, j.Folio)).ToList();
            held.Add(View(row, holders, config, partner.Id));
        }
        return held.OrderBy(d => d.MaturesOn).ToList();
    }

    // A deposit as it stands today: its dates, and whether a renewal can be entered now.
    private static HeldDeposit View(BookedRow row, IReadOnlyList<DepositHolder> jointHolders, AppConfig config, string partnerId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tenure = row.TenureMonths ?? 0;
        var started = DateOnly.FromDateTime(row.BookedOn);
        var matures = started.AddMonths(tenure);

        var until = config.RenewUntilDays;
        if (row.AutoRenewal) until = config.RenewUntilDaysAutoRenewal;
        var renewal = RenewalOf(row, partnerId);
        var (status, why) = StatusOf(row, renewal, today, matures, config.RenewFromDays, until);

        BankAccount? repayment = null;
        if (row.RepayIfsc is not null && row.RepayAccount is not null) repayment = new BankAccount(row.RepayIfsc, row.RepayAccount);

        // The maturity amount as quoted when it was submitted; the deposit's own amount where none was.
        var maturityAmount = (long)(row.MaturityAmount ?? row.Amount);
        return new HeldDeposit(row.Number, row.Folio, row.Investor, row.Category ?? "", row.Amount, row.Rate ?? 0, tenure, row.InterestFreq ?? "",
            matures, maturityAmount, status, status == "due", why, jointHolders, repayment, row.AutoRenewal, renewal);
    }

    // The application that renews the deposit, and whether its request can be cancelled now.
    private static RenewalApp? RenewalOf(BookedRow row, string partnerId)
    {
        if (row.RenewalAppNo is null) return null;

        var submitted = row.RenewalSubmittedOn is not null;
        var mine = row.RenewalPartner == partnerId;
        var (_, next) = DraftSummary.Progress(row.RenewalUploadVer is not null, row.RenewalDetailsVer is not null,
            row.RenewalPaymentVer is not null, row.RenewalDepositVer is not null);

        var cancelWhy = "";
        if (!mine) cancelWhy = Messages.RenewFd.CancelOnlyByWhoEntered;
        else if (submitted && row.RenewalAppType != ApplicationType.Digital) cancelWhy = Messages.RenewFd.CancelNotForPhysical;
        else if (submitted && row.RenewalAcceptedOn is not null) cancelWhy = Messages.RenewFd.CancelNotAfterAcceptance;

        return new RenewalApp(row.RenewalAppNo, submitted, submitted ? "" : next, mine, cancelWhy.Length == 0, cancelWhy);
    }

    private static (string Status, string Why) StatusOf(BookedRow row, RenewalApp? renewal, DateOnly today, DateOnly matures, int fromDays, int untilDays)
    {
        if (renewal is { Submitted: true }) return ("renewed", $"Already renewed: application {renewal.AppNo} is submitted.");
        if (renewal is not null) return ("renewing", $"A renewal is being entered: application {renewal.AppNo}, not submitted yet.");
        if (matures > today.AddDays(fromDays))
            return ("running", $"Renewal entry opens {fromDays} days before maturity, on {matures.AddDays(-fromDays):d MMM yyyy}.");
        if (matures >= today.AddDays(untilDays)) return ("due", "");
        if (matures >= today)
        {
            var asAutoRenewal = row.AutoRenewal ? ", as an auto-renewal deposit" : "";
            return ("late", $"Renewal entry closed {untilDays} days before maturity{asAutoRenewal}, on {matures.AddDays(-untilDays):d MMM yyyy}: it is with Operations now.");
        }
        return ("matured", $"Matured on {matures:d MMM yyyy}: past renewal entry here.");
    }
}
