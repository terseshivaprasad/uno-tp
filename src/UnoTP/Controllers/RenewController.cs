using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Renew FD: find the folio, pick the deposit, say what is renewed and for how
/// long, and send it to the investor to accept. The deposits, the quote and the
/// renewal itself are the backend's (<see cref="IRenewalApi"/>); the page only
/// asks. As on the deposit step, a choice redraws the quote alone (fd-quote.js),
/// and nothing is saved until Proceed.
/// </summary>
[RequiresFeature("renew")]
[Route("unotp/renew")]
public class RenewController(IRenewalApi renewals, IDepositApi deposits, Lookups lookups) : Controller
{
    private const string ProblemsKey = "renew-problems";

    /// <summary>The folio searched, and the deposits it holds; the renewals asked for so far.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? folio)
    {
        var number = (folio ?? "").Trim().ToUpperInvariant();
        var held = number.Length > 0 ? await renewals.DepositsAsync(number) : null;
        return View(new RenewViewModel(number, held, await renewals.RenewalsAsync(), await lookups.ReferenceAsync(), await lookups.ConfigAsync()));
    }

    /// <summary>One deposit, and the renewal to ask for it. A post turned back comes here with what it said.</summary>
    [HttpGet("{number}")]
    public async Task<IActionResult> Deposit(string number, string? mode, int? tenure, string? payout)
    {
        if (await renewals.DepositAsync(number) is not { } deposit) return NotFound();
        var reference = await lookups.ReferenceAsync();
        var form = RenewForm.Opening(deposit, reference);
        if (mode is not null) form.Mode = mode;
        if (tenure is not null) form.TenureMonths = tenure.Value;
        if (payout is not null) form.Payout = payout;
        var problems = TempData[ProblemsKey] is string said ? JsonSerializer.Deserialize<Dictionary<string, string>>(said)! : new Dictionary<string, string>();
        return View(new RenewDepositViewModel(deposit, form, await QuoteAsync(deposit, form), reference, problems));
    }

    /// <summary>The quote alone, for the renewal as the choices stand (fd-quote.js). Saves nothing.</summary>
    [HttpPost("{number}/quote")]
    public async Task<IActionResult> Quote(string number, RenewForm form)
    {
        if (await renewals.DepositAsync(number) is not { } deposit) return NotFound();
        return PartialView("_RenewQuote", new RenewDepositViewModel(deposit, form, await QuoteAsync(deposit, form), await lookups.ReferenceAsync(), new Dictionary<string, string>()));
    }

    /// <summary>Proceed: the renewal asked of the backend, which sends the investor the link.</summary>
    [HttpPost("{number}")]
    public async Task<IActionResult> Renew(string number, RenewForm form)
    {
        if (await renewals.DepositAsync(number) is not { } deposit) return NotFound();
        var problems = form.Problems(deposit, await lookups.ReferenceAsync());
        if (problems.Count == 0)
        {
            if (await renewals.RenewAsync(form.ToRequest(deposit)) is { } record)
                return RedirectToAction(nameof(Done), new { number, id = record.Id });
            problems["Deposit"] = "The backend did not take this renewal: the deposit cannot be renewed now. Search the folio again to see it as it stands.";
        }
        TempData[ProblemsKey] = JsonSerializer.Serialize(problems);
        return RedirectToAction(nameof(Deposit), new { number, mode = form.Mode, tenure = form.TenureMonths, payout = form.Payout });
    }

    /// <summary>What was asked for, as recorded, and where the acceptance link went.</summary>
    [HttpGet("{number}/done")]
    public async Task<IActionResult> Done(string number, string? id)
    {
        var record = (await renewals.RenewalsAsync()).FirstOrDefault(r => r.Id == id && r.DepositNumber == number);
        return record is null ? RedirectToAction(nameof(Index)) : View(new RenewDoneViewModel(record, await lookups.ReferenceAsync()));
    }

    // The backend's quote for the renewal as it stands; none while a choice is not made, or the backend cannot say.
    private async Task<DepositQuote?> QuoteAsync(HeldDeposit deposit, RenewForm form)
    {
        if (form.TenureMonths == 0 || form.Payout.Length == 0 || form.Mode.Length == 0) return null;
        try
        {
            return await deposits.QuoteAsync(new QuoteRequest(form.AmountFor(deposit), form.TenureMonths, form.Payout, deposit.Category, RenewForm.RenewsOn(deposit)));
        }
        catch (Exception e) when (e is ArgumentException or HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
