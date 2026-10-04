using UnoTP.Models;

namespace UnoTP.Tests;

public class AadhaarNumberTests
{
    [Theory]
    [InlineData("2345 6789 9012", "9012")]
    [InlineData("234567899012", "9012")]
    [InlineData("XXXX XXXX 9012", "9012")]
    [InlineData("xxxxxxxx9012", "9012")]
    [InlineData("XXXX-XXXX-9012", "9012")]
    [InlineData("9012", "9012")]
    public void The_last_four_digits_are_read_off_a_whole_or_a_masked_number(string read, string lastFour)
    {
        Assert.Equal(lastFour, AadhaarNumbers.LastFour(read));
    }

    [Theory]
    [InlineData("")]
    [InlineData("XXXX XXXX XXXX")]
    [InlineData("2345 6789 90")]
    [InlineData("XXXX XXXX 90I2")]
    [InlineData("6789 9012")]
    public void A_number_read_in_part_or_not_at_all_gives_no_last_four(string read)
    {
        Assert.Equal("", AadhaarNumbers.LastFour(read));
    }
}
