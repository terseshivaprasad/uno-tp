using UnoTP.Backend.External;

namespace UnoTP.Tests;

/// <summary>A typed Aadhaar number: 12 digits, not starting 0 or 1, Verhoeff check digit last.</summary>
public class AadhaarNumbersTests
{
    [Theory]
    [InlineData("234567890124")]
    [InlineData("999941057058")]
    public void Accepts_a_number_whose_check_digit_holds(string number) => Assert.True(AadhaarNumbers.IsValid(number));

    [Theory]
    [InlineData("234567890123", "a slip in the last digit")]
    [InlineData("243567890124", "two digits swapped")]
    [InlineData("034567890124", "starts with 0")]
    [InlineData("134567890124", "starts with 1")]
    [InlineData("23456789012", "11 digits")]
    [InlineData("2345678901245", "13 digits")]
    [InlineData("2345 6789 0124", "spaces left in")]
    [InlineData("", "nothing")]
    public void Refuses(string number, string why) => Assert.False(AadhaarNumbers.IsValid(number), why);

    [Fact]
    public void A_masked_number_is_not_whole() => Assert.False(AadhaarNumbers.IsWhole("XXXXXXXX0124"));
}
