using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>A name read off a document against the holder's: match, partial (initials) or mismatch.</summary>
public class NameMatchTests
{
    [Theory]
    [InlineData("ANJALI VIKRAM PATIL", "ANJALI VIKRAM PATIL")]
    [InlineData("anjali vikram patil", "ANJALI VIKRAM PATIL")]
    [InlineData("PATIL ANJALI VIKRAM", "ANJALI VIKRAM PATIL")]
    [InlineData("ANJALI  VIKRAM-PATIL", "ANJALI VIKRAM PATIL")]
    public void Same_words_match_whatever_the_case_order_or_punctuation(string read, string holder) =>
        Assert.Equal("match", UnoTP.Backend.Mock.External.MockNameMatch.Compare(read, holder));

    [Theory]
    [InlineData("KARAN D MEHTA", "KARAN DEEPAK MEHTA")]
    [InlineData("K D MEHTA", "KARAN DEEPAK MEHTA")]
    [InlineData("KARAN MEHTA", "KARAN DEEPAK MEHTA")]
    public void Initials_or_a_name_left_out_are_a_partial_match(string read, string holder) =>
        Assert.Equal("partial", UnoTP.Backend.Mock.External.MockNameMatch.Compare(read, holder));

    [Theory]
    [InlineData("RAHUL SUDHIR TAMBE", "ANJALI VIKRAM PATIL")]
    [InlineData("ANJALI VIKRAM SHAH", "ANJALI VIKRAM PATIL")]
    [InlineData("A V P", "ANJALI VIKRAM PATIL")]
    [InlineData("", "ANJALI VIKRAM PATIL")]
    [InlineData("ANJALI VIKRAM PATIL", "")]
    public void Anything_else_is_a_mismatch(string read, string holder) =>
        Assert.Equal("mismatch", UnoTP.Backend.Mock.External.MockNameMatch.Compare(read, holder));
}
