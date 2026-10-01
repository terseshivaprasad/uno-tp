using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Renew FD, the old RenewalDashboard: the investor searched by PAN and date of
/// birth or by folio, their deposits listed with where each stands, and a renewal
/// entered for one that is due. Renew opens an application like a new deposit's,
/// which the backend fills with the deposit's holders, repayment account and
/// maturity amount (<see cref="IRenewalApi.StartAsync"/>); from there it goes
/// through the same steps, and the investor accepts it the same way.
/// </summary>
[RequiresFeature("renew")]
[Route("RenewalDashboard")]
public class RenewController(IRenewalApi renewals, Lookups lookups) : Controller
{
    /// <summary>The search, and the deposits it found. Opened bare, nothing is searched yet.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? by, string? pan, string? dd, string? mm, string? yyyy, string? folio)
    {
        var search = new RenewSearch(by == "folio" ? "folio" : "pan", Clean(pan), Clean(dd), Clean(mm), Clean(yyyy), Clean(folio));
        var searched = by is not null;
        var problems = searched ? search.Problems() : new Dictionary<string, string>();
        IReadOnlyList<HeldDeposit>? deposits = null;
        if (searched && problems.Count == 0)
            deposits = search.ByFolio ? await renewals.DepositsByFolioAsync(search.Folio) : await renewals.DepositsByPanAsync(search.Pan, search.Dob!);
        return View(new RenewViewModel(search, searched, problems, deposits, await lookups.ReferenceAsync(), await lookups.ConfigAsync(), TempData["said"] as string));
    }

    /// <summary>Renew: the application opened from the deposit, and the partner taken to its first step.</summary>
    [HttpPost("{number}/start")]
    public async Task<IActionResult> Start(string number, string? by, string? pan, string? dd, string? mm, string? yyyy, string? folio)
    {
        if (await renewals.StartAsync(number) is { } app)
            return RedirectToAction(nameof(DocumentsController.Index), "Documents", new { appNo = app.AppNo });
        TempData["said"] = $"Deposit {number} cannot be renewed now. The list shows it as it stands.";
        return RedirectToAction(nameof(Index), new { by, pan, dd, mm, yyyy, folio });
    }

    /// <summary>Cancels the renewal request for a deposit: its application goes, and the deposit is due for renewal again.</summary>
    [HttpPost("{number}/cancel")]
    public async Task<IActionResult> Cancel(string number, string? by, string? pan, string? dd, string? mm, string? yyyy, string? folio)
    {
        if (await renewals.CancelAsync(number))
            TempData["said"] = $"The renewal request for deposit {number} is cancelled. It is due for renewal again.";
        else
            TempData["said"] = $"There is no renewal request for deposit {number} to cancel.";
        return RedirectToAction(nameof(Index), new { by, pan, dd, mm, yyyy, folio });
    }

    private static string Clean(string? value) => (value ?? "").Trim().ToUpperInvariant();
}
