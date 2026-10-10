using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// An application in full for View Application's details sheet: Review Summary's
/// sections - the deposit and its quote, the holders, the nominee, bank and payment,
/// the documents, how it is sourced - read-only, fetched when a row is opened. The
/// backend only finds the partner's own application.
/// </summary>
[RequiresFeature("view-app")]
public class ApplicationDetailsModel(IApplicationApi applications, IDepositApi deposits, IServiceProvider services)
    : ApplicationStepPage(applications, deposits, services)
{
    protected override bool ShowsCancelled => true;

    /// <summary>What the sheet shows.</summary>
    public ApplicationSummary Summary { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadAsync() is not { } docs) return NotFound();
        var payment = docs.App.Payment;
        var (pay, repay) = await BranchesAsync(payment?.Payment?.Ifsc ?? "", payment?.Repayment?.Ifsc ?? "");
        var review = new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), pay, repay);
        Summary = new ApplicationSummary(review, Editable: false);
        return Page();
    }
}
