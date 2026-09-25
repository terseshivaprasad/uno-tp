using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// The wizard's later steps, after Investor Information: Bank Details &amp;
/// Payment, FD Configuration, Review Summary and Submitted. Each reads the
/// application the session is on from the backend, and each post saves its part
/// back against the version read, then redirects, so a refresh never posts twice.
/// What a post found wrong is carried to the page it redirects to.
/// </summary>
[RequiresFeature("new-fd")]
[Route("Apps/UnoTp/Application")]
public class ApplicationController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services) : Controller
{
    private const string Changed =
        "This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again.";

    // ----- Bank Details & Payment ---------------------------------------------

    [HttpGet("BankDetails")]
    public async Task<IActionResult> BankDetails()
    {
        if (await LoadAsync() is not { } docs) return Start();
        var form = BankForm.From(docs.App.Payment);
        var (pay, repay) = await BranchesAsync(form.Payment.CleanIfsc, form.Repayment.SameAsPayment ? form.Payment.CleanIfsc : form.Repayment.CleanIfsc);
        return View(new BankDetailsViewModel(docs, form, pay, repay, Said()));
    }

    /// <summary>
    /// Saves what was typed, finished or not. Find looks an IFSC up and comes back;
    /// Proceed moves on once nothing is missing.
    /// </summary>
    [HttpPost("BankDetails")]
    public async Task<IActionResult> BankDetailsPost(BankForm form)
    {
        if (await LoadAsync() is not { } docs) return Start();
        var byCheque = docs.State.PayMode.Length > 0 && docs.DocumentOf(docs.State.PayMode) is not null;
        if (await applications.SavePaymentAsync(docs.AppNo, docs.App.Version, form.ToDetails(byCheque)) is null)
            return Back(nameof(BankDetails), new() { ["banner"] = Changed });
        if (form.Find is not null) return RedirectToAction(nameof(BankDetails), null, null, form.Find == "repayment" ? "repay-ifsc" : "pay-ifsc");

        var (pay, repay) = await BranchesAsync(form.Payment.CleanIfsc, form.Repayment.CleanIfsc);
        var problems = form.Problems(byCheque, pay, form.Repayment.SameAsPayment ? pay : repay, docs.Ref.CmsLocations);
        return problems.Count > 0 ? Back(nameof(BankDetails), problems) : RedirectToAction(nameof(FdConfiguration));
    }

    // ----- FD Configuration -----------------------------------------------------

    [HttpGet("FdConfiguration")]
    public async Task<IActionResult> FdConfiguration()
    {
        if (await LoadAsync() is not { } docs) return Start();
        var form = DepositForm.From(docs.App.Deposit, docs.Ref);
        var problems = Said();
        return View(new FdConfigViewModel(docs, form, await QuoteAsync(docs, form.AmountProblem(docs.Config) is null ? docs.App.Deposit : null), problems));
    }

