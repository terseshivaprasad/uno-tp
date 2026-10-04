using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Review Summary: the application as a whole, and Submit.</summary>
[Route("ReviewSummary/{appNo}")]
public class ReviewController(
    IApplicationApi applications, IDepositApi deposits, IServiceProvider services,
    IShortLinkService shortLinks, IOptions<PaymentLinkOptions> paymentLink, ILogger<ReviewController> log)
    : ApplicationStepController(applications, deposits, services)
{
    public const string NotShortened = "The payment link could not be shortened, so the investor gets it in full.";

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        // A submitted application has nothing left to review. This page is where the
        // browser's Back lands from Application Submitted, so it goes on to the
        // dashboard: sending it back to Application Submitted would leave Back going nowhere.
        if (docs.App.Submitted is not null) return RedirectToAction(nameof(DashboardController.Index), "Dashboard");
        var payment = docs.App.Payment;
        var (pay, repay) = await BranchesAsync(payment?.Payment?.Ifsc ?? "", payment?.Repayment?.Ifsc ?? "");
        var sourceOfFunds = await SourceOfFundsAsync(docs, docs.App.Deposit?.Amount ?? 0);
        return View(new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), pay, repay, sourceOfFunds)
        {
            Refused = Said().GetValueOrDefault("banner"),
        });
    }

    /// <summary>
    /// Submits the application once nothing blocks it. The payment link goes with it, shortened first when the shortener
    /// answers; a shortener that does not answer never holds up a submission.
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit()
    {
        if (await LoadAsync() is not { } docs) return Start();
        // Submitted already - from another tab, or by a second press: where it went is on its own page.
        if (docs.App.Submitted is not null) return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
        var review = new ReviewViewModel(docs, null, null, null, await SourceOfFundsAsync(docs, docs.App.Deposit?.Amount ?? 0));
        if (!review.Ready) return Back(nameof(Index), new() { ["banner"] = "Something is still missing, so the application was not submitted." });

        var (link, said) = await PaymentLinkAsync(docs.AppNo);
        if (await Applications.SubmitAsync(docs.AppNo, docs.App.Version, link) is null)
        {
            // Refused because it was submitted in the meantime: that submission's page stands.
            if ((await Applications.FindAsync(docs.AppNo))?.Submitted is not null) return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
            return Back(nameof(Index), new() { ["banner"] = Changed });
        }
        if (said.Count > 0) TempData["said"] = System.Text.Json.JsonSerializer.Serialize(said);
        return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
    }

    // The payment link for the application, shortened when it can be. Whatever
    // stops the shortening is logged and said once on Submitted, and the long link
    // goes instead; with no template configured there is no link, and the backend
    // makes its own.
    private async Task<(PaymentLink? Link, Dictionary<string, string> Said)> PaymentLinkAsync(string appNo)
    {
        if (paymentLink.Value.For(appNo) is not { } url) return (null, []);
        try
        {
            return (new PaymentLink(url, await shortLinks.ShortenAsync(url, HttpContext.RequestAborted)), []);
        }
        catch (ExternalServiceException e)
        {
            log.LogWarning(e, "Payment link for {AppNo} not shortened (trace {TraceId}): {Message}", appNo, e.TraceId, e.Message);
            return (new PaymentLink(url, null), new() { ["banner"] = NotShortened });
        }
    }
}
