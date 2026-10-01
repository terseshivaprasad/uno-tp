namespace UnoTP.Models;

/// <summary>
/// How a deposit's returns are worked out, the way the published rate chart works
/// them.
/// </summary>
public static class DepositMaths
{
    /// <summary>
    /// What a cumulative deposit comes to at maturity. Interest compounds
    /// <paramref name="compoundingPerYear"/> times a year (once, on the Samruddhi
    /// chart); the months left over after the last whole period earn simple
    /// interest on the sum so far. Rounded to the rupee.
    /// </summary>
    public static decimal MaturityAmount(decimal amount, decimal rate, int months, int compoundingPerYear)
    {
        var monthsPerPeriod = 12 / compoundingPerYear;
        var wholePeriods = months / monthsPerPeriod;
        var monthsLeft = months - wholePeriods * monthsPerPeriod;

        var ratePerPeriod = rate / 100 / compoundingPerYear;
        var sum = amount;
        for (var i = 0; i < wholePeriods; i++)
        {
            sum = sum * (1 + ratePerPeriod);
        }
        sum = sum * (1 + rate / 100 * monthsLeft / 12);
        return Math.Round(sum, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>What each payout of a non-cumulative deposit pays: simple interest for the period, rounded to the rupee.</summary>
    public static decimal InterestEach(decimal amount, decimal rate, int payoutsPerYear)
    {
        if (payoutsPerYear == 0) return 0;
        return Math.Round(amount * rate / 100 / payoutsPerYear, 0, MidpointRounding.AwayFromZero);
    }
}
