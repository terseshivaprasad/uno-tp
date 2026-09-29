using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>Renew FD: a folio searched, the deposits it holds, and the renewals asked for.</summary>
/// <param name="Deposits">Null until a folio is searched, or for a folio the register does not hold.</param>
public sealed record RenewViewModel(string Folio, IReadOnlyList<HeldDeposit>? Deposits, IReadOnlyList<RenewalRecord> Recent, ReferenceData Ref, AppConfig Config)
{
    public bool Searched => Folio.Length > 0;

    public bool NotFound => Searched && Deposits is null;

    public static string StatusLabel(string status) => status switch
    {
        "running" => "Running", "maturing" => "Maturing", "matured" => "Matured", "renewed" => "Renewed", "closed" => "Paid out",
        "sent" => "Sent for acceptance", "accepted" => "Accepted", _ => status,
    };

    public static string StatusTone(string status) => status switch
    {
        "maturing" or "matured" => "chip--warn", "renewed" or "accepted" => "chip--ok", "sent" => "chip--primary", _ => "chip--muted",
    };

    public string PayoutName(string code) => Ref.Payouts.FirstOrDefault(p => p.Code == code)?.Name ?? code;

    public string ModeName(string code) => Ref.RenewInstructions.FirstOrDefault(m => m.Code == code)?.Name ?? code;
}

/// <summary>What is asked for a renewal: what is renewed, for how long, and how interest is paid.</summary>
public sealed class RenewForm
{
    /// <summary>A renew instruction's code (<see cref="ReferenceData.RenewInstructions"/>).</summary>
    public string Mode { get => modeValue; set => modeValue = value ?? ""; }
    private string modeValue = "";

    public int TenureMonths { get; set; }

    /// <summary>A payout's code (<see cref="ReferenceData.Payouts"/>).</summary>
    public string Payout { get => payoutValue; set => payoutValue = value ?? ""; }
    private string payoutValue = "";

    public const string PrincipalAndInterest = "principal-interest";

    /// <summary>What is renewed under the mode: the principal, or the maturity amount.</summary>
    public long AmountFor(HeldDeposit deposit) => Mode == PrincipalAndInterest ? deposit.MaturityAmount : deposit.Amount;

    /// <summary>The day the new deposit starts: maturity, or today once the deposit has matured.</summary>
    public static DateOnly RenewsOn(HeldDeposit deposit) =>
        deposit.MaturesOn > DateOnly.FromDateTime(DateTime.Today) ? deposit.MaturesOn : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>How the form opens: the maturity amount renewed, on the deposit's own tenure and payout.</summary>
    public static RenewForm Opening(HeldDeposit deposit, ReferenceData reference) => new()
    {
        Mode = reference.RenewInstructions.Any(m => m.Code == PrincipalAndInterest) ? PrincipalAndInterest : reference.RenewInstructions.FirstOrDefault()?.Code ?? "",
        TenureMonths = reference.Tenures.Contains(deposit.TenureMonths) ? deposit.TenureMonths : reference.Tenures.FirstOrDefault(),
        Payout = reference.Payouts.Any(p => p.Code == deposit.Payout) ? deposit.Payout : reference.Payouts.FirstOrDefault()?.Code ?? "",
    };

    public NewRenewal ToRequest(HeldDeposit deposit) => new(deposit.Number, Mode, TenureMonths, Payout);

    /// <summary>What stops the renewal being asked for, by field; "Deposit" for the deposit itself.</summary>
    public Dictionary<string, string> Problems(HeldDeposit deposit, ReferenceData reference)
    {
        var problems = new Dictionary<string, string>();
        if (!deposit.Renewable) problems["Deposit"] = deposit.Why.Length > 0 ? deposit.Why : "This deposit cannot be renewed now.";
        if (!reference.RenewInstructions.Any(m => m.Code == Mode)) problems["Mode"] = "Choose what is renewed";
        if (!reference.Tenures.Contains(TenureMonths)) problems["TenureMonths"] = "Choose the tenure";
        if (!reference.Payouts.Any(p => p.Code == Payout)) problems["Payout"] = "Choose the interest payout";
        return problems;
    }
}

/// <summary>One deposit, the renewal asked for it, and the backend's quote for that.</summary>
public sealed record RenewDepositViewModel(HeldDeposit Deposit, RenewForm Form, DepositQuote? Quote, ReferenceData Ref, IReadOnlyDictionary<string, string> Problems)
{
    public string? Problem(string field) => Problems.GetValueOrDefault(field);

    public PayoutOption? PayoutChosen => Ref.Payouts.FirstOrDefault(p => p.Code == Form.Payout);

    public bool Cumulative => PayoutChosen is { PerYear: 0 };

    public string Category => Ref.Categories.FirstOrDefault(c => c.Code == Deposit.Category)?.Name ?? Deposit.Category;

    public string PayoutName(string code) => Ref.Payouts.FirstOrDefault(p => p.Code == code)?.Name ?? code;
}

/// <summary>A renewal sent for acceptance, as recorded.</summary>
public sealed record RenewDoneViewModel(RenewalRecord Renewal, ReferenceData Ref)
{
    public string ModeName => Ref.RenewInstructions.FirstOrDefault(m => m.Code == Renewal.Mode)?.Name ?? Renewal.Mode;

    public string PayoutName => Ref.Payouts.FirstOrDefault(p => p.Code == Renewal.Payout)?.Name ?? Renewal.Payout;
}
