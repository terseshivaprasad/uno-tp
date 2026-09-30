using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnoTP.Tests;

/// <summary>Every page answers at the old app's address, and every other old address leads to it.</summary>
[Collection("app")]
public class RoutesTests(App app)
{
    [Theory]
    [InlineData("/", "/Home/Index")]
    [InlineData("/?UserId=a&SysCode=b", "/Home/Index?UserId=a&SysCode=b")]
    [InlineData("/Apps/UnoTp/Classic", "/Dashboard")]
    [InlineData("/Apps/UnoTp/Dashboard", "/Dashboard")]
    [InlineData("/Purchase/InvestorIdentification", "/SearchInvestor")]
    [InlineData("/Apps/UnoTp/Classic/SearchInvestor?pan=X", "/SearchInvestor?pan=X")]
    [InlineData("/UploadInvestorDocuments", "/SearchInvestor")]
    [InlineData("/InvestorInformation", "/SearchInvestor")]
    [InlineData("/Apps/UnoTp/Classic/PayInSlip", "/PayInSlip")]
    [InlineData("/Apps/UnoTp/Classic/ShortUrl", "/ShortUrl")]
    [InlineData("/Apps/UnoTp/Classic/ViewApplication", "/ViewApplication")]
    [InlineData("/Apps/UnoTp/Classic/Admin", "/Admin")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/UploadDocuments", "/UploadInvestorDocuments/FBBMFL26FTEST")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/InvestorInfo", "/InvestorInformation/FBBMFL26FTEST")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/BankDetails", "/BankDetails/FBBMFL26FTEST")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/FdConfiguration", "/FDConfiguration/FBBMFL26FTEST")]
    [InlineData("/Apps/UnoTp/Application/FBBMFL26FTEST/ReviewSummary", "/ReviewSummary/FBBMFL26FTEST")]
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
    [InlineData("/Dashboard")]
    [InlineData("/SearchInvestor")]
    [InlineData("/ViewApplication")]
    [InlineData("/PayInSlip")]
    [InlineData("/ShortUrl")]
    [InlineData("/RenewalDashboard")]
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

        var answer = await client.GetAsync("/Admin");

        Assert.Equal("/Dashboard", answer.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal("?off=admin", answer.RequestMessage.RequestUri.Query);
    }

    [Fact]
    public async Task Without_a_session_a_page_is_not_shown()
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var answer = await client.GetAsync("/SearchInvestor");

        Assert.NotEqual(HttpStatusCode.OK, answer.StatusCode);
    }

    [Fact]
    public async Task The_health_check_asks_nothing_of_the_backend()
    {
        var answer = await app.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
    }
}
