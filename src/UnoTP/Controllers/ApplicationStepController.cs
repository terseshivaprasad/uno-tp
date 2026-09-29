using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
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

    // What the backend quotes for a deposit that has an amount (kept a minute: CachedBackend).
    protected async Task<DepositQuote?> QuoteAsync(DocumentsViewModel docs, DepositDetails? deposit) =>
        deposit is { Amount: > 0 } d && docs.Ref.Tenures.Contains(d.TenureMonths) && docs.Ref.Payouts.Any(p => p.Code == d.Payout)
            ? await deposits.QuoteAsync(new QuoteRequest(d.Amount, d.TenureMonths, d.Payout, docs.State.Category))
            : null;

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
