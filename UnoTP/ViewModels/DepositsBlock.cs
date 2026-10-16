using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// The deposits an identified investor holds, as Investor Identification lists them
/// under the investor's record: where each stands, and Renew on one that is due.
/// The register lists only deposits inside the renewal window. Renew opens an
/// application like a new deposit's, with the deposit's holders, repayment account
/// and amount already on it (<see cref="IRenewalApi.StartAsync"/>).
/// </summary>
/// <param name="Window">The renewal window: entry closes Window.MinDays before maturity.</param>
/// <param name="Off">Why a renewal cannot be entered now; null while Renew FD is on.</param>
public sealed record DepositsBlock(IReadOnlyList<HeldDeposit> Deposits, ReferenceData Ref, RenewalWindow Window, string? Off = null)
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
        "due" => "Due for renewal", "auto" => "Auto renewal", "requested" => "Request raised", "held" => "On hold",
        "renewing" => "Renewal in progress", "renewed" => "Renewed",
        _ => status,
    };

    public static string StatusTone(string status) => status switch
    {
        "due" or "renewing" => "chip--warn", "renewed" => "chip--ok", _ => "chip--muted",
    };

    /// <summary>What a deposit carries besides its number: its joint holders. Empty for one with none.</summary>
    public static string Tags(HeldDeposit d)
    {
        var tags = new List<string>();
        if (d.JointHolders is { Count: 1 }) tags.Add("1 joint holder");
        if (d.JointHolders is { Count: > 1 } joint) tags.Add($"{joint.Count} joint holders");
        return string.Join(" · ", tags);
    }

    /// <summary>The line under a deposit's status: the one date or fact that matters for it now.</summary>
    public string Note(HeldDeposit d) => d.Status switch
    {
        "due" => $"Entry open till {Money.Day(EntryClosesOn(d))}",
        "auto" => "Renews by itself on maturity",
        "requested" => "Already raised in the FD system",
        "held" => "Contact the E-Sarathi helpdesk",
        "renewing" => $"Draft {d.Renewal?.AppNo} · next: {d.Renewal?.NextStep}",
        "renewed" => d.Renewal is null ? "Processed in the FD system" : $"Application {d.Renewal.AppNo}",
        _ => "",
    };

    /// <summary>The whole remark on a deposit, shown when its status is pointed at: what renewing it means now, or why it cannot be entered.</summary>
    public string Remark(HeldDeposit d)
    {
        if (d.Status != "due") return d.Why;
        return $"Matures on {Money.Day(d.MaturesOn)}. Entry open till {Money.Day(EntryClosesOn(d))}; renewed at the card rate on the day of application.";
    }

    public string PayoutName(string code) => Ref.Payouts.FirstOrDefault(p => p.Code == code)?.Name ?? code;

    // The last day a renewal can be entered: MinDays before maturity.
    private DateOnly EntryClosesOn(HeldDeposit d) => d.MaturesOn.AddDays(-Window.MinDays);
}
