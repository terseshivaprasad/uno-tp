namespace UnoTP.Backend.Mock;

/// <summary>
/// The deposits the demo folios hold, and the renewals opened from them while the
/// app runs. The deposits stand relative to today, so the demo always has ones due
/// for renewal, one too early, one too late and one matured. A renewal opens an
/// application as a new deposit does, with the deposit's holders, repayment
/// account and maturity amount on it; from then on it is the application that moves.
/// </summary>
public sealed class MockRenewals(IRenewalOpener applications, IInvestorApi investors, IReferenceApi reference) : IRenewalApi
{
    /// <summary>A renewal may be entered from this many days before maturity...</summary>
    public const int FromDays = 61;

    /// <summary>...until this many days before it; nearer, it is Operations'.</summary>
    public const int UntilDays = 7;

    /// <summary>The same, for a deposit tagged for auto renewal.</summary>
    public const int UntilDaysAutoRenewal = 10;

    private sealed record Held(string Number, string Folio, string Pan, string Dob, string Investor, string Category,
        long Amount, decimal Rate, int TenureMonths, string Payout, bool AutoRenewal, int MaturesInDays,
        DepositHolder[] JointHolders, BankAccount? Repayment);

    // Maturity is so many days from today, so the statuses hold whatever the date.
    private static readonly Held[] Deposits =
    [
        new("FD2023001234", "TS003027", "XXXXA1001A", MockInvestors.DemoDob, "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 500_000, 7.85m, 36, "maturity", false, 20,
            [new("XXXXH1008H", MockInvestors.DemoDob, "MEERA ANIL JOSHI", "MF0084456")], new("HDFC0000521", "50100123456789")),
        new("FD2024005678", "TS003027", "XXXXA1001A", MockInvestors.DemoDob, "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 200_000, 7.60m, 24, "quarterly", true, 12,
            [], new("HDFC0000521", "50100123456789")),
        new("FD2022004455", "TS003027", "XXXXA1001A", MockInvestors.DemoDob, "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 100_000, 7.25m, 12, "monthly", false, 5,
            [], new("HDFC0000521", "50100123456789")),
        new("FD2025000912", "TS003027", "XXXXA1001A", MockInvestors.DemoDob, "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 300_000, 7.25m, 12, "yearly", false, 200,
            [], null),
        new("FD2021009876", "MF0084456", "XXXXH1008H", MockInvestors.DemoDob, "MEERA ANIL JOSHI", "WOMEN", 150_000, 7.40m, 18, "maturity", false, -60,
            [], new("ICIC0000104", "000401234567")),
    ];

    // Folios with no deposits still answer, with none.
    private static readonly string[] FoliosWithout = ["MF0051187"];

    private static readonly object Gate = new();
    private static readonly HashSet<string> Renewed = [];

    public Task<IReadOnlyList<HeldDeposit>?> DepositsByFolioAsync(string folio, CancellationToken ct = default)
    {
        folio = folio.Trim().ToUpperInvariant();
        var held = Deposits.Where(d => d.Folio == folio).Select(View).OrderBy(d => d.MaturesOn).ToList();
        return Task.FromResult<IReadOnlyList<HeldDeposit>?>(held.Count > 0 || FoliosWithout.Contains(folio) ? held : null);
    }

    public Task<IReadOnlyList<HeldDeposit>?> DepositsByPanAsync(string pan, string dob, CancellationToken ct = default)
    {
        pan = pan.Trim().ToUpperInvariant();
        var held = Deposits.Where(d => d.Pan == pan && d.Dob == dob.Trim()).Select(View).OrderBy(d => d.MaturesOn).ToList();
        return Task.FromResult<IReadOnlyList<HeldDeposit>?>(held.Count > 0 ? held : null);
    }

    public Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default) =>
        Task.FromResult(Deposits.FirstOrDefault(d => d.Number.Equals(number.Trim(), StringComparison.OrdinalIgnoreCase)) is { } d ? View(d) : null);

    public async Task<Application?> StartAsync(string depositNumber, CancellationToken ct = default)
    {
        if (Deposits.FirstOrDefault(d => d.Number == depositNumber) is not { } held) return null;
        var deposit = View(held);
        if (!deposit.Renewable) return null;
        // The investor as the register holds them, and the joint holders the deposit carries.
        if (await investors.FolioAsync(deposit.Folio, ct) is not { } record) return null;
        var upload = new UploadState();
        var code = 2;
        foreach (var joint in held.JointHolders)
        {
            if (await investors.FolioAsync(joint.Folio, ct) is { } on) upload.Joint[code.ToString("00")] = new JointHolder { Holder = HolderOf(on) };
            code++;
        }
        var lists = await reference.ReferenceAsync(ct);
        var renewal = new RenewalOf(deposit.Number, deposit.MaturityAmount, deposit.MaturesOn, deposit.Rate, deposit.TenureMonths, deposit.Payout);
        var payment = new PaymentDetails(null, held.Repayment, false, null);
        var opened = new DepositDetails(deposit.MaturityAmount, deposit.TenureMonths, deposit.Payout, held.AutoRenewal, "", false, lists.DeliveryTypes.FirstOrDefault()?.Code ?? "");
        lock (Gate)
        {
            if (!Renewed.Add(deposit.Number)) return null;
        }
        return await applications.OpenRenewalAsync(HolderOf(record), renewal, upload, payment, opened);
    }

    private static Holder HolderOf(FolioRecord r) =>
        new(r.Pan, r.Dob, r.Name, r.Folio, r.Docs.Pan, r.Address, r.Docs, r.Gender);

    // A deposit as it stands today: its dates, and whether a renewal can be entered now.
    private static HeldDeposit View(Held d)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var matures = today.AddDays(d.MaturesInDays);
        var started = matures.AddMonths(-d.TenureMonths);
        var perYear = d.Payout switch { "maturity" => 0, "yearly" => 1, "halfyearly" => 2, "quarterly" => 4, "monthly" => 12, _ => 0 };
        var maturity = MockDeposits.MaturityOf(d.Amount, d.Rate, d.TenureMonths, perYear);
        var until = d.AutoRenewal ? UntilDaysAutoRenewal : UntilDays;
        bool renewed;
        lock (Gate) renewed = Renewed.Contains(d.Number);
        var (status, why) = renewed ? ("renewed", "Already renewed: the renewal is on View Application.")
            : matures > today.AddDays(FromDays) ? ("running", $"Renewal entry opens {FromDays} days before maturity, on {matures.AddDays(-FromDays):d MMM yyyy}.")
            : matures >= today.AddDays(until) ? ("due", "")
            : matures >= today ? ("late", $"Renewal entry closed {until} days before maturity{(d.AutoRenewal ? ", as an auto-renewal deposit" : "")}, on {matures.AddDays(-until):d MMM yyyy}: it is with Operations now.")
            : ("matured", $"Matured on {matures:d MMM yyyy}: past renewal entry here.");
        return new HeldDeposit(d.Number, d.Folio, d.Investor, d.Category, d.Amount, d.Rate, d.TenureMonths, d.Payout,
            started, matures, (long)maturity, status, status == "due", why, d.JointHolders, d.Repayment, d.AutoRenewal);
    }
}
