using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Renew FD: find the folio, see its deposits and what renewing each means, and
/// open the renewal. A renewal is an application like a new deposit's, opened by
/// the backend with the deposit's holders, repayment account and maturity amount
/// on it (<see cref="IRenewalApi.StartAsync"/>); from there it goes through the
/// same steps - documents, holders, bank, deposit, review, submit - and the
/// investor accepts it the same way.
/// </summary>
[RequiresFeature("renew")]
[Route("unotp/renew")]
public class RenewController(IRenewalApi renewals, Lookups lookups) : Controller
{
    /// <summary>The folio searched, and the deposits it holds.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? folio)
    {
        var number = (folio ?? "").Trim().ToUpperInvariant();
        var held = number.Length > 0 ? await renewals.DepositsAsync(number) : null;
        return View(new RenewViewModel(number, held, await lookups.ReferenceAsync(), await lookups.ConfigAsync(), TempData["said"] as string));
    }

    /// <summary>Renew: the application opened from the deposit, and the partner taken to its first step.</summary>
    [HttpPost("{number}/start")]
    public async Task<IActionResult> Start(string number, string? folio)
    {
        if (await renewals.StartAsync(number) is { } app)
            return RedirectToAction(nameof(DocumentsController.Index), "Documents", new { appNo = app.AppNo });
        TempData["said"] = $"Deposit {number} cannot be renewed now. The list shows it as it stands.";
        return RedirectToAction(nameof(Index), new { folio });
    }
}
