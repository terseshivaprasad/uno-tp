using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// The deposits an identified investor holds, as Investor Identification lists them
/// under the investor's record: where each stands, and Renew on one that is due.
/// Renew opens an application like a new deposit's, with the deposit's holders,
/// repayment account and amount already on it (<see cref="IRenewalApi.StartAsync"/>).
/// </summary>
/// <param name="Off">Why a renewal cannot be entered now; null while Renew FD is on.</param>
public sealed record DepositsBlock(IReadOnlyList<HeldDeposit> Deposits, ReferenceData Ref, AppConfig Config, string? Off = null)
{
    /// <summary>How many of them a renewal can be entered for now.</summary>
    public int Due => Off is null ? Deposits.Count(d => d.Renewable) : 0;

    /// <summary>The deposits as the list shows them: those a renewal can be entered for first, then those renewed, then the rest; within each, the soonest to mature first.</summary>
    public IReadOnlyList<HeldDeposit> InOrder => [.. Deposits.OrderBy(Rank).ThenBy(d => d.MaturesOn)];

    private static int Rank(HeldDeposit d)
    {
        if (d.Renewable) return 0;
        if (d.Status == "renewing") return 1;
        if (d.Status == "renewed") return 2;
        return 3;
    }

    public static string StatusLabel(string status) => status switch
    {
        "running" => "Running", "due" => "Due for renewal", "late" => "Entry closed", "matured" => "Matured",
        "renewing" => "Renewal in progress", "renewed" => "Renewed", "closed" => "Paid out",
        _ => status,
    };

    public static string StatusTone(string status) => status switch
    {
        "due" or "renewing" => "chip--warn", "renewed" => "chip--ok", _ => "chip--muted",
    };

    /// <summary>What a deposit carries besides its number: its joint holders, and that it is tagged for auto renewal. Empty for one with neither.</summary>
    public static string Tags(HeldDeposit d)
    {
        var tags = new List<string>();
        if (d.JointHolders is { Count: 1 }) tags.Add("1 joint holder");
        if (d.JointHolders is { Count: > 1 } joint) tags.Add($"{joint.Count} joint holders");
        if (d.AutoRenewal) tags.Add("Auto renewal");
        return string.Join(" · ", tags);
    }

    /// <summary>The line under a deposit's status: the one date or fact that matters for it now.</summary>
    public string Note(HeldDeposit d) => d.Status switch
    {
        "due" => $"Entry open till {Money.Day(d.MaturesOn.AddDays(-UntilDays(d)))}",
        "running" => $"Entry opens {Money.Day(d.MaturesOn.AddDays(-Config.RenewFromDays))}",
        "late" => "With Operations now",
        "matured" => "Past renewal entry",
        "renewing" => $"Draft {d.Renewal?.AppNo} · next: {d.Renewal?.NextStep}",
        "renewed" => $"Application {d.Renewal?.AppNo}",
        _ => "",
    };

    /// <summary>The whole remark on a deposit, shown when its status is pointed at: what renewing it means now, or why it cannot be entered.</summary>
    public string Remark(HeldDeposit d)
    {
        if (d.Status != "due") return d.Why;
        return $"Matures on {Money.Day(d.MaturesOn)}{(d.AutoRenewal ? " · tagged for auto renewal" : "")}. "
            + $"Entry open till {Money.Day(d.MaturesOn.AddDays(-UntilDays(d)))}; renewed at the card rate on the day of application.";
    }

    public string PayoutName(string code) => Ref.Payouts.FirstOrDefault(p => p.Code == code)?.Name ?? code;

    // How many days before maturity entry closes: earlier for a deposit tagged for auto renewal.
    private int UntilDays(HeldDeposit d)
    {
        if (d.AutoRenewal) return Config.RenewUntilDaysAutoRenewal;
        return Config.RenewUntilDays;
    }
}
