using UnoTP.Models;

namespace UnoTP.Tests;

public class AddressTests
{
    [Theory]
    [InlineData("22 PARK STREET, KOLKATA, WEST BENGAL 700016", "22 PARK STREET, KOLKATA, WEST BENGAL", "700016")]
    [InlineData("12 MG ROAD, PATNA, BIHAR - 800 001", "12 MG ROAD, PATNA, BIHAR", "800001")]
    [InlineData("Flat 4, Pune, Maharashtra, Pune, 411001", "Flat 4, Pune, Maharashtra, Pune", "411001")]
    public void The_pin_code_is_the_last_six_digits_and_comes_off_the_address(string address, string lines, string pin)
    {
        Assert.Equal((lines, pin), Addresses.SplitPin(address));
    }

    [Fact]
    public void An_address_with_no_pin_code_gives_none()
    {
        Assert.Equal(("9 Lake View, Thane", ""), Addresses.SplitPin("9 Lake View, Thane"));
    }
}
