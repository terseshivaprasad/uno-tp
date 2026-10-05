using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// What the wizard's later steps share - Bank Details &amp; Payment, FD
/// Configuration, Review Summary and Submitted, one controller each. Each reads the
/// application its address names from the backend, and each post saves its part
/// back against the version read, then redirects, so a refresh never posts twice.
/// What a post found wrong is carried to the page it redirects to.
/// </summary>
[RequiresFeature("new-fd")]
public abstract class ApplicationStepController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services) : Controller
{
    protected const string Changed =
        "This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again.";

    protected IApplicationApi Applications => applications;
    protected IDepositApi Deposits => deposits;

    // The backend only ever finds the partner's own application.
    protected async Task<DocumentsViewModel?> LoadAsync()
    {
        var appNo = HttpContext.CurrentApplication();
        var app = appNo is null ? null : await applications.FindAsync(appNo);
        return app is null ? null : await ActivatorUtilities.CreateInstance<DocumentsViewModel>(services, app, HttpContext.Session).ReadyAsync();
    }

    // The branches two IFSCs name; either is null when it names none.
    protected async Task<(BankBranch?, BankBranch?)> BranchesAsync(string payment, string repayment) =>
        (payment.Length == 11 ? await deposits.BranchAsync(payment) : null,
         repayment.Length == 11 ? await deposits.BranchAsync(repayment) : null);

    // Paid by an instrument - a cheque - rather than electronically: only then is
    // there an account the deposit is paid from to ask for.
    protected static bool ByCheque(DocumentsViewModel docs) => docs.State.PayMode.Length > 0 && docs.DocumentOf(docs.State.PayMode) is not null;

    /// <summary>
    /// What Bank Details &amp; Payment still lacks, by field, for a form as posted or as
    /// last saved; empty when nothing does. A payment bank (for a cheque) and a
    /// repayment bank have to be picked, with their account numbers.
    /// </summary>
    protected async Task<Dictionary<string, string>> BankProblemsAsync(DocumentsViewModel docs, BankForm form)
    {
        var byCheque = ByCheque(docs);
        var (pay, repay) = await BranchesAsync(byCheque ? form.Payment.CleanIfsc : "", form.Repayment.CleanIfsc);
        var cmsLocationOnMaster = byCheque && await deposits.CmsLocationAsync(form.Cheque.CmsCode) is not null;
        var problems = form.Problems(byCheque, pay, form.RepaysToPayment(byCheque) ? pay : repay, cmsLocationOnMaster);
        // Paid online, the repayment bank has to be one the payment gateway takes.
        if (docs.State.PayMode == "Online" && form.Repayment.CleanIfsc.Length >= 4 && !docs.Ref.OnPaymentGateway(form.Repayment.CleanIfsc))
        {
            problems["Repayment.Ifsc"] = PaymentViewModel.GatewayProblemFor(repay?.Bank ?? "This bank");
        }
        return problems;
    }

    /// <summary>
    /// Where the page goes when Bank Details &amp; Payment is not complete as saved: back
    /// to it, with what is missing marked. Null when it is complete. The steps after
    /// it neither open nor go on without it, however they were reached - a draft
    /// saved half-filled, or the address typed.
    /// </summary>
    protected async Task<IActionResult?> BackToBankIfNotDoneAsync(DocumentsViewModel docs)
    {
        var problems = await BankProblemsAsync(docs, BankForm.From(docs.App.Payment));
        if (problems.Count == 0) return null;
        TempData["said"] = JsonSerializer.Serialize(problems);
        return RedirectToAction(nameof(PaymentController.Index), "Payment");
    }

    /// <summary>
    /// The rate card as FD Configuration offers it: at the amount given, or at the
    /// standing quoteAmount before one is entered. Whose card - the category, the
    /// holder's gender, purchase or renewal - the application says.
    /// </summary>
    protected Task<RateTable> RateTableAsync(DocumentsViewModel docs, long amount) =>
        RateTableAsync(docs, docs.State.Category, amount);

    /// <summary>
    /// The public category's card at the same amount: what a senior citizen's or a
    /// women's rate is compared with, row for row. The public category is the one
    /// that is not for employees, women or senior citizens.
    /// </summary>
    protected Task<RateTable> PublicRateTableAsync(DocumentsViewModel docs, long amount)
    {
        var open = docs.Ref.Categories.FirstOrDefault(c => !c.Employee && !c.Women && !c.Senior);
        return RateTableAsync(docs, open?.Code ?? docs.State.Category, amount);
    }

    private async Task<RateTable> RateTableAsync(DocumentsViewModel docs, string category, long amount)
    {
        if (amount <= 0) amount = docs.Config.QuoteAmount;
        var card = await deposits.RatesAsync(docs.App.RateCardRequest(category, docs.BranchUser));
        return new RateTable(card, docs.Ref, amount);
    }

    /// <summary>
    /// Whether the source of funds is asked for a deposit of this amount: the investor's
    /// active deposits with us come from the deposits register, by folio or, for an
    /// investor without one, by PAN and date of birth. A renewal leaves out the deposit
    /// it renews, which the new one replaces.
    /// </summary>
    protected async Task<SourceOfFundsCheck> SourceOfFundsAsync(DocumentsViewModel docs, long amount)
    {
        var renewals = services.GetRequiredService<IRenewalApi>();
        var holder = docs.App.Holder;

        IReadOnlyList<HeldDeposit>? held;
        if (holder.Folio.Length > 0) held = await renewals.DepositsByFolioAsync(holder.Folio);
        else held = await renewals.DepositsByPanAsync(holder.Pan, holder.Dob);

        long heldTotal = 0;
        foreach (var deposit in held ?? [])
        {
            if (deposit.Status is "matured" or "renewed") continue;
            if (docs.Renewal is not null && deposit.Number == docs.Renewal.DepositNumber) continue;
            heldTotal += deposit.Amount;
        }

        var investor = docs.App.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
        return SourceOfFundsCheck.For(docs.Config, heldTotal, amount, investor);
    }

    /// <summary>
    /// What the backend quotes for a deposit with an amount the card offers its tenure
    /// and payout at; null before then (kept a minute: CachedBackend).
    /// </summary>
    protected async Task<DepositQuote?> QuoteAsync(DocumentsViewModel docs, DepositDetails? deposit)
    {
        if (deposit is null) return null;
        if (deposit.Amount <= 0) return null;
        var rates = await RateTableAsync(docs, deposit.Amount);
        if (rates.Row(deposit.TenureMonths, deposit.Payout) is null) return null;
        return await deposits.QuoteAsync(new QuoteRequest(deposit.Amount, deposit.TenureMonths, deposit.Payout, docs.App.RateCardRequest(docs.State.Category, docs.BranchUser)));
    }

    // What a post found, said once on the page it redirects to.
    protected Dictionary<string, string> Said() =>
        TempData["said"] is string json ? JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [] : [];

    protected RedirectToActionResult Back(string action, Dictionary<string, string> said, string? at = null)
    {
        if (said.Count > 0) TempData["said"] = JsonSerializer.Serialize(said);
        return RedirectToAction(action, null, null, at);
    }

    // With no application in the address there is nothing to show: the partner
    // starts at Investor Identification, which opens one.
    protected RedirectToActionResult Start() => RedirectToAction(nameof(NewApplicationController.Index), "NewApplication");
}
