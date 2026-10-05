using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Review Summary: the application as a whole, and Submit.</summary>
[Route("ReviewSummary/{appNo}")]
public class ReviewController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services, PaymentLinkSender paymentLinks)
    : ApplicationStepController(applications, deposits, services)
{
    /// <summary>What the submit dialog's Try later posts as "link": the application is submitted, its link left for later.</summary>
    public const string LinkLater = "later";

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
            BankProblems = await BankProblemsAsync(docs, BankForm.From(payment)),
        });
    }

    /// <summary>
    /// Submits the application once nothing blocks it: it is saved as submitted first, always. On Submit &amp; send link
    /// its payment link is then made, shortened and put on record; a shortener that does not answer never holds up a
    /// submission. On Try later (<paramref name="link"/> = "later") the application alone is saved: no link is made
    /// until one is asked for, on Application Submitted.
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(string? link)
    {
        if (await LoadAsync() is not { } docs) return Start();
        // Submitted already - from another tab, or by a second press: where it went is on its own page.
        if (docs.App.Submitted is not null) return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
        var review = new ReviewViewModel(docs, null, null, null, await SourceOfFundsAsync(docs, docs.App.Deposit?.Amount ?? 0))
        {
            BankProblems = await BankProblemsAsync(docs, BankForm.From(docs.App.Payment)),
        };
        if (!review.Ready) return Back(nameof(Index), new() { ["banner"] = "Something is still missing, so the application was not submitted." });

        if (await Applications.SubmitAsync(docs.AppNo, docs.App.Version) is null)
        {
            // Refused because it was submitted in the meantime: that submission's page stands.
            if ((await Applications.FindAsync(docs.AppNo))?.Submitted is not null) return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
            return Back(nameof(Index), new() { ["banner"] = Changed });
        }
        if (link == LinkLater) return RedirectToAction(nameof(SubmittedController.Index), "Submitted");

        var (_, shortened) = await paymentLinks.SendAsync(docs.AppNo, HttpContext.RequestAborted);
        if (!shortened) TempData["said"] = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["banner"] = PaymentLinkSender.NotShortened });
        return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
    }
}
