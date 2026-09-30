using System.Net;
using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>Input no page would send is refused before a page sees it; what the fields take is one rule everywhere.</summary>
[Collection("app")]
public class InputScreeningTests(App app)
{
    [Fact]
    public async Task A_query_parameter_given_twice_is_refused()
    {
        var client = await app.SignedInAsync();
        var answer = await client.GetAsync("/ViewApplication?q=a&q=b");
        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
    }

    [Fact]
    public async Task A_form_value_with_a_character_no_field_takes_is_refused()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, "XXXXC1003C");
        var investor = App.Step(at, "investor");
        var answer = await App.PostAsync(client, investor, investor + "/save", ("Holder1.ParentName", "<script>alert(1)</script>"));
        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
    }

    [Fact]
    public async Task A_checkbox_and_its_hidden_false_may_post_together()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, "XXXXC1003C");
        var deposit = App.Step(at, "deposit");
        var answer = await App.PostAsync(client, deposit, deposit + "/quote", ("Amount", "10,000"), ("TenureMonths", "12"), ("InterestPayout", "maturity"), ("NoTds", "true"), ("NoTds", "false"));
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
    }

    [Theory]
    [InlineData("Flat 4, Sea View - Baner Rd. 2/3 & co", true)]
    [InlineData("Flat 4 (Sea View)", false)]
    [InlineData("O'Brien", false)]
    [InlineData("a<b", false)]
    public void Text_takes_letters_digits_spaces_and_the_allowed_punctuation(string text, bool clean)
    {
        Assert.Equal(clean, InputRules.IsClean(text));
    }

    [Theory]
    [InlineData("ANJALI VIKRAM PATIL", true)]
    [InlineData("R K SHARMA", true)]
    [InlineData("R. K. Sharma", false)]
    [InlineData("Sharma2", false)]
    [InlineData("", false)]
    public void A_name_is_letters_and_spaces_only(string name, bool ok)
    {
        Assert.Equal(ok, InputRules.IsName(name));
    }
}
