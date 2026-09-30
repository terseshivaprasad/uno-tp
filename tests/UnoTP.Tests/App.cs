using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnoTP.Tests;

/// <summary>
/// The app as it runs, in a test host on the mock backend, shared by the tests
/// that drive its pages. A client signs in as the demo user by opening "/", as the
/// demo does; every post carries the page's antiforgery token, as the page's own
/// forms do.
/// </summary>
public sealed class App : WebApplicationFactory<Program>
{
    /// <summary>A browser signed in as the demo user, following redirects and keeping cookies.</summary>
    public async Task<HttpClient> SignedInAsync()
    {
        var client = CreateClient();
        var landed = await client.GetAsync("/");
        Assert.Equal("/Dashboard", landed.RequestMessage!.RequestUri!.AbsolutePath);
        return client;
    }

    /// <summary>The antiforgery token a page's forms carry.</summary>
    public static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]*)\"").Groups[1].Value;

    /// <summary>Opens a page and posts a form on it, as a browser would.</summary>
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string page, string action, params (string Name, string Value)[] fields)
    {
        var html = await client.GetStringAsync(page);
        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", Token(html)) };
        form.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));
        return await client.PostAsync(action, new FormUrlEncodedContent(form));
    }

    /// <summary>Uploads a document into a box, as its form does; answers with the page after it.</summary>
    public static async Task<string> UploadAsync(HttpClient client, string page, string slot, string fileName, params (string Name, string Value)[] fields)
    {
        var html = await client.GetStringAsync(page);
        var form = new MultipartFormDataContent { { new StringContent(Token(html)), "__RequestVerificationToken" }, { new StringContent(slot), "slot" } };
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        var file = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine("Files", "pan.jpg")));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file_" + slot, fileName);
        var answer = await client.PostAsync(page + "/upload", form);
        return await answer.Content.ReadAsStringAsync();
    }

    /// <summary>The address of one of an application's steps: /UploadInvestorDocuments/{appNo} and so on.</summary>
    public static string Step(string appNo, string step)
    {
        var pages = new Dictionary<string, string>
        {
            ["documents"] = "UploadInvestorDocuments", ["investor"] = "InvestorInformation", ["payment"] = "BankDetails",
            ["deposit"] = "FDConfiguration", ["review"] = "ReviewSummary", ["submitted"] = "ApplicationSubmitted",
        };
        if (step == "details") return $"/ViewApplication/{appNo}/Details";
        return $"/{pages[step]}/{appNo}";
    }

    /// <summary>
    /// Opens a new application for a PAN the mock register knows, through Investor
    /// Identification, and answers with the application's number.
    /// </summary>
    public static async Task<string> NewApplicationAsync(HttpClient client, string pan)
    {
        await PostAsync(client, "/SearchInvestor", "/SearchInvestor/check", ("By", "pan"), ("Pan", pan), ("Dd", "14"), ("Mm", "08"), ("Yyyy", "1988"));
        var opened = await PostAsync(client, "/SearchInvestor", "/SearchInvestor/proceed");
        var at = opened.RequestMessage!.RequestUri!.AbsolutePath;
        Assert.StartsWith("/UploadInvestorDocuments/", at);
        return at["/UploadInvestorDocuments/".Length..];
    }
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<App>;
