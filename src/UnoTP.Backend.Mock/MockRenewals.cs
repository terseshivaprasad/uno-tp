namespace UnoTP.Backend.Mock;

/// <summary>
/// The deposits the demo folios hold, and the renewals asked for while the app
/// runs. The deposits stand relative to today, so the demo always has one that has
/// matured, one about to, and one running. A renewal is quoted at the same card
/// rates as a new deposit (<see cref="MockDeposits"/>).
/// </summary>
public sealed class MockRenewals(IDepositApi deposits) : IRenewalApi
{
    /// <summary>A renewal may be asked for from this many days before maturity...</summary>
    public const int BeforeDays = 30;

    /// <summary>...to this many days after it; then the deposit is paid out.</summary>
    public const int AfterDays = 14;

    private sealed record Held(string Number, string Folio, string Investor, string Mobile, string Category,
        long Amount, decimal Rate, int TenureMonths, string Payout, int MaturesInDays);

    // Maturity is so many days from today, so the statuses hold whatever the date.
    private static readonly Held[] Deposits =
    [
        new("FD2023001234", "TS003027", "SHIVAPRASAD SUBHASH TERSE", "9876543210", "PUBLIC/GENERAL", 500_000, 7.85m, 36, "maturity", -4),
        new("FD2024005678", "TS003027", "SHIVAPRASAD SUBHASH TERSE", "9876543210", "PUBLIC/GENERAL", 200_000, 7.60m, 24, "quarterly", 18),
        new("FD2025000912", "TS003027", "SHIVAPRASAD SUBHASH TERSE", "9876543210", "PUBLIC/GENERAL", 300_000, 7.25m, 12, "yearly", 200),
        new("FD2021009876", "MF0084456", "MEERA ANIL JOSHI", "9822011223", "WOMEN", 150_000, 7.40m, 18, "maturity", -60),
    ];

    // Folios with no deposits still answer, with none.
    private static readonly string[] FoliosWithout = ["MF0051187"];

    private static readonly object Gate = new();
    private static readonly List<RenewalRecord> Renewals = [];
    private static readonly HashSet<string> Renewed = [];
    private static int seq;

    public Task<IReadOnlyList<HeldDeposit>?> DepositsAsync(string folio, CancellationToken ct = default)
    {
        folio = folio.Trim().ToUpperInvariant();
        var held = Deposits.Where(d => d.Folio == folio).Select(View).OrderByDescending(d => d.StartedOn).ToList();
        return Task.FromResult<IReadOnlyList<HeldDeposit>?>(held.Count > 0 || FoliosWithout.Contains(folio) ? held : null);
    }

    public Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default) =>
        Task.FromResult(Deposits.FirstOrDefault(d => d.Number.Equals(number.Trim(), StringComparison.OrdinalIgnoreCase)) is { } d ? View(d) : null);

    public async Task<RenewalRecord?> RenewAsync(NewRenewal renewal, CancellationToken ct = default)
    {
        if (Deposits.FirstOrDefault(d => d.Number == renewal.DepositNumber) is not { } held) return null;
        var deposit = View(held);
        if (!deposit.Renewable) return null;
        var amount = renewal.Mode == "principal-interest" ? deposit.MaturityAmount : deposit.Amount;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var renewsOn = deposit.MaturesOn > today ? deposit.MaturesOn : today;
        var quote = await deposits.QuoteAsync(new QuoteRequest(amount, renewal.TenureMonths, renewal.Payout, deposit.Category, renewsOn), ct);
        lock (Gate)
        {
            if (!Renewed.Add(deposit.Number)) return null;
            var record = new RenewalRecord($"RN-{today:ddMM}-{++seq:D3}", deposit.Number, deposit.Folio, deposit.Investor,
                renewal.Mode, amount, renewal.TenureMonths, renewal.Payout, quote.Rate, renewsOn, quote.MaturesOn, (long)quote.MaturityAmount,
                "sent", DateTime.Now, Masks.Mobile(held.Mobile));
            Renewals.Insert(0, record);
            return record;
        }
    }

    public Task<IReadOnlyList<RenewalRecord>> RenewalsAsync(CancellationToken ct = default)
    {
        lock (Gate) return Task.FromResult<IReadOnlyList<RenewalRecord>>(Renewals.ToList());
    }

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
        var (status, why) = renewed ? ("renewed", "Already renewed: see the renewals asked for.")
            : matures > today.AddDays(BeforeDays) ? ("running", $"Renewal opens {BeforeDays} days before maturity, on {matures.AddDays(-BeforeDays):d MMM yyyy}.")
            : matures >= today ? ("maturing", "")
            : matures >= today.AddDays(-AfterDays) ? ("matured", "")
            : ("closed", $"The renewal window closed {AfterDays} days after maturity, and the deposit was paid out.");
        return new HeldDeposit(d.Number, d.Folio, d.Investor, d.Category, d.Amount, d.Rate, d.TenureMonths, d.Payout,
            started, matures, (long)maturity, status, status is "maturing" or "matured", why);
    }
}
