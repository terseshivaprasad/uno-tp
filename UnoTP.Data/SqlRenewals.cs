using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// Renew FD: the deposits a holder can renew, and opening a renewal of one. The
/// deposits are read from the FD system's deposit register as its own Renew FD
/// procedure reads them (RenewFdQueries): live deposits inside the renewal window
/// with no renewal application on them yet. The window itself is the FD system's web
/// config for this user's source: a partner's, or a branch user's.
///
/// What the register says of a deposit - renewable, auto renewal, a request already
/// raised, a loan or a freeze on it - is kept as its status; a renewal being entered
/// or submitted here comes first.
/// </summary>
public sealed class SqlRenewals(Db db, IPartner partner, IRenewalOpener applications, IInvestorApi investors, SqlReference reference) : IRenewalApi
{
    // The web config's sources: a partner's, and a branch user's (one of the sourcing agency).
    private const string PartnerSource = "CPTP_UNI";
    private const string BranchSource = "BOTC_UNI";

    // What the register says of a deposit (RegisterStatus), as the procedure words it.
    private const string AutoRenewalStatus = "Auto-Renewal";
    private const string RequestRaisedStatus = "Renewal request already generated";
    private const string RequestProcessedStatus = "Renewal request processed successfully";
    private const string HelpdeskStatus = "Please contact E-Sarathi Helpdesk";

    private sealed class DepositRow
    {
        public string Folio { get; set; } = "";
        public string Number { get; set; } = "";
        public string? Investor { get; set; }
        public decimal? Amount { get; set; }
        public int? TenureMonths { get; set; }
        public decimal? Rate { get; set; }
        public decimal? MaturityAmount { get; set; }
        public string? BrokerCode { get; set; }
        public string? Cumulative { get; set; }
        public string? RepayAccount { get; set; }
        public string? RepayIfsc { get; set; }
        public bool AutoRenewal { get; set; }
        public DateTime MaturesOn { get; set; }
        public bool Renewable { get; set; }
        public string? RegisterStatus { get; set; }

        // The application that renews it here, where one is open or submitted and not cancelled.
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

    private sealed class WindowRow
    {
        public int? MinDays { get; set; }
        public int? MaxDays { get; set; }
        public int? CancelMinDays { get; set; }
    }

    public async Task<IReadOnlyList<HeldDeposit>?> DepositsByFolioAsync(string folio, CancellationToken ct = default)
    {
        folio = folio.Trim().ToUpperInvariant();
        // The register is searched by the holder: the folio's first holder.
        if (await investors.FolioAsync(folio, ct) is not { } f) return null;
        return await HeldAsync(RenewFdQueries.DepositsOfHolder, new { f.Pan, Dob = Dates.ParseDdMmYyyy(f.Dob) }, ct);
    }

    public async Task<IReadOnlyList<HeldDeposit>?> DepositsByPanAsync(string pan, string dob, CancellationToken ct = default)
    {
        var held = await HeldAsync(RenewFdQueries.DepositsOfHolder, new { Pan = pan.Trim().ToUpperInvariant(), Dob = Dates.ParseDdMmYyyy(dob) }, ct);
        if (held.Count == 0) return null;
        return held;
    }

    public async Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default) =>
        (await HeldAsync(RenewFdQueries.DepositByNumber, new { Number = number.Trim() }, ct)).FirstOrDefault();

