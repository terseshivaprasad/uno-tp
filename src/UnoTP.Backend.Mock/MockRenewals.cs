namespace UnoTP.Backend.Mock;

/// <summary>
/// The deposits the demo folios hold, and the renewals opened from them while the
/// app runs. The deposits stand relative to today, so the demo always has one that
/// has matured, one about to, and one running. A renewal opens an application as
/// a new deposit does, with the deposit's holders, repayment account and maturity
/// amount on it; from then on it is the application that moves.
/// </summary>
public sealed class MockRenewals(MockApplications applications, IInvestorApi investors, IReferenceApi reference) : IRenewalApi
{
    /// <summary>A renewal may be opened from this many days before maturity...</summary>
    public const int BeforeDays = 30;

    /// <summary>...to this many days after it; then the deposit is paid out.</summary>
    public const int AfterDays = 14;

    private sealed record Held(string Number, string Folio, string Investor, string Category,
        long Amount, decimal Rate, int TenureMonths, string Payout, int MaturesInDays,
        DepositHolder[] JointHolders, BankAccount? Repayment);

    // Maturity is so many days from today, so the statuses hold whatever the date.
    private static readonly Held[] Deposits =
    [
        new("FD2023001234", "TS003027", "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 500_000, 7.85m, 36, "maturity", -4,
            [new("XXXXH1008H", MockInvestors.DemoDob, "MEERA ANIL JOSHI", "MF0084456")], new("HDFC0000521", "50100123456789")),
        new("FD2024005678", "TS003027", "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 200_000, 7.60m, 24, "quarterly", 18,
            [], new("HDFC0000521", "50100123456789")),
        new("FD2025000912", "TS003027", "SHIVAPRASAD SUBHASH TERSE", "PUBLIC/GENERAL", 300_000, 7.25m, 12, "yearly", 200,
            [], null),
        new("FD2021009876", "MF0084456", "MEERA ANIL JOSHI", "WOMEN", 150_000, 7.40m, 18, "maturity", -60,
            [], new("ICIC0000104", "000401234567")),
    ];

    // Folios with no deposits still answer, with none.
    private static readonly string[] FoliosWithout = ["MF0051187"];

    private static readonly object Gate = new();
    private static readonly HashSet<string> Renewed = [];

    public Task<IReadOnlyList<HeldDeposit>?> DepositsAsync(string folio, CancellationToken ct = default)
    {
        folio = folio.Trim().ToUpperInvariant();
        var held = Deposits.Where(d => d.Folio == folio).Select(View).OrderByDescending(d => d.StartedOn).ToList();
        return Task.FromResult<IReadOnlyList<HeldDeposit>?>(held.Count > 0 || FoliosWithout.Contains(folio) ? held : null);
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
        var opened = new DepositDetails(deposit.MaturityAmount, deposit.TenureMonths, deposit.Payout, false, "", false, lists.DeliveryTypes.FirstOrDefault()?.Code ?? "");
        lock (Gate)
        {
            if (!Renewed.Add(deposit.Number)) return null;
        }
        return await applications.OpenRenewalAsync(HolderOf(record), renewal, upload, payment, opened);
    }

    private static Holder HolderOf(FolioRecord r) =>
        new(r.Pan, r.Dob, r.Name, r.Folio, r.Docs.Pan, r.Address, r.Docs, r.Gender);

    // A deposit as it stands today: its dates, and whether it can be renewed now.
    private static HeldDeposit View(Held d)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var matures = today.AddDays(d.MaturesInDays);
        var started = matures.AddMonths(-d.TenureMonths);
        var perYear = d.Payout switch { "maturity" => 0, "yearly" => 1, "halfyearly" => 2, "quarterly" => 4, "monthly" => 12, _ => 0 };
        var maturity = MockDeposits.MaturityOf(d.Amount, d.Rate, d.TenureMonths, perYear);
        bool renewed;
        lock (Gate) renewed = Renewed.Contains(d.Number);
        var (status, why) = renewed ? ("renewed", "Already renewed: the renewal is on View Application.")
            : matures > today.AddDays(BeforeDays) ? ("running", $"Renewal opens {BeforeDays} days before maturity, on {matures.AddDays(-BeforeDays):d MMM yyyy}.")
            : matures >= today ? ("maturing", "")
            : matures >= today.AddDays(-AfterDays) ? ("matured", "")
            : ("closed", $"The renewal window closed {AfterDays} days after maturity, and the deposit was paid out.");
        return new HeldDeposit(d.Number, d.Folio, d.Investor, d.Category, d.Amount, d.Rate, d.TenureMonths, d.Payout,
            started, matures, (long)maturity, status, status is "maturing" or "matured", why, d.JointHolders, d.Repayment);
    }
}
