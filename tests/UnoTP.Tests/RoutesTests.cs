using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnoTP.Tests;

/// <summary>Every page answers under /unotp, and every old address leads to its new one.</summary>
[Collection("app")]
public class RoutesTests(App app)
{
    [Theory]
    [InlineData("/", "/unotp/entry")]
    [InlineData("/Home?UserId=a&Syscode=b", "/unotp/entry?UserId=a&Syscode=b")]
    [InlineData("/Dashboard", "/unotp")]
    [InlineData("/Home/SessionExpired", "/unotp/session-expired")]
    [InlineData("/Home/Logout", "/unotp/logout")]
    [InlineData("/Apps/UnoTp/Classic", "/unotp")]
    [InlineData("/Apps/UnoTp/Dashboard", "/unotp")]
    [InlineData("/Purchase/InvestorIdentification", "/unotp/new")]
    [InlineData("/Apps/UnoTp/Classic/SearchInvestor?pan=X", "/unotp/new?pan=X")]
    [InlineData("/Apps/UnoTp/Classic/PayInSlip", "/unotp/pay-in-slips")]
    [InlineData("/Apps/UnoTp/Classic/ShortUrl", "/unotp/links")]
    [InlineData("/Apps/UnoTp/Classic/ViewApplication", "/unotp/applications")]
    [InlineData("/Apps/UnoTp/Classic/Admin", "/unotp/admin")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/UploadDocuments", "/unotp/applications/FBBMFL26FTEST/documents")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/InvestorInfo", "/unotp/applications/FBBMFL26FTEST/investor")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/BankDetails", "/unotp/applications/FBBMFL26FTEST/payment")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/FdConfiguration", "/unotp/applications/FBBMFL26FTEST/deposit")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/ReviewSummary", "/unotp/applications/FBBMFL26FTEST/review")]
    public async Task An_old_address_leads_to_the_new_one(string old, string now)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var answer = await client.GetAsync(old);

        Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);
        Assert.Equal(now, answer.Headers.Location!.ToString());
    }

    [Fact]
    public async Task An_old_step_name_nobody_had_is_not_found()
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var answer = await client.GetAsync("/Apps/UnoTp/Application/FBBMFL26FTEST/Nope");

        Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
    }

    [Theory]
    [InlineData("/unotp")]
    [InlineData("/unotp/new")]
    [InlineData("/unotp/applications")]
    [InlineData("/unotp/pay-in-slips")]
    [InlineData("/unotp/links")]
    [InlineData("/unotp/renew")]
    public async Task A_page_opens_once_signed_in(string page)
    {
        var client = await app.SignedInAsync();

        var answer = await client.GetAsync(page);

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal(page, answer.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_page_of_a_feature_that_is_off_sends_the_user_back_to_the_dashboard_saying_why()
    {
        var client = await app.SignedInAsync();

        var answer = await client.GetAsync("/unotp/admin");

        Assert.Equal("/unotp", answer.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal("?off=admin", answer.RequestMessage.RequestUri.Query);
    }

    [Fact]
    public async Task Without_a_session_a_page_is_not_shown()
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var answer = await client.GetAsync("/unotp/new");

        Assert.NotEqual(HttpStatusCode.OK, answer.StatusCode);
    }

    [Fact]
    public async Task The_health_check_asks_nothing_of_the_backend()
    {
        var answer = await app.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
    }
}
