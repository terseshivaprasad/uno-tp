using System.Globalization;
using UnoTP.Models;

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
    public string SourceOfFunds { get => sourceOfFundsValue; set => sourceOfFundsValue = value ?? ""; }
    private string sourceOfFundsValue = "";
    public string SourceOfFundsRemark { get => sourceOfFundsRemarkValue; set => sourceOfFundsRemarkValue = (value ?? "").Trim(); }
    private string sourceOfFundsRemarkValue = "";
    /// <summary>On a renewal, what of the old deposit is renewed: a <see cref="RenewalChoice"/> code.</summary>
    public string RenewalFor { get => renewalForValue; set => renewalForValue = value ?? ""; }
    private string renewalForValue = "";

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
            Amount = saved.Amount > 0 ? saved.Amount.ToString(CultureInfo.InvariantCulture) : "",
            TenureMonths = saved.TenureMonths,
            InterestPayout = saved.Payout,
            AutoRenewal = saved.AutoRenewal,
            RenewInstruction = saved.RenewInstruction,
            NoTds = saved.NoTds,
            DeliveryType = saved.DeliveryType,
            SourceOfFunds = saved.SourceOfFunds,
            SourceOfFundsRemark = saved.SourceOfFundsRemark,
            RenewalFor = saved.RenewalFor,
        };

    /// <param name="sourceOfFunds">Whether, and why, the source of funds is asked for this deposit.</param>
    public DepositDetails ToDetails(SourceOfFundsCheck sourceOfFunds)
    {
        var renewInstruction = AutoRenewal ? RenewInstruction : "";
        var remark = sourceOfFunds.TakesRemark(SourceOfFunds) ? SourceOfFundsRemark : "";
        return new DepositDetails(AmountValue, TenureMonths, InterestPayout, AutoRenewal, renewInstruction, NoTds, DeliveryType,
            SourceOfFunds, remark, sourceOfFunds.Reason, RenewalFor);
    }

    /// <summary>
    /// A renewal's amount is the old deposit's, whatever was posted: its maturity
    /// amount, or its principal when the principal only is renewed. Until a choice
    /// is made, principal and interest are renewed.
    /// </summary>
    public void TakeRenewalAmount(RenewalOf renewal)
    {
        if (RenewalFor.Length == 0) RenewalFor = RenewalChoice.PrincipalInterest;
        Amount = Money.Group(renewal.AmountFor(RenewalFor));
    }

    /// <summary>Where the source of funds is not asked, nothing of it is kept.</summary>
    public void DropSourceOfFunds()
    {
        SourceOfFunds = "";
        SourceOfFundsRemark = "";
    }

    /// <summary>What is wrong with the amount, or null.</summary>
    public string? AmountProblem(AppConfig config)
    {
        var n = AmountValue;
        if (n == 0) return Messages.FdConfiguration.AmountRequired;
        if (n < config.MinAmount) return Messages.FdConfiguration.BelowMinimum(Money.Rupees(config.MinAmount));
        if (n > config.MaxAmount) return config.OverMaxAmountMessage.Length > 0 ? config.OverMaxAmountMessage : Messages.FdConfiguration.AboveMaximum(Money.Rupees(config.MaxAmount));
        // A step of 1 (or none) means any amount; a larger one, multiples of it.
        if (config.AmountStep > 1 && n % config.AmountStep != 0) return Messages.FdConfiguration.NotAMultiple(Money.Rupees(config.AmountStep), Money.InWords(n).ToLowerInvariant());
        return null;
    }

    /// <summary>What stops the step, by field.</summary>
    /// <param name="rates">The rate card at the amount posted: the tenure and payout must be on it, and offered at the amount.</param>
    /// <param name="sourceOfFunds">Whether the source of funds is asked for this deposit.</param>
    /// <param name="renewal">The deposit a renewal renews: the amount is its own, not the partner's to change or the limits' to check; null for a new deposit.</param>
    public Dictionary<string, string> Problems(AppConfig config, RateTable rates, SourceOfFundsCheck sourceOfFunds, RenewalOf? renewal = null)
    {
        var reference = rates.Reference;
        var problems = new Dictionary<string, string>();
        if (renewal is null && AmountProblem(config) is { } amount) problems["Amount"] = amount;

        if (renewal is not null)
        {
            if (reference.RenewInstructions.All(r => r.Code != RenewalFor)) problems["RenewalFor"] = Messages.FdConfiguration.RenewalOfRequired;
            else if (RenewalFor == RenewalChoice.Principal && !renewal.PrincipalKnown) problems["RenewalFor"] = Messages.FdConfiguration.PrincipalNotKnown;
        }

        if (!rates.Tenures.Contains(TenureMonths))
        {
            problems["TenureMonths"] = Messages.FdConfiguration.TenureRequired;
        }
        else if (!rates.OffersTenure(TenureMonths))
        {
            problems["TenureMonths"] = Messages.FdConfiguration.TenureNotOffered(TenureMonths);
        }

        var payout = rates.Payouts.FirstOrDefault(p => p.Code == InterestPayout);
        if (payout is null)
        {
            problems["InterestPayout"] = Messages.FdConfiguration.PayoutRequired;
        }
        else if (!rates.OffersPayout(InterestPayout))
        {
            problems["InterestPayout"] = Messages.FdConfiguration.PayoutNotOffered(payout.Name.ToLowerInvariant());
        }

        if (AutoRenewal && reference.RenewInstructions.All(r => r.Code != RenewInstruction)) problems["RenewInstruction"] = Messages.FdConfiguration.AutoRenewalOfRequired;
        if (reference.DeliveryTypes.All(d => d.Code != DeliveryType)) problems["DeliveryType"] = Messages.FdConfiguration.DeliveryTypeRequired;

        if (sourceOfFunds.Asked)
        {
            var sources = reference.SourcesOfFunds ?? [];
            if (sources.All(s => s.Code != SourceOfFunds)) problems["SourceOfFunds"] = Messages.FdConfiguration.SourceOfFundsRequired;
            else if (sourceOfFunds.TakesRemark(SourceOfFunds) && SourceOfFundsRemark.Length == 0) problems["SourceOfFundsRemark"] = Messages.FdConfiguration.SourceOfFundsRemarkRequired;
            else if (sourceOfFunds.TakesRemark(SourceOfFunds) && SourceOfFundsRemark.Length > InputRules.MaxRemark) problems["SourceOfFundsRemark"] = InputRules.TooLong(InputRules.MaxRemark);
            else if (sourceOfFunds.TakesRemark(SourceOfFunds) && !InputRules.IsClean(SourceOfFundsRemark)) problems["SourceOfFundsRemark"] = InputRules.OnlyAllowed;
        }
        return problems;
    }
}

