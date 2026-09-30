using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// An application in full for View Application's details sheet: Review Summary's
/// sections - the deposit and its quote, the holders, the nominee, bank and payment,
/// the documents, how it is sourced - read-only, fetched when a row is opened. The
/// backend only finds the partner's own application.
/// </summary>
[Route("unotp/applications/{appNo}/details")]
public class ApplicationDetailsController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services)
    : ApplicationStepController(applications, deposits, services)
{
    [HttpGet("")]
    [RequiresFeature("view-app")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return NotFound();
        var payment = docs.App.Payment;
        var (pay, repay) = await BranchesAsync(payment?.Payment?.Ifsc ?? "", payment?.Repayment?.Ifsc ?? "");
        var review = new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), pay, repay);
        return PartialView("_ApplicationSummary", new ApplicationSummary(review, Editable: false));
    }
}
