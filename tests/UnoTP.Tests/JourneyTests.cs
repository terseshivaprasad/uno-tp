using UnoTP.ViewModels;
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
    private const string NameNsdlDisagrees = "XXXXD1004D";

    [Fact]
    public async Task Upload_Documents_opens_waiting_on_the_PAN_copy()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var page = await client.GetStringAsync(App.Step(at, "documents"));

        // The proof of address waits on the PAN, and names the four proofs it takes.
        Assert.Contains("Upload the PAN copy first", page);
        Assert.Contains("Accepted: Aadhaar, Passport, Driving Licence or Voter ID.", page);
        // The communication address proof box stands while its upload is switched
        // off, not applicable, saying why - it is not left out.
        var mail = Regex.Match(page, "id=\"slot-mail\".*?doc-slot__notes", RegexOptions.Singleline).Value;
        Assert.Contains("Communication address proof", page);
        Assert.Contains("Not applicable", mail);
        // Nothing has been checked yet, so there is no list of checks.
        Assert.DoesNotContain("What the PAN was checked with", page);
        // The payment box waits on the mode - every mode's box is in the page, and
        // the one shown is for no mode chosen - and the account card stands greyed.
        var noMode = Regex.Match(page, "<fieldset class=\"doc-choice\" data-show-when=\"payMode=\">.*?</fieldset>", RegexOptions.Singleline).Value;
        Assert.Contains("Choose the payment mode first", noMode);
        Assert.DoesNotContain("settled electronically", noMode);
        Assert.Matches("<div class=\"doc-payment__card\"[^>]*aria-disabled=\"true\"", page);
        Assert.DoesNotMatch("<div class=\"doc-payment__card\"[^>]*\\shidden", page);
    }

    [Fact]
    public async Task A_PAN_copy_NSDL_verifies_is_final_and_its_checks_are_listed()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var page = await App.UploadAsync(client, App.Step(at, "documents"), "pan", "pan.jpg", ("appType", "DIGITAL"));

        Assert.Contains("What the PAN was checked with", page);
        // Razor writes the en dash as an entity.
        Assert.Matches("<li class=\"doc-check is-done\"[^>]*>\\s*<span class=\"doc-check__kind\">PAN (–|&#x2013;) NSDL", page);
        Assert.Contains("Verified with NSDL", page);
        // Verified, the copy cannot be replaced.
        Assert.Contains("doc-tool--final", page);
        var panBox = Regex.Match(page, "id=\"slot-pan\".*?doc-slot__frame", RegexOptions.Singleline).Value;
        Assert.DoesNotContain(">Replace<", panBox);
        // The proof of address is open now.
        Assert.DoesNotContain("Upload the PAN copy first", page);
    }

    [Fact]
    public async Task Fetch_from_CKYC_is_offered_before_the_PAN_copy_and_on_a_verified_one()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        // No PAN copy yet: the record can be fetched on the PAN the investor was identified with.
        var before = await client.GetStringAsync(App.Step(at, "documents"));
        Assert.DoesNotContain("disabled", Regex.Match(before, "<button[^>]*doc-ckyc-btn[^>]*>").Value);
        Assert.DoesNotContain(DocumentsViewModel.CkycWaitsOnPan, before);

        // The PAN copy filed and verified with NSDL: still offered.
        var after = await App.UploadAsync(client, App.Step(at, "documents"), "pan", "pan.jpg", ("appType", "DIGITAL"));
        Assert.Contains("Verified with NSDL", after);
        Assert.DoesNotContain("disabled", Regex.Match(after, "<button[^>]*doc-ckyc-btn[^>]*>").Value);
    }

    [Fact]
    public async Task Fetch_from_CKYC_waits_on_a_PAN_copy_NSDL_has_not_verified()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NameNsdlDisagrees);

        // A copy whose name NSDL does not match: the offer stands, disabled, saying why.
        var page = await App.UploadAsync(client, App.Step(at, "documents"), "pan", "pan.jpg", ("appType", "DIGITAL"));
        var button = Regex.Match(page, "<button[^>]*doc-ckyc-btn[^>]*>").Value;
        Assert.Contains("disabled", button);
        Assert.DoesNotContain("data-enable-when", button);
        Assert.Contains(DocumentsViewModel.CkycWaitsOnPan, page);
        // Asked for anyway, it is refused: the application stays off the CKYC route.
        await App.PostAsync(client, App.Step(at, "documents"), App.Step(at, "documents") + "/ckyc", ("appType", "DIGITAL"));
        Assert.DoesNotContain("doc-ckyc-on", await client.GetStringAsync(App.Step(at, "documents")));
    }

    [Fact]
    public async Task View_Application_details_show_the_whole_application_read_only()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var details = await client.GetStringAsync(App.Step(at, "details"));

        // Review Summary's sections, each under a heading, with nothing to edit.
        foreach (var section in new[] { "Fixed Deposit", "Holders", "Nominee", "Bank Details &amp; Payment", "Documents", "Other Details" })
            Assert.Contains($"<h3 class=\"view-app-sheet__head\">{section}</h3>", details);
        Assert.DoesNotContain("review-edit", details);
        Assert.DoesNotContain("<html", details);
        // Only the partner's own applications are found.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/ViewApplication/FBBMFL26F99999/details")).StatusCode);
    }

    [Fact]
    public async Task A_holder_on_a_folio_is_not_checked_again()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, OnFolioWithoutPanCopy);

        // The PAN copy is asked for, but not needed: the folio has been through KYC.
        var before = await client.GetStringAsync(App.Step(at, "documents"));
        Assert.Contains("Not mandatory: the holder is on a folio.", before);
        Assert.DoesNotContain("What the PAN was checked with", before);

        var page = await App.UploadAsync(client, App.Step(at, "documents"), "pan", "pan.jpg", ("appType", "DIGITAL"));

        Assert.DoesNotContain("What the PAN was checked with", page);
    }

    [Fact]
    public async Task A_holder_on_a_folio_may_file_a_newer_proof_of_address_over_the_folios()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, OnFolioComplete);

        // The folio's address stands in the box, not needed again, with the tool to file a newer proof.
        var page = await client.GetStringAsync(App.Step(at, "documents"));
        var box = Regex.Match(page, "id=\"slot-poa\".*?doc-slot__notes", RegexOptions.Singleline).Value;
        Assert.Contains("Newer proof", box);
        Assert.Contains("Shantiniketan", box);
        Assert.Contains("file a newer proof only if the address has changed", box);
        Assert.DoesNotContain("the proof of address", Regex.Match(page, "page-action-bar__hint.*?</(p|details)>", RegexOptions.Singleline).Value);

        // Filed, the newer proof is what the box holds, and Replace stands over it.
        var after = await App.UploadAsync(client, App.Step(at, "documents"), "poa", "voter_id.jpg", ("appType", "DIGITAL"));
        var filed = Regex.Match(after, "id=\"slot-poa\".*?doc-slot__notes", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("Newer proof", filed);
        Assert.Contains(">Replace<", filed);
    }

    [Fact]
    public async Task An_Aadhaar_proof_of_address_is_masked_before_it_is_filed()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        await App.UploadAsync(client, App.Step(at, "documents"), "pan", "pan.jpg", ("appType", "DIGITAL"));

        var page = await App.UploadAsync(client, App.Step(at, "documents"), "poa", "aadhaar.jpg", ("appType", "DIGITAL"));
        // The history is drawn below the boxes; the box itself holds the filed copy.
        Assert.Contains("Identified as a proof of address: Aadhaar.", page);
        Assert.Contains("Aadhaar number masked before the copy is filed.", page);
        Assert.Contains(">Replace<", Regex.Match(page, "id=\"slot-poa\".*?doc-slot__notes", RegexOptions.Singleline).Value);
    }

    [Fact]
    public async Task Proceed_waits_on_the_PAN_Aadhaar_link_and_the_PAN_POA_name_match()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var documents = App.Step(at, "documents");
        await App.UploadAsync(client, documents, "pan", "pan.jpg", ("appType", "DIGITAL"));

        // An Aadhaar whose number OCR could not read whole: the link waits on the number, and so does Proceed.
        await App.UploadAsync(client, documents, "poa", "aadhaar_masked.jpg", ("appType", "DIGITAL"));
        var stopped = await App.PostAsync(client, documents, documents + "/proceed", ("appType", "DIGITAL"));
        Assert.StartsWith("/UploadInvestorDocuments/", stopped.RequestMessage!.RequestUri!.AbsolutePath);
        var page = await stopped.Content.ReadAsStringAsync();
        Assert.Contains("Type the Aadhaar number in the row under the proofs of address, so the PAN-Aadhaar link can be asked.", page);

        // An Aadhaar read whole: linked, name and date of birth matched, so neither check stops Proceed.
        await App.UploadAsync(client, documents, "poa", "aadhaar.jpg", ("appType", "DIGITAL"));
        var again = await App.PostAsync(client, documents, documents + "/proceed", ("appType", "DIGITAL"));
        var after = await again.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PAN-Aadhaar link can be asked", after);
        Assert.DoesNotContain("PAN-Aadhaar link must be confirmed", after);
        Assert.DoesNotContain("must match the PAN's before proceeding", after);
        Assert.Contains("Linked with Aadhaar", after);
    }

    [Fact]
    public async Task A_different_communication_address_is_typed_on_Investor_Information()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        await App.PostAsync(client, App.Step(at, "documents"), App.Step(at, "documents") + "/refresh", ("appType", "DIGITAL"), ("mailing", "different"));

        var page = await client.GetStringAsync(App.Step(at, "investor"));
        Assert.Contains("Communication address &middot; different from permanent", page);
        foreach (var field in new[] { "Line1", "Line2", "Line3", "City", "PinCode" })
            Assert.Contains($"name=\"Holder1.Comm.{field}\"", page);
        // No suggestions while the PIN code is typed; it is placed once six digits are in.
        Assert.Matches("id=\"h1-commpin\"[^>]*autocomplete=\"off\"", page);
        Assert.Matches("id=\"h1-commpin\"[^>]*data-pin-url=", page);

        // Proceed will not go on without the first line, the city and a 6-digit PIN code.
        var refused = await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor"),
            ("Holder1.Comm.Line1", ""), ("Holder1.Comm.City", ""), ("Holder1.Comm.PinCode", "4000"));
        var errors = await refused.Content.ReadAsStringAsync();
        Assert.Contains("Enter the first line of the address", errors);
        Assert.Contains("Enter the city", errors);
        Assert.Contains("Enter a 6-digit PIN code", errors);

        // Typed, it is saved with the holder's details, placed by its PIN code.
        await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor"),
            ("Holder1.Comm.Line1", "Flat 4, Sea View"), ("Holder1.Comm.City", "Mumbai"), ("Holder1.Comm.PinCode", "400050"));
        var review = await client.GetStringAsync(App.Step(at, "review"));
        Assert.Contains("Flat 4, Sea View, Mumbai, Maharashtra - 400050", review);
    }

    [Fact]
    public async Task Proceed_with_no_nominee_named_asks_first_until_one_is_added_or_skipped()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var page = await client.GetStringAsync(App.Step(at, "investor"));
        Assert.Contains("data-ask-nominee=\"yes\"", page);
        Assert.Contains("id=\"investorNomineeAsk\"", page);
        Assert.Contains("We strongly advise adding a nominee.", page);

        // Skipped, the question is kept answered with the form.
        await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor") + "/save", ("NomineeSkipped", "yes"));
        var skipped = await client.GetStringAsync(App.Step(at, "investor"));
        Assert.DoesNotContain("data-ask-nominee=\"yes\"", skipped);

        // Added, there is nothing to ask.
        await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor") + "/nominee/add");
        var added = await client.GetStringAsync(App.Step(at, "investor"));
        Assert.DoesNotContain("id=\"investorNomineeAsk\"", added);
        Assert.Contains("id=\"nominee\"", added);
    }

    [Fact]
    public async Task A_matured_deposit_is_renewed_through_the_same_steps_with_the_deposits_own_details_filled_in()
    {
        var client = await app.SignedInAsync();

        // Searched by PAN and date of birth: the investor's deposits, each with where it
        // stands and a remark; only the ones due - inside the window - can be renewed.
        var page = await client.GetStringAsync("/RenewalDashboard?by=pan&pan=XXXXA1001A&dd=14&mm=08&yyyy=1988");
        Assert.Contains("4 deposits against PAN XXXXA1001A", page);
        foreach (var status in new[] { "Due for renewal", "Running", "Entry closed" }) Assert.Contains($">{status}<", page);
        Assert.Equal(2, Regex.Matches(page, ">Renew</button>").Count);
        // The row's form posts to the deposit's own Start, with the token: a form that posts nowhere looks like a dead button.
        Assert.Matches("<form method=\"post\" action=\"/RenewalDashboard/FD2023001234/start[?][^\"]*\"[^>]*>\\s*<button", page);
        Assert.Contains("__RequestVerificationToken", Regex.Match(page, "<form method=\"post\" action=\"/RenewalDashboard/FD2023001234/start.*?</form>", RegexOptions.Singleline).Value);
        Assert.Contains("Renewal entry opens 61 days before maturity", page);
        Assert.Contains("Renewal entry closed 7 days before maturity", page);
        Assert.Contains("tagged for auto renewal", page);
        Assert.Contains("1 joint holder", page);
        Assert.Contains("Deposits due for renewal only will be displayed in this module.", page);

        // Renew opens an application, at Upload Documents: no payment for a renewal.
        var started = await App.PostAsync(client, "/RenewalDashboard?by=pan&pan=XXXXA1001A&dd=14&mm=08&yyyy=1988", "/RenewalDashboard/FD2023001234/start", ("by", "pan"), ("pan", "XXXXA1001A"), ("dd", "14"), ("mm", "08"), ("yyyy", "1988"));
        var at = started.RequestMessage!.RequestUri!.AbsolutePath;
        Assert.StartsWith("/UploadInvestorDocuments/", at);
        var appAt = at["/UploadInvestorDocuments/".Length..];
        var documents = await started.Content.ReadAsStringAsync();
        Assert.Contains("FDR FD2023001234", documents);
        Assert.Contains("The maturing deposit FD2023001234 pays for the new one", documents);
        Assert.Contains(">Not applicable</option>", documents);
        Assert.DoesNotContain("the payment mode", Regex.Match(documents, "page-action-bar__hint.*?</(p|details)>", RegexOptions.Singleline).Value);

        // The deposit's joint holder is on Investor Information already, added.
        var investor = await client.GetStringAsync(App.Step(appAt, "investor"));
        Assert.Contains("MEERA ANIL JOSHI", investor);
        Assert.Contains("Second Holder Details", investor);

        // Bank Details: only the repayment account, filled from the deposit's.
        var payment = await client.GetStringAsync(App.Step(appAt, "payment"));
        Assert.Contains("HDFC0000521", payment);
        Assert.Contains("pays for the new one", payment);
        Assert.DoesNotContain("Payment Bank Details", payment);

        // FD Configuration: the maturity amount, read-only, quoted.
        var deposit = await client.GetStringAsync(App.Step(appAt, "deposit"));
        Assert.Matches("id=\"deposit-amount\"[^>]*readonly", deposit);
        Assert.Contains("the maturity amount of deposit FD2023001234", deposit);
        Assert.Contains("deposit-key-value__value--rate", deposit);

        // Review Summary names the deposit renewed; the list, by folio this time, shows it renewed.
        var review = await client.GetStringAsync(App.Step(appAt, "review"));
        Assert.Contains("Deposit FD2023001234", review);
        Assert.Contains(">Renewed<", await client.GetStringAsync("/RenewalDashboard?by=folio&folio=TS003027"));

        // A deposit still running is turned back, with why.
        var refused = await App.PostAsync(client, "/RenewalDashboard?by=folio&folio=TS003027", "/RenewalDashboard/FD2025000912/start", ("by", "folio"), ("folio", "TS003027"));
        Assert.EndsWith("/RenewalDashboard", refused.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("cannot be renewed now", await refused.Content.ReadAsStringAsync());

        // Nothing on record says so; a folio with no deposits too; a PAN mistyped is not searched.
        Assert.Contains("No deposit on record against folio NOPE0001", await client.GetStringAsync("/RenewalDashboard?by=folio&folio=NOPE0001"));
        Assert.Contains("Nothing due for renewal, or held, against folio MF0051187", await client.GetStringAsync("/RenewalDashboard?by=folio&folio=MF0051187"));
        Assert.Contains("Enter a valid PAN, like ABCDE1234F", await client.GetStringAsync("/RenewalDashboard?by=pan&pan=XXXX&dd=14&mm=08&yyyy=1988"));
    }

    [Fact]
    public async Task FD_Configuration_quotes_the_card_rate_before_an_amount_and_shuts_a_payout_under_its_minimum()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var deposit = App.Step(at, "deposit");

        // Before an amount is typed the rate is quoted at the standing ₹50,000, where every payout is open.
        var opened = await client.GetStringAsync(deposit);
        Assert.Contains("6.60%", opened);
        Assert.Contains("of ₹ 50,000 as on", System.Net.WebUtility.HtmlDecode(opened));
        Assert.DoesNotContain("deposit-option--off", opened);

        // At ₹10,000 a monthly payout is under the chart's ₹50,000 minimum: shut.
        var quoted = await App.PostAsync(client, deposit, deposit + "/quote",
            ("Amount", "10,000"), ("TenureMonths", "12"), ("InterestPayout", "monthly"), ("DeliveryType", "ereceipt"));
        var panel = System.Net.WebUtility.HtmlDecode(await quoted.Content.ReadAsStringAsync());
        Assert.Contains("data-offer=\"InterestPayout\" data-value=\"monthly\" data-offered=\"no\"", panel);
        Assert.Contains("data-offer=\"InterestPayout\" data-value=\"maturity\" data-offered=\"yes\"", panel);
        Assert.Contains("A monthly payout is not offered for this amount", panel);

        // Proceed with that choice is refused for the same reason.
        var refused = await App.PostAsync(client, deposit, deposit,
            ("Amount", "10,000"), ("TenureMonths", "12"), ("InterestPayout", "monthly"), ("DeliveryType", "ereceipt"));
        Assert.StartsWith("/FDConfiguration/", refused.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("A monthly payout is not offered for this amount", System.Net.WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task The_source_of_funds_is_asked_of_a_homemaker_whose_deposits_pass_a_crore()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var investor = App.Step(at, "investor");
        var deposit = App.Step(at, "deposit");
        await App.PostAsync(client, investor, investor, ("Holder1.Occupation", "Homemaker"), ("Holder1.AnnualIncome", "Rs.10,00,000 - Rs.25,00,000"));

        // A new investor holds nothing with us: at ₹50 lakh the field is shut, with the rule under it.
        var opened = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync(deposit));
        Assert.Contains("id=\"deposit-source-of-funds\" name=\"SourceOfFunds\" disabled", opened);
        Assert.Contains("Asked when the investor's deposits with us, with this one, pass ₹ 1,00,00,000 (they hold ₹ 0 now)", opened);

        // At ₹1.5 crore it is asked: the quote says so, and Proceed wants it.
        (string, string)[] chosen = [("Amount", "1,50,00,000"), ("TenureMonths", "12"), ("InterestPayout", "maturity"), ("DeliveryType", "ereceipt")];
        var quoted = await App.PostAsync(client, deposit, deposit + "/quote", chosen);
        var panel = System.Net.WebUtility.HtmlDecode(await quoted.Content.ReadAsStringAsync());
        Assert.Contains("data-source-of-funds data-asked=\"yes\"", panel);
        Assert.Contains("their occupation is homemaker", panel);

        var refused = await App.PostAsync(client, deposit, deposit, chosen);
        Assert.StartsWith("/FDConfiguration/", refused.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("Required — choose the source of funds", System.Net.WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));

        // "Other" wants a remark; a source named goes on to Review Summary, which shows it.
        var other = await App.PostAsync(client, deposit, deposit, [.. chosen, ("SourceOfFunds", "other")]);
        Assert.Contains("Required — say what the source of funds is", System.Net.WebUtility.HtmlDecode(await other.Content.ReadAsStringAsync()));

        var proceeded = await App.PostAsync(client, deposit, deposit, [.. chosen, ("SourceOfFunds", "other"), ("SourceOfFundsRemark", "Sale of a car")]);
        Assert.Contains("/ReviewSummary/", proceeded.RequestMessage!.RequestUri!.AbsolutePath);
        var review = await proceeded.Content.ReadAsStringAsync();
        Assert.Contains("Source of funds", review);
        Assert.Contains("Sale of a car", review);
    }

    // Everything Investor Information asks of an investor with no folio, apart from the gender.
    private static readonly (string, string)[] InvestorCard =
    [
        ("Holder1.NameType", "Father"), ("Holder1.ParentName", "SUBHASH TERSE"), ("Holder1.AnnualIncome", "Upto Rs.5,00,000"),
        ("Holder1.Occupation", "Salaried"), ("Holder1.SubOccupation", "Private sector"), ("Holder1.MaritalStatus", "Married"),
        ("Holder1.Mobile", "9876543210"), ("Holder1.Email", "neha@example.com"), ("Holder1.Pep", "no"), ("Holder1.PepRelated", "no"),
        ("NomineeSkipped", "yes"),
    ];

    [Fact]
    public async Task A_female_applicant_under_a_non_women_category_moves_to_the_womens_one_and_the_page_says_so()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var documents = App.Step(at, "documents");
        var investor = App.Step(at, "investor");

        // A new investor: nothing read a gender, and the sourcing agency chose Public / General.
        await App.PostAsync(client, documents, documents + "/save", ("Sourcing", "1"), ("Category", "PUBLIC/GENERAL"));
        Assert.Contains("name=\"Holder1.Gender\"", await client.GetStringAsync(investor));

        var answered = await App.PostAsync(client, investor, investor, [.. InvestorCard, ("Holder1.Gender", "Female")]);
        Assert.StartsWith("/InvestorInformation/", answered.RequestMessage!.RequestUri!.AbsolutePath);
        var page = System.Net.WebUtility.HtmlDecode(await answered.Content.ReadAsStringAsync());
        Assert.Contains("We notice a female applicant is selected under Public / General, a non-women category. The category has been updated to Women to ensure they receive the applicable women's category benefits.", page);
        Assert.Contains(">Women<", await client.GetStringAsync(App.Step(at, "deposit")));

        // Proceed again goes on, and the category stays a women's one.
        var proceeded = await App.PostAsync(client, investor, investor, [.. InvestorCard, ("Holder1.Gender", "Female")]);
        Assert.Contains("/BankDetails/", proceeded.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains(">Women<", await client.GetStringAsync(App.Step(at, "deposit")));
    }

    [Fact]
    public async Task A_male_applicant_under_a_womens_category_is_stopped_until_Upload_Documents_is_corrected()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var documents = App.Step(at, "documents");
        var investor = App.Step(at, "investor");
        await App.PostAsync(client, documents, documents + "/save", ("Sourcing", "1"), ("Category", "WOMEN"));

        var stopped = await App.PostAsync(client, investor, investor, [.. InvestorCard, ("Holder1.Gender", "Male")]);
        Assert.StartsWith("/InvestorInformation/", stopped.RequestMessage!.RequestUri!.AbsolutePath);
        var page = System.Net.WebUtility.HtmlDecode(await stopped.Content.ReadAsStringAsync());
        Assert.Contains("The category on Upload Documents is Women, a women's category, but the applicant's gender is male. Correct the category on Upload Documents before proceeding.", page);
        Assert.Contains($"href=\"/UploadInvestorDocuments/{at}\"", page);

        // Corrected there, Proceed goes on.
        await App.PostAsync(client, documents, documents + "/save", ("Sourcing", "1"), ("Category", "PUBLIC/GENERAL"));
        var proceeded = await App.PostAsync(client, investor, investor, [.. InvestorCard, ("Holder1.Gender", "Male")]);
        Assert.Contains("/BankDetails/", proceeded.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_holder_name_screening_does_not_allow_is_stopped_on_Investor_Information_and_sent_to_a_branch()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, UnoTP.Backend.Mock.MockNameScreening.NotAllowedPan);
        var investor = App.Step(at, "investor");

        var stopped = await App.PostAsync(client, investor, investor, [.. InvestorCard, ("Holder1.Gender", "Male")]);
        Assert.StartsWith("/InvestorInformation/", stopped.RequestMessage!.RequestUri!.AbsolutePath);
        var page = await stopped.Content.ReadAsStringAsync();
        Assert.Contains("Not allowed to invest online", page);
        Assert.Contains("kindly reach out to the nearest Mahindra Finance branch", page);
    }

    [Fact]
    public async Task No_TDS_needs_Form_121_filed_on_FD_Configuration_before_Proceed()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        var deposit = App.Step(at, "deposit");
        (string, string)[] chosen = [("Amount", "1,00,000"), ("TenureMonths", "12"), ("InterestPayout", "maturity"), ("NoTds", "true"), ("DeliveryType", "ereceipt")];

        // The box stands under the switch; with the switch on and nothing filed, Proceed stops.
        Assert.Contains("id=\"slot-tdsform\"", await client.GetStringAsync(deposit));
        var refused = await App.PostAsync(client, deposit, deposit, chosen);
        Assert.StartsWith("/FDConfiguration/", refused.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("Upload the Form 121 before proceeding", await refused.Content.ReadAsStringAsync());

        // Filed from the page, the deposit is saved with the switch on, and Proceed goes on.
        var filed = await App.UploadAsync(client, deposit, "tdsform", "form-121.jpg", chosen);
        Assert.Contains(">Replace<", Regex.Match(filed, "id=\"slot-tdsform\".*?doc-slot__notes", RegexOptions.Singleline).Value);
        var proceeded = await App.PostAsync(client, deposit, deposit, chosen);
        Assert.Contains("/ReviewSummary/", proceeded.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("Form 121", await proceeded.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_broker_code_is_searched_on_the_register_as_it_is_typed()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var found = await client.GetStringAsync(App.Step(at, "documents") + "/sourcing?register=brokers&q=dec");
        var byCode = await client.GetStringAsync(App.Step(at, "documents") + "/sourcing?register=brokers&q=BR11");
        var tooShort = await client.GetStringAsync(App.Step(at, "documents") + "/sourcing?register=brokers&q=d");
        var noRegister = await client.GetAsync(App.Step(at, "documents") + "/sourcing?register=nope&q=dec");

        Assert.Contains("Deccan Wealth Advisors", found);
        Assert.Contains("\"code\":\"BR10874\"", found);
        Assert.Contains("BR11250", byCode);
        Assert.Contains("BR11903", byCode);
        Assert.Equal("[]", tooShort);
        Assert.Equal(HttpStatusCode.NotFound, noRegister.StatusCode);

        // The field on the page is wired to it.
        var page = await client.GetStringAsync(App.Step(at, "documents"));
        Assert.Contains("data-register-search=", page);
        Assert.DoesNotContain("<datalist", page);
    }

    [Fact]
    public async Task The_bell_says_why_Application_Status_is_off()
    {
        var client = await app.SignedInAsync();

        var page = await client.GetStringAsync("/Dashboard");

        Assert.Contains("Application Status is being rebuilt", page);
        Assert.Contains("class=\"notices__count\">1<", page);
    }

    [Fact]
    public async Task A_PIN_code_is_placed_by_the_backend()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);

        var found = await client.GetAsync(App.Step(at, "investor") + "/pincode/400001");
        var none = await client.GetAsync(App.Step(at, "investor") + "/pincode/999999");
        var short_ = await client.GetAsync(App.Step(at, "investor") + "/pincode/4000");

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

        var investor = await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor") + "/save");
        var payment = await App.PostAsync(client, App.Step(at, "payment"), App.Step(at, "payment"), ("draft", "1"));
        var deposit = await App.PostAsync(client, App.Step(at, "deposit"), App.Step(at, "deposit"), ("draft", "1"));

        Assert.Contains("/InvestorInformation/", investor.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("/BankDetails/", payment.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Contains("/FDConfiguration/", deposit.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_joint_holder_folds_to_a_line_once_their_card_is_complete()
    {
        var client = await app.SignedInAsync();
        var at = await App.NewApplicationAsync(client, NewInvestor);
        await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor") + "/joint/add");
        await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor") + "/joint/2/check",
            ("Joint2.Pan", OnFolioComplete), ("Joint2.Dd", "14"), ("Joint2.Mm", "08"), ("Joint2.Yyyy", "1988"));

        // Just added, with fields to fill: open, with the line saying so.
        var opened = await client.GetStringAsync(App.Step(at, "investor"));
        Assert.Matches("<details class=\"investor-folded-holder\" open", opened);
        Assert.Contains("fields to fill", opened);

        // Every field filled: folded to its line - while the investor's own card,
        // which Proceed stopped at, stays open.
        await App.PostAsync(client, App.Step(at, "investor"), App.Step(at, "investor"),
            ("Holder2.NameType", "Father"), ("Holder2.ParentName", "SUBHASH TERSE"), ("Holder2.AnnualIncome", "5-10 lakh"),
            ("Holder2.Occupation", "Service"), ("Holder2.SubOccupation", "Private"), ("Holder2.MaritalStatus", "Married"),
            ("Holder2.Mobile", "9876543210"), ("Holder2.Email", "rahul@example.com"));
        var page = await client.GetStringAsync(App.Step(at, "investor"));
        Assert.Matches("<details class=\"investor-folded-holder\">", page);
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
            var answer = await client.GetAsync(App.Step(at, step));
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            Assert.Equal(App.Step(at, step), answer.RequestMessage!.RequestUri!.AbsolutePath);
        }
        var submitted = await client.GetAsync(App.Step(at, "submitted"));
        Assert.Contains("/ReviewSummary/", submitted.RequestMessage!.RequestUri!.AbsolutePath);
    }
}
