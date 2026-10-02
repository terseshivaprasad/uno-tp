using UnoTP.Models;

namespace UnoTP.Tests;

public class DepositMathsTests
{
    [Theory]
    // Compounded once a year: 1,00,000 at 8% is 1,08,000 after a year and 1,16,640 after two.
    [InlineData(100000, 8, 12, 1, 108000)]
    [InlineData(100000, 8, 24, 1, 116640)]
    // The six months left over after the whole year earn simple interest on 1,08,000.
    [InlineData(100000, 8, 18, 1, 112320)]
    // Under a year, nothing compounds: simple interest for the months.
    [InlineData(100000, 8, 6, 1, 104000)]
    // Compounded quarterly: 1,00,000 x 1.02^4 = 1,08,243.22, to the rupee.
    [InlineData(100000, 8, 12, 4, 108243)]
    public void A_cumulative_deposit_matures_at_the_compounded_amount(decimal amount, decimal rate, int months, int perYear, decimal expected)
    {
        Assert.Equal(expected, DepositMaths.MaturityAmount(amount, rate, months, perYear));
    }

    [Theory]
    [InlineData(100000, 8, 4, 2000)]
    [InlineData(100000, 8, 1, 8000)]
    // 645.83 a month, to the rupee.
    [InlineData(100000, 7.75, 12, 646)]
    // 12.5 rounds up, away from zero.
    [InlineData(1000, 15, 12, 13)]
    public void Each_payout_is_simple_interest_for_its_period(decimal amount, decimal rate, int payoutsPerYear, decimal expected)
    {
        Assert.Equal(expected, DepositMaths.InterestEach(amount, rate, payoutsPerYear));
    }

    [Fact]
    public void A_deposit_with_no_payouts_pays_nothing_out()
    {
        Assert.Equal(0, DepositMaths.InterestEach(100000, 8, 0));
    }
}