/// <summary>FD Configuration: the deposit as configured, the rate card it is offered from, and what the backend quotes for it.</summary>
/// <param name="publicRates">The public category's card at the same amount, which a senior citizen's or a women's rate is compared with.</param>
public sealed class DepositViewModel(DocumentsViewModel docs, DepositForm form, RateTable rates, RateTable publicRates, DepositQuote? quote,
    SourceOfFundsCheck sourceOfFunds, IReadOnlyDictionary<string, string> problems)
{
    public DocumentsViewModel Docs { get; } = docs;
    public DepositForm Form { get; } = form;

    /// <summary>Whether the source of funds is asked at this amount, and why.</summary>
    public SourceOfFundsCheck SourceOfFunds { get; } = sourceOfFunds;

    /// <summary>The sources of funds to choose from.</summary>
    public IReadOnlyList<Option> SourcesOfFunds => Ref.SourcesOfFunds ?? [];

    /// <summary>Whether the remark box is open: the field is asked and "Other" is chosen.</summary>
    public bool RemarkOpen => SourceOfFunds.Asked && SourceOfFunds.TakesRemark(Form.SourceOfFunds);

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

    /// <summary>The amount in words, under the figure; empty before an amount is entered.</summary>
    public string AmountInWords
    {
        get
        {
            if (Form.AmountValue <= 0) return "";
            return Money.InWords(Form.AmountValue);
        }
    }

    /// <summary>
    /// The banner at the foot of the quote for a senior citizen or women's category:
    /// what its rate comes to over the public rate, for the tenure and payout chosen.
    /// The two cards are compared row for row, so a benefit that starts at a longer
    /// tenure shows only from there. Null for the public category, an employee
    /// category, one not chosen yet, or where the rate is no higher than the public one.
    /// </summary>
    public string? ExtraRateBanner
    {
        get
        {
            var category = Ref.Categories.FirstOrDefault(c => c.Code == Docs.State.Category);
            if (category is null) return null;
            if (category.Employee) return null;
            if (!category.Senior && !category.Women) return null;

            var own = Rates.Row(Form.TenureMonths, Form.InterestPayout);
            var open = publicRates.Row(Form.TenureMonths, Form.InterestPayout);
            if (own is null || open is null) return null;
            var extra = own.Rate - open.Rate;
            if (extra <= 0) return null;
            return $"{category.Name} rate: includes an additional {extra:0.00}% a year over the public rate.";
        }
    }

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
            if (Row is null && Form.AmountProblem(Docs.Config) is not null) return "The rate is quoted once a valid amount is entered.";
            if (Row is null && Payout is not null && !Rates.OffersPayout(Form.InterestPayout))
            {
                return Messages.FdConfiguration.PayoutNotOfferedChooseAnother(Payout.Name.ToLowerInvariant());
            }
            if (Row is null && !Rates.OffersTenure(Form.TenureMonths))
            {
                return Messages.FdConfiguration.TenureNotOfferedChooseAnother(Form.TenureMonths);
            }
            if (Row is null) return "The rate is quoted once a valid amount is entered.";
            return $"Rate is the card rate for a {Form.TenureMonths}-month deposit of {Money.Rupees(Rates.Amount)} as on {Money.Day(Row.AsOn)}.";
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

    /// <summary>A renewal's amount is the old deposit's: shown, not typed.</summary>
    public bool AmountFixed => Docs.IsRenewal;

    /// <summary>On a renewal, what of the old deposit the amount is, for the line beside it.</summary>
    public string RenewedAmountIs(string renewalFor)
    {
        if (renewalFor == RenewalChoice.Principal) return $"the principal of deposit {Docs.Renewal!.DepositNumber}";
        return $"the maturity amount of deposit {Docs.Renewal!.DepositNumber}";
    }

    public string AmountHint => AmountFixed ? $"{Money.InWords(Form.AmountValue)} · {RenewedAmountIs(Form.RenewalFor)}, renewed on {Money.Day(Docs.Renewal!.MaturesOn)} at the card rate on the day of application"
        : Form.AmountProblem(Config) is { } problem ? problem
        : Config.AmountStep > 1 ? $"{Money.InWords(Form.AmountValue)} · in multiples of {Money.Rupees(Config.AmountStep)}"
        : Money.InWords(Form.AmountValue);

    /// <summary>What is still to do before Proceed opens, in words.</summary>
    public IReadOnlyList<string> Outstanding => [.. Problems.Values];

}

// ===== Review Summary and Submitted ==========================================
