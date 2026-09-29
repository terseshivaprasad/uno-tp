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
    private const string OnFolioComplete = "XXXXA1001A";

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
    public async Task A_holder_on_a_folio_may_file_a_newer_proof_of_address_over_the_folios()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, OnFolioComplete);

        // The folio's address stands in the box, not needed again, with the tool to file a newer proof.
        var page = await client.GetStringAsync(at + "/documents");
        var box = Regex.Match(page, "id=\"slot-poa\".*?cud-slot__notes", RegexOptions.Singleline).Value;
        Assert.Contains("Newer proof", box);
        Assert.Contains("Shantiniketan", box);
        Assert.Contains("file a newer proof only if the address has changed", box);
        Assert.DoesNotContain("the proof of address", Regex.Match(page, "csi-bar__hint.*?</(p|details)>", RegexOptions.Singleline).Value);

        // Filed, the newer proof is what the box holds, and Replace stands over it.
        var after = await App.UploadAsync(client, at + "/documents", "poa", "voter_id.jpg", ("appType", "DIGITAL"));
        var filed = Regex.Match(after, "id=\"slot-poa\".*?cud-slot__notes", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("Newer proof", filed);
        Assert.Contains(">Replace<", filed);
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
    public async Task Proceed_with_no_nominee_named_asks_first_until_one_is_added_or_skipped()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var page = await client.GetStringAsync(at + "/investor");
        Assert.Contains("data-ask-nominee=\"yes\"", page);
        Assert.Contains("id=\"ciiNomineeAsk\"", page);
        Assert.Contains("We strongly advise adding a nominee.", page);

        // Skipped, the question is kept answered with the form.
        await App.PostAsync(client, at + "/investor", at + "/investor/save", ("NomineeSkipped", "yes"));
        var skipped = await client.GetStringAsync(at + "/investor");
        Assert.DoesNotContain("data-ask-nominee=\"yes\"", skipped);

        // Added, there is nothing to ask.
        await App.PostAsync(client, at + "/investor", at + "/investor/nominee/add");
        var added = await client.GetStringAsync(at + "/investor");
        Assert.DoesNotContain("id=\"ciiNomineeAsk\"", added);
        Assert.Contains("id=\"nominee\"", added);
    }

    [Fact]
    public async Task A_matured_deposit_is_renewed_through_the_same_steps_with_the_deposits_own_details_filled_in()
    {
        var client = await app.SignedInAsync();

        // Searched by PAN and date of birth: the investor's deposits, each with where it
        // stands and a remark; only the ones due - inside the window - can be renewed.
        var page = await client.GetStringAsync("/unotp/renew?by=pan&pan=XXXXA1001A&dd=14&mm=08&yyyy=1988");
        Assert.Contains("4 deposits against PAN XXXXA1001A", page);
        foreach (var status in new[] { "Due for renewal", "Running", "Entry closed" }) Assert.Contains($">{status}<", page);
        Assert.Equal(2, Regex.Matches(page, ">Renew</button>").Count);
        Assert.Contains("Renewal entry opens 61 days before maturity", page);
        Assert.Contains("Renewal entry closed 7 days before maturity", page);
        Assert.Contains("tagged for auto renewal", page);
        Assert.Contains("1 joint holder", page);
        Assert.Contains("Deposits due for renewal only will be displayed in this module.", page);

        // Renew opens an application, at Upload Documents: no payment for a renewal.
        var started = await App.PostAsync(client, "/unotp/renew?by=pan&pan=XXXXA1001A&dd=14&mm=08&yyyy=1988", "/unotp/renew/FD2023001234/start", ("by", "pan"), ("pan", "XXXXA1001A"), ("dd", "14"), ("mm", "08"), ("yyyy", "1988"));
        var at = started.RequestMessage!.RequestUri!.AbsolutePath;
        Assert.EndsWith("/documents", at);
        var appAt = at[..^"/documents".Length];
        var documents = await started.Content.ReadAsStringAsync();
        Assert.Contains("FDR FD2023001234", documents);
        Assert.Contains("The maturing deposit FD2023001234 pays for the new one", documents);
        Assert.Contains(">Not applicable</option>", documents);
        Assert.DoesNotContain("the payment mode", Regex.Match(documents, "csi-bar__hint.*?</(p|details)>", RegexOptions.Singleline).Value);

        // The deposit's joint holder is on Investor Information already, added.
        var investor = await client.GetStringAsync(appAt + "/investor");
        Assert.Contains("MEERA ANIL JOSHI", investor);
        Assert.Contains("Second Holder Details", investor);

        // Bank Details: only the repayment account, filled from the deposit's.
        var payment = await client.GetStringAsync(appAt + "/payment");
        Assert.Contains("HDFC0000521", payment);
        Assert.Contains("pays for the new one", payment);
        Assert.DoesNotContain("Payment Bank Details", payment);

        // FD Configuration: the maturity amount, read-only, quoted.
        var deposit = await client.GetStringAsync(appAt + "/deposit");
        Assert.Matches("id=\"fd-amount\"[^>]*readonly", deposit);
        Assert.Contains("the maturity amount of deposit FD2023001234", deposit);
        Assert.Contains("fd-kv__v--rate", deposit);

        // Review Summary names the deposit renewed; the list, by folio this time, shows it renewed.
        var review = await client.GetStringAsync(appAt + "/review");
        Assert.Contains("Deposit FD2023001234", review);
        Assert.Contains(">Renewed<", await client.GetStringAsync("/unotp/renew?by=folio&folio=TS003027"));

        // A deposit still running is turned back, with why.
        var refused = await App.PostAsync(client, "/unotp/renew?by=folio&folio=TS003027", "/unotp/renew/FD2025000912/start", ("by", "folio"), ("folio", "TS003027"));
        Assert.EndsWith("/unotp/renew", refused.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("cannot be renewed now", await refused.Content.ReadAsStringAsync());

        // Nothing on record says so; a folio with no deposits too; a PAN mistyped is not searched.
        Assert.Contains("No deposit on record against folio NOPE0001", await client.GetStringAsync("/unotp/renew?by=folio&folio=NOPE0001"));
        Assert.Contains("Nothing due for renewal, or held, against folio MF0051187", await client.GetStringAsync("/unotp/renew?by=folio&folio=MF0051187"));
        Assert.Contains("Enter a valid PAN, like ABCDE1234F", await client.GetStringAsync("/unotp/renew?by=pan&pan=XXXX&dd=14&mm=08&yyyy=1988"));
    }

    [Fact]
    public async Task No_TDS_needs_the_form_filed_on_FD_Configuration_before_Proceed()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var deposit = at + "/deposit";
        (string, string)[] chosen = [("Amount", "1,00,000"), ("TenureMonths", "12"), ("InterestPayout", "maturity"), ("NoTds", "true"), ("DeliveryType", "ereceipt")];

        // The box stands under the switch; with the switch on and nothing filed, Proceed stops.
        Assert.Contains("id=\"slot-tdsform\"", await client.GetStringAsync(deposit));
        var refused = await App.PostAsync(client, deposit, deposit, chosen);
        Assert.EndsWith("/deposit", refused.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("Upload the Form 15G/15H before proceeding", await refused.Content.ReadAsStringAsync());

        // Filed from the page, the deposit is saved with the switch on, and Proceed goes on.
        var filed = await App.UploadAsync(client, deposit, "tdsform", "form-15g.jpg", chosen);
        Assert.Contains(">Replace<", Regex.Match(filed, "id=\"slot-tdsform\".*?cud-slot__notes", RegexOptions.Singleline).Value);
        var proceeded = await App.PostAsync(client, deposit, deposit, chosen);
        Assert.EndsWith("/review", proceeded.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("Form 15G/15H", await proceeded.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_bell_says_why_Application_Status_is_off()
    {
        var client = await app.SignedInAsync();

        var page = await client.GetStringAsync("/unotp");

        Assert.Contains("Application Status is being rebuilt", page);
        Assert.Contains("class=\"notices__count\">1<", page);
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
    public async Task Save_draft_keeps_the_partner_on_the_step()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var investor = await App.PostAsync(client, at + "/investor", at + "/investor/save");
        var payment = await App.PostAsync(client, at + "/payment", at + "/payment", ("draft", "1"));
        var deposit = await App.PostAsync(client, at + "/deposit", at + "/deposit", ("draft", "1"));

        Assert.EndsWith("/investor", investor.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.EndsWith("/payment", payment.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.EndsWith("/deposit", deposit.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_joint_holder_folds_to_a_line_once_their_card_is_complete()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        await App.PostAsync(client, at + "/investor", at + "/investor/joint/add");
        await App.PostAsync(client, at + "/investor", at + "/investor/joint/2/check",
            ("Joint2.Pan", OnFolioComplete), ("Joint2.Dd", "14"), ("Joint2.Mm", "08"), ("Joint2.Yyyy", "1988"));

        // Just added, with fields to fill: open, with the line saying so.
        var opened = await client.GetStringAsync(at + "/investor");
        Assert.Matches("<details class=\"cii-fold\" open", opened);
        Assert.Contains("fields to fill", opened);

        // Every field filled: folded to its line - while the investor's own card,
        // which Proceed stopped at, stays open.
        await App.PostAsync(client, at + "/investor", at + "/investor",
            ("Holder2.NameType", "Father"), ("Holder2.ParentName", "SUBHASH TERSE"), ("Holder2.AnnualIncome", "5-10 lakh"),
            ("Holder2.Occupation", "Service"), ("Holder2.SubOccupation", "Private"), ("Holder2.MaritalStatus", "Married"),
            ("Holder2.Mobile", "9876543210"), ("Holder2.Email", "rahul@example.com"));
        var page = await client.GetStringAsync(at + "/investor");
        Assert.Matches("<details class=\"cii-fold\">", page);
        Assert.Contains("SHIVAPRASAD SUBHASH TERSE", page);
        Assert.Contains("details complete", page);
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
