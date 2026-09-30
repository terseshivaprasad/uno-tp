using UnoTP.Backend;
using UnoTP.Backend.Mock;
using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>
/// When FD Configuration asks the source of funds: the investor's active deposits
/// with us, with the new one, pass ₹1 crore, and they are a homemaker, student or
/// retired, or earn up to ₹5 lakh a year.
/// </summary>
public class SourceOfFundsTests
{
    private static readonly AppConfig Config = new MockReference().ConfigAsync().Result;
    private const long Crore = 1_00_00_000;

    private static HolderDetails Investor(string occupation, string income) =>
        new(HolderType.Investor, Occupation: occupation, AnnualIncome: income);

    [Fact]
    public void Asked_over_a_crore_for_a_homemaker()
    {
        var check = SourceOfFundsCheck.For(Config, heldTotal: 80_00_000, freshAmount: 25_00_000, Investor("Homemaker", "Rs.10,00,000 - Rs.25,00,000"));
        Assert.True(check.Asked);
        Assert.Equal(1_05_00_000, check.Total);
        Assert.Contains("their occupation is homemaker", check.Why);
    }

    [Fact]
    public void Asked_over_a_crore_on_a_low_income_whatever_the_occupation()
    {
        var check = SourceOfFundsCheck.For(Config, heldTotal: Crore, freshAmount: 1_000, Investor("Business", "Upto Rs.5,00,000"));
        Assert.True(check.Asked);
        Assert.Contains("their annual income is upto rs.5,00,000", check.Why);
    }

    [Fact]
    public void Not_asked_at_exactly_a_crore()
    {
        var check = SourceOfFundsCheck.For(Config, heldTotal: 50_00_000, freshAmount: 50_00_000, Investor("Student", "Upto Rs.5,00,000"));
        Assert.False(check.Asked);
        Assert.Contains("Asked when the investor's deposits with us, with this one, pass ₹ 1,00,00,000 (they hold ₹ 50,00,000 now) and they are a homemaker, student or retired, or their annual income is upto rs.5,00,000.", check.Why);
    }

    [Fact]
    public void Not_asked_over_a_crore_for_a_salaried_investor_on_a_higher_income()
    {
        var check = SourceOfFundsCheck.For(Config, heldTotal: 2 * Crore, freshAmount: 10_00_000, Investor("Salaried", "Above Rs.25,00,000"));
        Assert.False(check.Asked);
    }

    [Fact]
    public void Not_asked_before_Investor_Information_is_saved()
    {
        Assert.False(SourceOfFundsCheck.For(Config, heldTotal: 2 * Crore, freshAmount: 10_00_000, investor: null).Asked);
    }

    [Fact]
    public void Other_needs_a_remark_and_a_source_is_kept_only_where_it_is_asked()
    {
        var reference = new MockReference().ReferenceAsync().Result;
        var rates = new RateTable(new MockDeposits().RatesAsync(new("PUBLIC/GENERAL", "M", RateCard.Purchase)).Result, reference, 10_00_000);
        var asked = SourceOfFundsCheck.For(Config, Crore, 10_00_000, Investor("Retired", "Upto Rs.5,00,000"));
        var notAsked = SourceOfFundsCheck.For(Config, 0, 10_00_000, Investor("Retired", "Upto Rs.5,00,000"));

        var form = new DepositForm { Amount = "10,00,000", TenureMonths = 12, InterestPayout = "maturity", DeliveryType = "ereceipt" };
        Assert.Equal("Required — choose the source of funds", form.Problems(Config, rates, asked)["SourceOfFunds"]);
        Assert.False(form.Problems(Config, rates, notAsked).ContainsKey("SourceOfFunds"));

        form.SourceOfFunds = "other";
        Assert.Equal("Required — say what the source of funds is", form.Problems(Config, rates, asked)["SourceOfFundsRemark"]);

        form.SourceOfFundsRemark = "  Sale of a car  ";
        Assert.Empty(form.Problems(Config, rates, asked));
        Assert.Equal("Sale of a car", form.ToDetails().SourceOfFundsRemark);

        // A remark typed under another source is not kept; nothing is, where it is not asked.
        form.SourceOfFunds = "savings";
        Assert.Equal("", form.ToDetails().SourceOfFundsRemark);
        form.DropSourceOfFunds();
        Assert.Equal("", form.ToDetails().SourceOfFunds);
    }
}
