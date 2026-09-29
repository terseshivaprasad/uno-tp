using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>FD Configuration: the deposit - amount, tenure, payout and the rest - with the backend's quote.</summary>
[Route("unotp/applications/{appNo}/deposit")]
public class DepositController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services)
    : ApplicationStepController(applications, deposits, services)
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        var form = DepositForm.From(docs.App.Deposit, docs.Ref);
        var problems = Said();
        return View(new DepositViewModel(docs, form, await QuoteAsync(docs, form.AmountProblem(docs.Config) is null ? docs.App.Deposit : null), problems));
    }

    /// <summary>
    /// Every choice saves the deposit and redraws it with the backend's quote;
    /// Proceed moves on once nothing is missing.
    /// </summary>
    [HttpPost("")]
    public async Task<IActionResult> Save(DepositForm form, string? refresh)
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (await Applications.SaveDepositAsync(docs.AppNo, docs.App.Version, form.ToDetails()) is null)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        var problems = form.Problems(docs.Config, docs.Ref);
        if (refresh is not null)
        {
            // Redrawn around the choice that changed; an amount that is wrong says so as it is typed.
            var said = new Dictionary<string, string>();
            if (form.AmountProblem(docs.Config) is { } amount && form.Amount.Length > 0) said["Amount"] = amount;
            return Back(nameof(Index), said, refresh);
        }
        return problems.Count > 0 ? Back(nameof(Index), problems) : RedirectToAction(nameof(ReviewController.Index), "Review");
    }

    /// <summary>
    /// A choice on FD Configuration, made without the page: the backend's quote for
    /// the deposit as it stands, drawn alone (_Quote) for fd-quote.js to put in
    /// place. It saves nothing - as on a bank's form, the step is saved by Proceed.
    /// </summary>
    [HttpPost("quote")]
    public async Task<IActionResult> Quote(DepositForm form)
    {
        if (await LoadAsync() is not { } docs) return NotFound();
        var deposit = form.ToDetails();
        var quote = await QuoteAsync(docs, form.AmountProblem(docs.Config) is null ? deposit : null);
        return PartialView("_Quote", new DepositViewModel(docs, form, quote, new Dictionary<string, string>()));
    }
}