    public async Task<Application?> StartAsync(string depositNumber, CancellationToken ct = default)
    {
        if (await DepositAsync(depositNumber, ct) is not { Renewable: true } deposit) return null;
        // The investor as the register holds them, and the joint holders the deposit carries.
        if (await OnRecordAsync(deposit.Folio, ct) is not { } record) return null;

        var upload = new UploadState();
        var code = 2;
        foreach (var joint in deposit.JointHolders ?? [])
        {
            if (await investors.FolioOnRecordAsync(joint.Folio, joint.Pan, joint.Dob, ct) is { } on) upload.Joint[code.ToString("00")] = new JointHolder { Holder = HolderOf(on) };
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

    /// <summary>
    /// The window from the FD system's web config for this user's source. A value the
    /// config does not hold falls back to the app's own setting (renewFromDays opens
    /// entry, renewUntilDays closes it).
    /// </summary>
    public async Task<RenewalWindow> WindowAsync(CancellationToken ct = default)
    {
        var config = await reference.ConfigAsync(ct);
        var branchUser = partner.AgencyType == await reference.SettingAsync("sourcingAgency", ct);
        var source = branchUser ? BranchSource : PartnerSource;

        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<WindowRow>(RenewFdQueries.RenewalWindow, new { Source = source }) ?? new WindowRow();
        return new RenewalWindow(row.MinDays ?? config.RenewUntilDays, row.MaxDays ?? config.RenewFromDays, row.CancelMinDays ?? config.RenewUntilDays);
    }

    // A holder of the deposit as their folio's data source has them, found by the
    // PAN and date of birth the folio master holds.
    private async Task<FolioRecord?> OnRecordAsync(string folio, CancellationToken ct)
    {
        if (await investors.FolioAsync(folio, ct) is not { } f) return null;
        return await investors.FolioOnRecordAsync(f.Folio, f.Pan, f.Dob, ct);
    }

    private static Holder HolderOf(FolioRecord r) =>
        new(r.Pan, r.Dob, r.Name, r.Folio, r.Docs.Pan, r.Address, r.Docs, r.Gender, r.Source);

    // The deposits a query finds, each as it stands today, soonest to mature first.
    private async Task<IReadOnlyList<HeldDeposit>> HeldAsync(string deposits, object holder, CancellationToken ct)
    {
        var window = await WindowAsync(ct);
        var lists = await reference.ReferenceAsync(ct);
        var cumulative = lists.Payouts.FirstOrDefault(p => p.PerYear == 0)?.Code ?? "";

        await using var connection = await db.OpenAsync(ct);
        var args = new DynamicParameters(holder);
        args.Add("Today", DateTime.Today);
        args.Add("MinDays", window.MinDays);
        args.Add("MaxDays", window.MaxDays);
        var rows = (await connection.QueryAsync<DepositRow>(deposits, args)).ToList();
        if (rows.Count == 0) return [];

        // The joint holders on those folios: every holder but the first.
        var folios = rows.Select(r => r.Folio).Distinct().ToList();
        var joint = (await connection.QueryAsync<(string Folio, string Pan, DateTime? Dob, string Name)>(RenewFdQueries.JointHoldersOnFolios, new { Folios = folios })).ToList();

        var held = new List<HeldDeposit>();
        foreach (var row in rows)
        {
            var holders = joint.Where(j => j.Folio == row.Folio).Select(j => new DepositHolder(j.Pan, Dates.FromDb(j.Dob), j.Name, j.Folio)).ToList();
            held.Add(View(row, holders, cumulative, partner.Id));
        }
        return held.OrderBy(d => d.MaturesOn).ToList();
    }

    // A deposit as it stands today: what the register says of it, and the renewal entered here if any.
    private static HeldDeposit View(DepositRow row, IReadOnlyList<DepositHolder> jointHolders, string cumulativePayout, string partnerId)
    {
        var renewal = RenewalOf(row, partnerId);
        var (status, why) = StatusOf(row, renewal);

        BankAccount? repayment = null;
        if (!string.IsNullOrWhiteSpace(row.RepayIfsc) && !string.IsNullOrWhiteSpace(row.RepayAccount)) repayment = new BankAccount(row.RepayIfsc.Trim(), row.RepayAccount.Trim());

        // A cumulative deposit pays out on maturity; a non-cumulative one's frequency the register does not say.
        var payout = row.Cumulative?.Trim().ToUpperInvariant() == "C" ? cumulativePayout : "";
        var amount = (long)(row.Amount ?? 0);
        var maturityAmount = (long)(row.MaturityAmount ?? row.Amount ?? 0);
        return new HeldDeposit(row.Number.Trim(), row.Folio.Trim(), row.Investor?.Trim() ?? "", "", amount, row.Rate ?? 0, row.TenureMonths ?? 0, payout,
            DateOnly.FromDateTime(row.MaturesOn), maturityAmount, status, status == "due", why, jointHolders, repayment, row.AutoRenewal, renewal);
    }

    // The application that renews the deposit here, and whether its request can be cancelled now.
    private static RenewalApp? RenewalOf(DepositRow row, string partnerId)
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

    // Where the deposit stands: a renewal entered here first, then what the register says.
    private static (string Status, string Why) StatusOf(DepositRow row, RenewalApp? renewal)
    {
        if (renewal is { Submitted: true }) return ("renewed", $"Already renewed: application {renewal.AppNo} is submitted.");
        if (renewal is not null) return ("renewing", $"A renewal is being entered: application {renewal.AppNo}, not submitted yet.");
        if (row.Renewable) return ("due", "");

        var said = row.RegisterStatus?.Trim() ?? "";
        if (said == AutoRenewalStatus) return ("auto", "Tagged for auto renewal: it renews on maturity by itself, so no renewal is entered here.");
        if (said == RequestRaisedStatus) return ("requested", "A renewal request is already raised for it in the FD system.");
        if (said == RequestProcessedStatus) return ("renewed", "Its renewal request is processed in the FD system.");
        if (said == HelpdeskStatus) return ("held", "A loan or a freeze stands on it: please contact the E-Sarathi helpdesk.");
        return ("held", said);
    }
}
