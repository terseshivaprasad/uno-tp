using UnoTP.Backend;
using UnoTP.Backend.Mock;

namespace UnoTP.Tests;

/// <summary>
/// The Samruddhi rate chart as the mock carries it, and the sums the chart prints:
/// the "amount payable" on ₹5,000 for a cumulative deposit, and what each category
/// earns over the public rate.
/// </summary>
public class RateChartTests
{
    private static readonly RatesRequest PublicMan = new("PUBLIC/GENERAL", "M", RateCard.Purchase);

    [Theory]
    [InlineData(12, 5330)]
    [InlineData(18, 5506)]
    [InlineData(24, 5708)]
    [InlineData(30, 5904)]
    [InlineData(36, 6194)]
    [InlineData(42, 6423)]
    [InlineData(48, 6665)]
    [InlineData(60, 7161)]
    public async Task A_cumulative_deposit_of_5000_comes_to_what_the_chart_prints(int months, int amountPayable)
    {
        var quote = await new MockDeposits().QuoteAsync(new QuoteRequest(5_000, months, "maturity", PublicMan));
        Assert.Equal(amountPayable, quote.MaturityAmount);
    }

    [Theory]
    [InlineData("PUBLIC/GENERAL", "maturity", 6.60)]
    [InlineData("PUBLIC/GENERAL", "monthly", 6.40)]
    [InlineData("PUBLIC/GENERAL", "quarterly", 6.45)]
    [InlineData("PUBLIC/GENERAL", "halfyearly", 6.50)]
    [InlineData("PUBLIC/GENERAL", "yearly", 6.60)]
    [InlineData("SR CITIZEN", "maturity", 6.95)]
    [InlineData("WOMEN", "yearly", 6.65)]
    [InlineData("SR CITIZEN WOMEN", "monthly", 6.80)]
    [InlineData("EMPLOYEE", "quarterly", 6.80)]
    [InlineData("EMPLOYEE WOMEN", "halfyearly", 6.90)]
    public async Task Each_category_earns_the_chart_rate_plus_its_addition(string category, string payout, decimal rate)
    {
        var card = await new MockDeposits().RatesAsync(new RatesRequest(category, "M", RateCard.Purchase));
        var line = card.Single(row => row.TenureMonths == 12 && row.Payout == payout);
        Assert.Equal(rate, line.Rate);
    }

    [Fact]
    public async Task A_payout_is_offered_from_its_minimum_amount()
    {
        var card = await new MockDeposits().RatesAsync(PublicMan);
        Assert.True(card.Single(row => row.TenureMonths == 12 && row.Payout == "maturity").Offers(5_000));
        Assert.False(card.Single(row => row.TenureMonths == 12 && row.Payout == "yearly").Offers(20_000));
        Assert.True(card.Single(row => row.TenureMonths == 12 && row.Payout == "yearly").Offers(25_000));
        Assert.False(card.Single(row => row.TenureMonths == 12 && row.Payout == "monthly").Offers(49_000));
        Assert.True(card.Single(row => row.TenureMonths == 12 && row.Payout == "monthly").Offers(50_000));
        Assert.False(card.Single(row => row.TenureMonths == 12 && row.Payout == "monthly").Offers(5_00_00_001));
    }

    [Fact]
    public void Interest_each_period_is_simple_interest_rounded_to_the_rupee()
    {
        Assert.Equal(533, DepositMaths.InterestEach(1_00_000, 6.40m, 12));
        // ₹1,612.50: the half rupee rounds up, as the page always rounded it.
        Assert.Equal(1_613, DepositMaths.InterestEach(1_00_000, 6.45m, 4));
        Assert.Equal(0, DepositMaths.InterestEach(1_00_000, 6.60m, 0));
    }
}
