using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace UnoTP.Tests;

/// <summary>An outside service switched off is not called, and the page goes on with the check marked as not asked.</summary>
[Collection("app")]
public class SwitchesTests(App app)
{
    // The same app with one service switched off, signed in.
    private static async Task<HttpClient> SignedInWithOffAsync(App app, string service)
    {
        var factory = app.WithWebHostBuilder(b => b.UseSetting($"Backend:Switches:{service}", "false"));
        var client = factory.CreateClient();
        await client.GetAsync("/");
        return client;
    }

    [Fact]
    public async Task Name_screening_off_lets_a_holder_it_would_have_stopped_go_on()
    {
        var client = await SignedInWithOffAsync(app, "NameScreening");
        var at = await App.NewApplicationAsync(client, UnoTP.Backend.Mock.MockNameScreening.NotAllowedPan);
        var investor = App.Step(at, "investor");
        var fields = new (string, string)[]
        {
            ("Holder1.NameType", "Father"), ("Holder1.ParentName", "SUBHASH TERSE"), ("Holder1.AnnualIncome", "Upto Rs.5,00,000"),
            ("Holder1.Occupation", "Salaried"), ("Holder1.SubOccupation", "Private sector"), ("Holder1.MaritalStatus", "Married"),
            ("Holder1.Mobile", "9876543210"), ("Holder1.Email", "neha@example.com"), ("Holder1.Pep", "no"), ("Holder1.PepRelated", "no"),
            ("NomineeSkipped", "yes"), ("Holder1.Gender", "Male"),
        };
        var proceeded = await App.PostAsync(client, investor, investor, fields);
        Assert.Contains("/BankDetails/", proceeded.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OCR_off_files_an_Aadhaar_with_the_name_match_and_the_link_not_asked_and_Proceed_is_not_stopped_by_them()
    {
        var client = await SignedInWithOffAsync(app, "Ocr");
        var at = await App.NewApplicationAsync(client, "XXXXC1003C");
        var documents = App.Step(at, "documents");
        await App.UploadAsync(client, documents, "pan", "pan.jpg", ("appType", "DIGITAL"));
        // With nothing read off the PAN copy, the name printed on it is typed for NSDL, as for a misread one.
        await App.PostAsync(client, documents, documents + "/nsdl", ("appType", "DIGITAL"), ("nsdlName", "ANJALI VIKRAM PATIL"));
        var page = await App.UploadAsync(client, documents, "poa", "aadhaar.jpg", ("appType", "DIGITAL"));
        Assert.Contains("OCR is switched off", page);
        Assert.Contains("not compared: OCR is switched off", page);

        var proceed = await App.PostAsync(client, documents, documents + "/proceed", ("appType", "DIGITAL"));
        var after = await proceed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("must match the PAN's before proceeding", after);
    }
}
