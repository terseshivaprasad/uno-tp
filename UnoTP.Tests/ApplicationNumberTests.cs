using UnoTP.Data;

namespace UnoTP.Tests;

public class ApplicationNumberTests
{
    [Theory]
    [InlineData(false, "B", "C")]
    [InlineData(true, "B", "B")]
    public void A_purchase_is_bus_type_B_and_its_source_says_who_asked(bool branchUser, string busType, string source)
    {
        Assert.Equal((busType, source), ApplicationNumber.Codes(branchUser, renewal: false));
    }

    [Theory]
    [InlineData(false, "C", "R")]
    [InlineData(true, "B", "R")]
    public void A_renewal_is_source_R_and_its_bus_type_says_who_asked(bool branchUser, string busType, string source)
    {
        Assert.Equal((busType, source), ApplicationNumber.Codes(branchUser, renewal: true));
    }
}
