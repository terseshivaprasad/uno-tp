using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// When FD Configuration asks where the money comes from: once the investor's active
/// deposits with us, with this one, pass AppConfig.SourceOfFundsFrom (₹1 crore) and
/// their occupation is one of AppConfig.SourceOfFundsOccupations (homemaker, student,
/// retired) or their annual income band is one of AppConfig.SourceOfFundsIncomeBands
/// (up to ₹5 lakh). "Other" takes a typed remark.
/// </summary>
public sealed class SourceOfFundsCheck
{
    /// <summary>The source of funds, by its code, that takes a remark (AppConfig.SourceOfFundsOther).</summary>
    public string Other { get; private init; } = "";

    /// <summary>Whether a source of funds, by its code, is the one that takes a remark.</summary>
    public bool TakesRemark(string source) => Other.Length > 0 && source == Other;

    /// <summary>Whether the field is asked for this deposit.</summary>
    public bool Asked { get; private init; }

    /// <summary>Why it is asked, or when it would be: the line under the field.</summary>
    public string Why { get; private init; } = "";

    /// <summary>The rule that asked it, as the FD system keeps it: "Occupation" or "Annual Income"; empty when not asked.</summary>
    public string Reason { get; private init; } = "";

    public static SourceOfFundsCheck For(AppConfig config, long heldTotal, long freshAmount, HolderDetails? investor)
    {
        var total = heldTotal + freshAmount;
        var overLimit = total > config.SourceOfFundsFrom;

        var occupations = config.SourceOfFundsOccupations ?? [];
        var incomeBands = config.SourceOfFundsIncomeBands ?? [];
        var occupation = investor?.Occupation ?? "";
        var income = investor?.AnnualIncome ?? "";
        var byOccupation = occupations.Contains(occupation, StringComparer.OrdinalIgnoreCase);
        var byIncome = incomeBands.Contains(income, StringComparer.OrdinalIgnoreCase);

        var asked = overLimit && (byOccupation || byIncome);

        var reason = "";
        if (asked && byOccupation) reason = "Occupation";
        else if (asked) reason = "Annual Income";

        string why;
        if (asked)
        {
            var because = byOccupation ? $"their occupation is {occupation.ToLowerInvariant()}" : $"their annual income is {income.ToLowerInvariant()}";
            why = $"Asked: the investor's deposits with us come to {Money.Rupees(total)} with this one, and {because}.";
        }
        else
        {
            var whom = string.Join(", ", occupations.Select(o => o.ToLowerInvariant()));
            var lastComma = whom.LastIndexOf(", ", StringComparison.Ordinal);
            if (lastComma > 0) whom = whom[..lastComma] + " or " + whom[(lastComma + 2)..];
            var bands = string.Join(" or ", incomeBands.Select(b => b.ToLowerInvariant()));
            why = $"Asked when the investor's deposits with us, with this one, pass {Money.Rupees(config.SourceOfFundsFrom)} (they hold {Money.Rupees(heldTotal)} now) and they are a {whom}, or their annual income is {bands}.";
        }

        return new SourceOfFundsCheck { Asked = asked, Why = why, Reason = reason, Other = config.SourceOfFundsOther };
    }
}
