using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>Investor Information's own rules: a minor nominee, a PIN code, and what Proceed will not go on without.</summary>
public class InvestorRulesTests
{
    private static readonly DateTime Today = new(2026, 9, 30);

    [Theory]
    [InlineData("30", "09", "2008", false, "turns 18 today, so is not a minor")]
    [InlineData("01", "10", "2008", true, "turns 18 tomorrow")]
    [InlineData("14", "08", "1988", false, "an adult")]
    [InlineData("31", "02", "2010", false, "not a date, so nobody is asked for a guardian")]
    [InlineData("01", "01", "2027", false, "not born yet")]
    public void A_nominee_is_a_minor_under_18(string dd, string mm, string yyyy, bool minor, string why) =>
        Assert.True(minor == InvestorViewModel.IsMinor(dd, mm, yyyy, Today, 18), why);

    [Theory]
    [InlineData("400001", true)]
    [InlineData("110001", true)]
    [InlineData("012345", false)]
    [InlineData("40001", false)]
    [InlineData("4000010", false)]
    [InlineData("40000A", false)]
    public void A_PIN_code_is_six_digits_not_starting_with_zero(string pin, bool valid) =>
        Assert.Equal(valid, InvestorViewModel.IsPin(pin));

    [Theory]
    [InlineData("ANJALI VIKRAM PATIL", true)]
    [InlineData("Karan D Mehta", true)]
    [InlineData("Karan D. Mehta", false)]
    [InlineData("O'Brien-Smith", false)]
    [InlineData("ANJALI 2", false)]
    [InlineData("ANJALI@PATIL", false)]
    [InlineData("<script>", false)]
    [InlineData(" ANJALI", false)]
    public void A_name_is_letters_only(string value, bool ok) => Assert.Equal(ok, InvestorViewModel.IsName(value));

    [Theory]
    [InlineData("Flat 4, Sea View", true)]
    [InlineData("12/3 M.G. Road, near SBI - 2 & Co.", true)]
    [InlineData("12/3 M.G. Road (near SBI) #2", false)]
    [InlineData("Flat 4; drop table", false)]
    [InlineData("Flat <b>4</b>", false)]
    [InlineData("Flat 4 @ Sea View", false)]
    public void An_address_line_carries_no_special_characters(string value, bool ok) => Assert.Equal(ok, InvestorViewModel.IsText(value));

    [Fact]
    public void A_name_or_address_with_special_characters_is_refused_and_a_blank_optional_line_is_not()
    {
        var state = new InvestorInfoState { Nominee = true };
        state.Fields["Nominee.Name"] = "ARJUN #1";
        state.Fields["Nominee.Relation"] = "Son";
        var born = DateTime.Today.AddYears(-10);
        state.Fields["Nominee.Dd"] = born.Day.ToString(); state.Fields["Nominee.Mm"] = born.Month.ToString(); state.Fields["Nominee.Yyyy"] = born.Year.ToString();
        state.Fields["Nominee.GuardianName"] = "VIKRAM PATIL";
        state.Fields["Nominee.GuardianAddress.Line1"] = "Flat 4, Sea View";
        state.Fields["Nominee.GuardianAddress.Line2"] = "";
        state.Fields["Nominee.GuardianAddress.Line3"] = "Bandra <west>";
        state.Fields["Nominee.GuardianAddress.PinCode"] = "400050";
        state.Fields["Nominee.GuardianAddress.City"] = "Mumbai";

        var missing = InvestorViewModel.Unfilled(state, [], 18);

        Assert.Equal([("Nominee.Name", InvestorViewModel.LettersOnly), ("Nominee.GuardianAddress.Line3", InvestorViewModel.NoSpecialCharacters)],
            missing.Select(u => (u.Field, u.Error)));
    }

    [Fact]
    public void An_adult_nominee_needs_a_name_date_of_birth_and_relation_but_no_guardian()
    {
        var state = new InvestorInfoState { Nominee = true };
        state.Fields["Nominee.Name"] = "";
        state.Fields["Nominee.Dd"] = "14"; state.Fields["Nominee.Mm"] = "08"; state.Fields["Nominee.Yyyy"] = "1988";

        var missing = InvestorViewModel.Unfilled(state, [], 18).Select(u => u.Field).ToList();

        Assert.Equal(["Nominee.Name", "Nominee.Relation"], missing);
    }

    [Fact]
    public void A_minor_nominee_needs_a_guardian_with_an_address()
    {
        var state = new InvestorInfoState { Nominee = true };
        state.Fields["Nominee.Name"] = "ARJUN PATIL";
        state.Fields["Nominee.Relation"] = "Son";
        var born = DateTime.Today.AddYears(-10);
        state.Fields["Nominee.Dd"] = born.Day.ToString(); state.Fields["Nominee.Mm"] = born.Month.ToString(); state.Fields["Nominee.Yyyy"] = born.Year.ToString();
        state.Fields["Nominee.GuardianAddress.PinCode"] = "4110";

        var missing = InvestorViewModel.Unfilled(state, [], 18);

        Assert.Equal(["Nominee.GuardianName", "Nominee.GuardianAddress.Line1", "Nominee.GuardianAddress.PinCode", "Nominee.GuardianAddress.City"],
            missing.Select(u => u.Field));
        Assert.Contains(missing, u => u.Field == "Nominee.GuardianAddress.PinCode" && u.Error == "Enter a 6-digit PIN code");
    }

    [Fact]
    public void A_future_date_of_birth_is_refused()
    {
        var state = new InvestorInfoState { Nominee = true };
        state.Fields["Nominee.Name"] = "ARJUN PATIL";
        state.Fields["Nominee.Relation"] = "Son";
        var next = DateTime.Today.AddDays(1);
        state.Fields["Nominee.Dd"] = next.Day.ToString(); state.Fields["Nominee.Mm"] = next.Month.ToString(); state.Fields["Nominee.Yyyy"] = next.Year.ToString();

        var missing = InvestorViewModel.Unfilled(state, [], 18);

        Assert.Single(missing);
        Assert.Equal("Nominee.Dob", missing[0].Field);
    }
}