    /// <summary>
    /// Every choice saves the deposit and redraws it with the backend's quote;
    /// Proceed moves on once nothing is missing.
    /// </summary>
    [HttpPost("FdConfiguration")]
    public async Task<IActionResult> FdConfigurationPost(DepositForm form, string? refresh)
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (await applications.SaveDepositAsync(docs.AppNo, docs.App.Version, form.ToDetails()) is null)
            return Back(nameof(FdConfiguration), new() { ["banner"] = Changed });
        var problems = form.Problems(docs.Config, docs.Ref);
        if (refresh is not null)
        {
            // Redrawn around the choice that changed; an amount that is wrong says so as it is typed.
            var said = new Dictionary<string, string>();
            if (form.AmountProblem(docs.Config) is { } amount && form.Amount.Length > 0) said["Amount"] = amount;
            return Back(nameof(FdConfiguration), said, refresh);
        }
        return problems.Count > 0 ? Back(nameof(FdConfiguration), problems) : RedirectToAction(nameof(ReviewSummary));
    }

    // ----- Review Summary -------------------------------------------------------

    [HttpGet("ReviewSummary")]
    public async Task<IActionResult> ReviewSummary()
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.App.Submitted is not null) return RedirectToAction(nameof(Submitted));
        var payment = docs.App.Payment;
        var (pay, repay) = await BranchesAsync(payment?.Payment?.Ifsc ?? "", payment?.Repayment?.Ifsc ?? "");
        return View(new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), pay, repay)
        {
            Refused = Said().GetValueOrDefault("banner"),
        });
    }

    /// <summary>Submits the application once nothing blocks it and every declaration is signed.</summary>
    [HttpPost("ReviewSummary/Send")]
    public async Task<IActionResult> Send(int[]? declarations)
    {
        if (await LoadAsync() is not { } docs) return Start();
        var review = new ReviewViewModel(docs, null, null, null);
        if (!review.Ready) return Back(nameof(ReviewSummary), new() { ["banner"] = "Something is still missing, so the application was not submitted." });
        if ((declarations ?? []).Distinct().Count(i => i >= 0 && i < docs.Ref.Declarations.Count) != docs.Ref.Declarations.Count)
            return Back(nameof(ReviewSummary), new() { ["banner"] = "Every declaration has to be signed before the application is submitted." });
        if (await applications.SubmitAsync(docs.AppNo, docs.App.Version) is null)
            return Back(nameof(ReviewSummary), new() { ["banner"] = Changed });
        return RedirectToAction(nameof(Submitted));
    }

    // ----- Submitted --------------------------------------------------------------

    [HttpGet("Submitted")]
    public async Task<IActionResult> Submitted()
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.App.Submitted is null) return RedirectToAction(nameof(ReviewSummary));
        return View(new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), null, null)
        {
            Refused = Said().GetValueOrDefault("banner"),
        });
    }

    /// <summary>Sends the payment link again, while a resend is left.</summary>
    [HttpPost("Submitted/Resend")]
    public async Task<IActionResult> Resend()
    {
        if (HttpContext.Session.CurrentApplication() is not { } appNo) return Start();
        return await applications.ResendLinkAsync(appNo) is null
            ? Back(nameof(Submitted), new() { ["banner"] = "The link could not be sent again: no resend is left." })
            : RedirectToAction(nameof(Submitted));
    }

    // ----- Shared -----------------------------------------------------------------

    // The backend only ever finds the partner's own application.
    private async Task<UploadDocumentsViewModel?> LoadAsync()
    {
        var appNo = HttpContext.Session.CurrentApplication();
        var app = appNo is null ? null : await applications.FindAsync(appNo);
        return app is null ? null : await ActivatorUtilities.CreateInstance<UploadDocumentsViewModel>(services, app, HttpContext.Session).ReadyAsync();
    }

    // The branches two IFSCs name; either is null when it names none.
    private async Task<(BankBranch?, BankBranch?)> BranchesAsync(string payment, string repayment) =>
        (payment.Length == 11 ? await deposits.BranchAsync(payment) : null,
         repayment.Length == 11 ? await deposits.BranchAsync(repayment) : null);

    // What the backend quotes for a deposit that has an amount.
    private async Task<DepositQuote?> QuoteAsync(UploadDocumentsViewModel docs, DepositDetails? deposit) =>
        deposit is { Amount: > 0 } d && docs.Ref.Tenures.Contains(d.TenureMonths) && docs.Ref.Payouts.Any(p => p.Code == d.Payout)
            ? await deposits.QuoteAsync(new QuoteRequest(d.Amount, d.TenureMonths, d.Payout, docs.State.Category))
            : null;

    // What a post found, said once on the page it redirects to.
    private Dictionary<string, string> Said() =>
        TempData["said"] is string json ? JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [] : [];

    private RedirectToActionResult Back(string action, Dictionary<string, string> said, string? at = null)
    {
        if (said.Count > 0) TempData["said"] = JsonSerializer.Serialize(said);
        return RedirectToAction(action, null, null, at);
    }

    // With no application in the session there is nothing to show: the partner
    // starts at Investor Identification, which opens one.
    private RedirectToActionResult Start() => RedirectToAction(nameof(InvestorIdentificationController.Index), "InvestorIdentification");
}
