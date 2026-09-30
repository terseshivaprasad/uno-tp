using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnoTP.Tests;

/// <summary>
/// The way in from the portal and back to it. The mock decryption reads base64, so
/// the portal's encrypted values are base64 here: "100002225" and "UNOTP".
/// </summary>
[Collection("app")]
public class EntryTests(App app)
{
    private const string UserIdToken = "MTAwMDAyMjI1";
    private const string SysCodeToken = "VU5PVFA=";

    private HttpClient Browser() => app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task The_portal_link_signs_the_user_in_and_lands_on_the_dashboard()
    {
        var browser = Browser();
        var entered = await browser.GetAsync($"/Home/Index?UserId={UserIdToken}&Syscode={Uri.EscapeDataString(SysCodeToken)}");
        Assert.Equal(HttpStatusCode.Redirect, entered.StatusCode);
        Assert.Equal("/Dashboard", entered.Headers.Location!.ToString());

        var dashboard = await browser.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Fact]
    public async Task Portal_goes_back_to_the_portal_dashboard_with_the_values_it_sent_in()
    {
        var browser = Browser();
        await browser.GetAsync($"/Home/Index?UserId={UserIdToken}&Syscode={Uri.EscapeDataString(SysCodeToken)}");

        var home = await browser.GetAsync("/Home/Home");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        // Portal:Home is blank in tests, so it is the console's /Classic.
        Assert.EndsWith($"/Classic?UserId={UserIdToken}&SysCode={Uri.EscapeDataString(SysCodeToken)}", home.Headers.Location!.ToString());
    }

    [Fact]
    public async Task A_plus_in_the_portal_values_that_arrived_as_a_space_is_put_back()
    {
        // "VU5PVFA=" carries no '+', so a space is put where the mock will only accept a '+' or
        // nothing: the space alone would make the value unreadable, and the entry refused.
        var browser = Browser();
        var entered = await browser.GetAsync($"/Home/Index?UserId={UserIdToken}&Syscode=VU5P VFA%3D");
        Assert.Equal(HttpStatusCode.Forbidden, entered.StatusCode);
        var page = await entered.Content.ReadAsStringAsync();
        Assert.Contains("could not be read", page);
    }

    [Fact]
    public async Task Logout_ends_the_session_and_goes_to_the_portal()
    {
        var browser = Browser();
        await browser.GetAsync($"/Home/Index?UserId={UserIdToken}&Syscode={Uri.EscapeDataString(SysCodeToken)}");

        var logout = await browser.GetAsync("/Home/LogOut");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        // Portal:Logout is blank in tests, so it is the login portal's root.
        Assert.EndsWith("/", logout.Headers.Location!.ToString());

        var afterwards = await browser.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, afterwards.StatusCode);
        Assert.Contains("/Home/Index", afterwards.Headers.Location!.ToString());
    }

    [Fact]
    public async Task The_root_leads_to_the_entry_with_what_the_portal_sent()
    {
        var browser = Browser();
        var root = await browser.GetAsync("/?UserId=a&SysCode=b");
        Assert.Equal("/Home/Index?UserId=a&SysCode=b", root.Headers.Location!.ToString());
    }
}
