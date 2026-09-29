using System.Globalization;
using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>What FD Configuration posts.</summary>
public sealed class DepositForm
{
    public string Amount { get => amountValue; set => amountValue = value ?? ""; }
    private string amountValue = "";
    public int TenureMonths { get; set; }
    public string InterestPayout { get => interestPayoutValue; set => interestPayoutValue = value ?? ""; }
    private string interestPayoutValue = "";
    public bool AutoRenewal { get; set; }
    public string RenewInstruction { get => renewInstructionValue; set => renewInstructionValue = value ?? ""; }
    private string renewInstructionValue = "";
    public bool NoTds { get; set; }
    public string DeliveryType { get => deliveryTypeValue; set => deliveryTypeValue = value ?? ""; }
    private string deliveryTypeValue = "";

    public long AmountValue => long.TryParse(new string(Amount.Where(char.IsAsciiDigit).ToArray()), out var n) ? n : 0;

    /// <summary>The deposit as the backend last saved it, or the first of each list before it ever was.</summary>
    public static DepositForm From(DepositDetails? saved, ReferenceData reference) => saved is null
        ? new DepositForm
        {
            TenureMonths = reference.Tenures.FirstOrDefault(),
            InterestPayout = reference.Payouts.FirstOrDefault()?.Code ?? "",
            DeliveryType = reference.DeliveryTypes.FirstOrDefault()?.Code ?? "",
        }
        : new DepositForm
        {
            Amount = saved.Amount > 0 ? Money.Group(saved.Amount) : "",
            TenureMonths = saved.TenureMonths,
            InterestPayout = saved.Payout,
            AutoRenewal = saved.AutoRenewal,
            RenewInstruction = saved.RenewInstruction,
            NoTds = saved.NoTds,
            DeliveryType = saved.DeliveryType,
        };

    public DepositDetails ToDetails() =>
        new(AmountValue, TenureMonths, InterestPayout, AutoRenewal, AutoRenewal ? RenewInstruction : "", NoTds, DeliveryType);

    /// <summary>What is wrong with the amount, or null.</summary>
    public string? AmountProblem(AppConfig config)
    {
        var n = AmountValue;
        if (n == 0) return "Required — enter the deposit amount";
        if (n < config.MinAmount) return $"Below the {Money.Rupees(config.MinAmount)} minimum";
        if (n > config.MaxAmount) return $"Above the {Money.Rupees(config.MaxAmount)} maximum";
        if (config.AmountStep > 0 && n % config.AmountStep != 0) return $"Not a multiple of {Money.Rupees(config.AmountStep)} — {Money.InWords(n).ToLowerInvariant()}";
        return null;
    }

    /// <summary>What stops the step, by field.</summary>
    /// <param name="amountFixed">A renewal: the amount is the deposit's maturity amount, not the partner's to change or the limits' to check.</param>
    public Dictionary<string, string> Problems(AppConfig config, ReferenceData reference, bool amountFixed = false)
    {
        var problems = new Dictionary<string, string>();
        if (!amountFixed && AmountProblem(config) is { } amount) problems["Amount"] = amount;
        if (!reference.Tenures.Contains(TenureMonths)) problems["TenureMonths"] = "Choose the tenure";
        if (reference.Payouts.All(p => p.Code != InterestPayout)) problems["InterestPayout"] = "Choose the interest payout";
        if (AutoRenewal && reference.RenewInstructions.All(r => r.Code != RenewInstruction)) problems["RenewInstruction"] = "Required — choose what auto renewal renews";
        if (reference.DeliveryTypes.All(d => d.Code != DeliveryType)) problems["DeliveryType"] = "Choose the delivery type";
        return problems;
    }
}

/// <summary>FD Configuration: the deposit as configured, and what the backend quotes for it.</summary>
public sealed class DepositViewModel(DocumentsViewModel docs, DepositForm form, DepositQuote? quote, IReadOnlyDictionary<string, string> problems)
{
    public DocumentsViewModel Docs { get; } = docs;
    public DepositForm Form { get; } = form;

    /// <summary>The backend's quote, once the amount is valid.</summary>
    public DepositQuote? Quote { get; } = quote;

    public IReadOnlyDictionary<string, string> Problems { get; } = problems;
    public ReferenceData Ref => Docs.Ref;
    public AppConfig Config => Docs.Config;

    public string? Problem(string key) => Problems.GetValueOrDefault(key);

    public PayoutOption? Payout => Ref.Payouts.FirstOrDefault(p => p.Code == Form.InterestPayout);
    public bool Cumulative => Payout?.PerYear == 0;

    /// <summary>The category the deposit is booked as, from Upload Documents.</summary>
    public string Category => Docs.State.Category.Length > 0 ? Docs.CategoryName(Docs.State.Category) : "Not chosen yet";

    /// <summary>The form that exempts the investor from TDS: 15H at the senior citizen age and over, 15G under it.</summary>
    public string TdsForm => SeniorByDob(Docs.Investor.Who.Dob, Config.SeniorAge) ? "Form 15H" : "Form 15G";

    /// <summary>Where an e-receipt goes: the investor's e-mail on Investor Information.</summary>
    public string Email => Docs.App.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor)?.Email ?? "";

    /// <summary>A renewal's amount is the deposit's maturity amount: shown, not typed.</summary>
    public bool AmountFixed => Docs.IsRenewal;

    public string AmountHint => AmountFixed ? $"{Money.InWords(Form.AmountValue)} · the maturity amount of deposit {Docs.Renewal!.DepositNumber}, renewed"
        : Form.AmountProblem(Config) is { } problem ? problem
        : $"{Money.InWords(Form.AmountValue)} · in multiples of {Money.Rupees(Config.AmountStep)}";

    /// <summary>What is still to do before Proceed opens, in words.</summary>
    public IReadOnlyList<string> Outstanding => [.. Problems.Values];

    public static bool SeniorByDob(string dob, int seniorAge) =>
        DateTime.TryParseExact(dob, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var born)
        && born.AddYears(seniorAge) <= DateTime.Today;
}

// ===== Review Summary and Submitted ==========================================
