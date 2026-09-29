using System.Net;
using System.Text.RegularExpressions;

namespace UnoTP.Tests;

/// <summary>
/// A new application driven through its pages on the mock backend, checking the
/// rules the pages keep. The PANs are the mock register's test records: XXXXC1003C
/// a new investor NSDL verifies, XXXXH1008H a folio that holds no PAN copy.
/// </summary>
[Collection("app")]
public class JourneyTests(App app)
{
    private const string NewInvestor = "XXXXC1003C";
    private const string OnFolioWithoutPanCopy = "XXXXH1008H";

    [Fact]
    public async Task Upload_Documents_opens_waiting_on_the_PAN_copy()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var page = await client.GetStringAsync(at + "/documents");

        // The proof of address waits on the PAN, and names the four proofs it takes.
        Assert.Contains("Upload the PAN copy first", page);
        Assert.Contains("Accepted: Aadhaar, Passport, Driving Licence or Voter ID.", page);
        // No communication address proof box while its upload is switched off.
        Assert.DoesNotContain("Communication address proof", page);
        // Nothing has been checked yet, so there is no list of checks.
        Assert.DoesNotContain("What the PAN was checked with", page);
        // The payment box waits on the mode - every mode's box is in the page, and
        // the one shown is for no mode chosen - and the account card is not shown.
        var noMode = Regex.Match(page, "<fieldset class=\"cud-choice\" data-show-when=\"payMode=\">.*?</fieldset>", RegexOptions.Singleline).Value;
        Assert.Contains("Choose the payment mode first", noMode);
        Assert.DoesNotContain("settled electronically", noMode);
        Assert.Matches("<div class=\"cud-pay__card\"[^>]*\\shidden", page);
    }

    [Fact]
    public async Task A_PAN_copy_NSDL_verifies_is_final_and_its_checks_are_listed()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var page = await App.UploadAsync(client, at + "/documents", "pan", "pan.jpg", ("appType", "DIGITAL"));

        Assert.Contains("What the PAN was checked with", page);
        // Razor writes the en dash as an entity.
        Assert.Matches("<li class=\"cud-check is-done\"[^>]*>\\s*<span class=\"cud-check__kind\">PAN (–|&#x2013;) NSDL", page);
        Assert.Contains("Verified with NSDL", page);
        // Verified, the copy cannot be replaced.
        Assert.Contains("cud-tool--final", page);
        var panBox = Regex.Match(page, "id=\"slot-pan\".*?cud-slot__frame", RegexOptions.Singleline).Value;
        Assert.DoesNotContain(">Replace<", panBox);
        // The proof of address is open now.
        Assert.DoesNotContain("Upload the PAN copy first", page);
    }

    [Fact]
    public async Task A_holder_on_a_folio_is_not_checked_again()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, OnFolioWithoutPanCopy);

        // The PAN copy is asked for, but not needed: the folio has been through KYC.
        var before = await client.GetStringAsync(at + "/documents");
        Assert.Contains("Not mandatory: the holder is on a folio.", before);
        Assert.DoesNotContain("What the PAN was checked with", before);

        var page = await App.UploadAsync(client, at + "/documents", "pan", "pan.jpg", ("appType", "DIGITAL"));

        Assert.DoesNotContain("What the PAN was checked with", page);
    }

    [Fact]
    public async Task A_different_communication_address_is_typed_on_Investor_Information()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        await App.PostAsync(client, at + "/documents", at + "/documents/refresh", ("appType", "DIGITAL"), ("mailing", "different"));

        var page = await client.GetStringAsync(at + "/investor");
        Assert.Contains("Communication address &middot; different from permanent", page);
        foreach (var field in new[] { "Line1", "Line2", "Line3", "City", "PinCode" })
            Assert.Contains($"name=\"Holder1.Comm.{field}\"", page);
        // No suggestions while the PIN code is typed; it is placed once six digits are in.
        Assert.Matches("id=\"h1-commpin\"[^>]*autocomplete=\"off\"", page);
        Assert.Matches("id=\"h1-commpin\"[^>]*data-pin-url=", page);

        // Proceed will not go on without the first line, the city and a 6-digit PIN code.
        var refused = await App.PostAsync(client, at + "/investor", at + "/investor",
            ("Holder1.Comm.Line1", ""), ("Holder1.Comm.City", ""), ("Holder1.Comm.PinCode", "4000"));
        var errors = await refused.Content.ReadAsStringAsync();
        Assert.Contains("Enter the first line of the address", errors);
        Assert.Contains("Enter the city", errors);
        Assert.Contains("Enter a 6-digit PIN code", errors);

        // Typed, it is saved with the holder's details, placed by its PIN code.
        await App.PostAsync(client, at + "/investor", at + "/investor",
            ("Holder1.Comm.Line1", "Flat 4, Sea View"), ("Holder1.Comm.City", "Mumbai"), ("Holder1.Comm.PinCode", "400050"));
        var review = await client.GetStringAsync(at + "/review");
        Assert.Contains("Flat 4, Sea View, Mumbai, Maharashtra - 400050", review);
    }

    [Fact]
    public async Task A_PIN_code_is_placed_by_the_backend()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var found = await client.GetAsync(at + "/investor/pincode/400001");
        var none = await client.GetAsync(at + "/investor/pincode/999999");
        var short_ = await client.GetAsync(at + "/investor/pincode/4000");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Contains("\"district\":\"Mumbai\"", await found.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, short_.StatusCode);
    }

    [Fact]
    public async Task Every_step_of_an_application_opens_and_Submitted_waits_on_the_review()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        foreach (var step in new[] { "documents", "investor", "payment", "deposit", "review" })
        {
            var answer = await client.GetAsync($"{at}/{step}");
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            Assert.EndsWith("/" + step, answer.RequestMessage!.RequestUri!.AbsolutePath);
        }
        var submitted = await client.GetAsync(at + "/submitted");
        Assert.EndsWith("/review", submitted.RequestMessage!.RequestUri!.AbsolutePath);
    }
}
