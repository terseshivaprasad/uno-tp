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
    public static DepositForm From(DepositDetails? saved, RateTable rates) => saved is null
        ? new DepositForm
        {
            TenureMonths = rates.Tenures.FirstOrDefault(),
            InterestPayout = rates.Payouts.FirstOrDefault()?.Code ?? "",
            DeliveryType = rates.Reference.DeliveryTypes.FirstOrDefault()?.Code ?? "",
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
    /// <param name="rates">The rate card at the amount posted: the tenure and payout must be on it, and offered at the amount.</param>
    /// <param name="amountFixed">A renewal: the amount is the deposit's maturity amount, not the partner's to change or the limits' to check.</param>
    public Dictionary<string, string> Problems(AppConfig config, RateTable rates, bool amountFixed = false)
    {
        var reference = rates.Reference;
        var problems = new Dictionary<string, string>();
        if (!amountFixed && AmountProblem(config) is { } amount) problems["Amount"] = amount;

        if (!rates.Tenures.Contains(TenureMonths))
        {
            problems["TenureMonths"] = "Choose the tenure";
        }
        else if (rates.TenureBlockedReason(TenureMonths) is { } tenureBlocked)
        {
            problems["TenureMonths"] = $"A {TenureMonths}-month deposit is {tenureBlocked}";
        }

        var payout = rates.Payouts.FirstOrDefault(p => p.Code == InterestPayout);
        if (payout is null)
        {
            problems["InterestPayout"] = "Choose the interest payout";
        }
        else if (rates.PayoutBlockedReason(InterestPayout) is { } payoutBlocked)
        {
            problems["InterestPayout"] = $"A {payout.Name.ToLowerInvariant()} payout is {payoutBlocked}";
        }

        if (AutoRenewal && reference.RenewInstructions.All(r => r.Code != RenewInstruction)) problems["RenewInstruction"] = "Required — choose what auto renewal renews";
        if (reference.DeliveryTypes.All(d => d.Code != DeliveryType)) problems["DeliveryType"] = "Choose the delivery type";
        return problems;
    }
}

/// <summary>FD Configuration: the deposit as configured, the rate card it is offered from, and what the backend quotes for it.</summary>
public sealed class DepositViewModel(DocumentsViewModel docs, DepositForm form, RateTable rates, DepositQuote? quote, IReadOnlyDictionary<string, string> problems)
{
    public DocumentsViewModel Docs { get; } = docs;
    public DepositForm Form { get; } = form;

    /// <summary>The rate card at the amount entered, or at the standing amount before one is.</summary>
    public RateTable Rates { get; } = rates;

    /// <summary>The tenures the card offers, for the page's choices.</summary>
    public IReadOnlyList<int> Tenures => Rates.Tenures;

    /// <summary>The payouts the card offers, for the page's choices.</summary>
    public IReadOnlyList<PayoutOption> Payouts => Rates.Payouts;

    /// <summary>The card's row for the tenure and payout chosen, at the amount; null when it is not offered at it.</summary>
    public RateOption? Row => Rates.Row(Form.TenureMonths, Form.InterestPayout);

    /// <summary>The backend's quote, once the amount is valid and the card offers the tenure and payout at it.</summary>
    public DepositQuote? Quote { get; } = quote;

    /// <summary>The scheme as the card names it: "Cumulative" or "Non-cumulative".</summary>
    public string Scheme
    {
        get
        {
            if (Row is not null) return Row.Scheme == RateCard.Cumulative ? "Cumulative" : "Non-cumulative";
            return Cumulative ? "Cumulative" : "Non-cumulative";
        }
    }

    /// <summary>What the quote panel says under the figures.</summary>
    public string QuoteNote
    {
        get
        {
            if (Quote is not null) return $"Rate is the card rate for a {Form.TenureMonths}-month deposit as on {Money.Day(Quote.RateAsOn)}, and is locked when the application is submitted.";
            if (Row is null && Payout is not null && Rates.PayoutBlockedReason(Form.InterestPayout) is { } blocked)
            {
                return $"A {Payout.Name.ToLowerInvariant()} payout is {blocked}. Choose another payout, or change the amount.";
            }
            if (Row is null && Rates.TenureBlockedReason(Form.TenureMonths) is { } tenureBlocked)
            {
                return $"A {Form.TenureMonths}-month deposit is {tenureBlocked}. Choose another tenure, or change the amount.";
            }
            if (Row is null) return "The rate and the returns are quoted once a valid amount is entered.";
            return $"Rate is the card rate for a {Form.TenureMonths}-month deposit of {Money.Rupees(Rates.Amount)} as on {Money.Day(Row.AsOn)}. The returns are worked out once a valid amount is entered.";
        }
    }

    public IReadOnlyDictionary<string, string> Problems { get; } = problems;
    public ReferenceData Ref => Docs.Ref;
    public AppConfig Config => Docs.Config;

    public string? Problem(string key) => Problems.GetValueOrDefault(key);

    public PayoutOption? Payout => Ref.Payouts.FirstOrDefault(p => p.Code == Form.InterestPayout);
    public bool Cumulative => Payout?.PerYear == 0;

    /// <summary>The category the deposit is booked as, from Upload Documents.</summary>
    public string Category => Docs.State.Category.Length > 0 ? Docs.CategoryName(Docs.State.Category) : "Not chosen yet";

    /// <summary>The form that exempts the investor from TDS: one form, Form 121, whatever the investor's age.</summary>
    public string TdsForm => "Form 121";

    /// <summary>Where an e-receipt goes: the investor's e-mail on Investor Information.</summary>
    public string Email => Docs.App.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor)?.Email ?? "";

    /// <summary>A renewal's amount is the deposit's maturity amount: shown, not typed.</summary>
    public bool AmountFixed => Docs.IsRenewal;

    public string AmountHint => AmountFixed ? $"{Money.InWords(Form.AmountValue)} · the maturity amount of deposit {Docs.Renewal!.DepositNumber}, renewed on {Money.Day(Docs.Renewal.MaturesOn)} at the rate prevailing then"
        : Form.AmountProblem(Config) is { } problem ? problem
        : $"{Money.InWords(Form.AmountValue)} · in multiples of {Money.Rupees(Config.AmountStep)}";

    /// <summary>What is still to do before Proceed opens, in words.</summary>
    public IReadOnlyList<string> Outstanding => [.. Problems.Values];

}

// ===== Review Summary and Submitted ==========================================
