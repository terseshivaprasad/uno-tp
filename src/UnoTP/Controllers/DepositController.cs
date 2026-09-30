using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>FD Configuration: the deposit - amount, tenure, payout and the rest - with the backend's quote.</summary>
[Route("FDConfiguration/{appNo}")]
public class DepositController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services)
    : ApplicationStepController(applications, deposits, services)
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        var form = DepositForm.From(docs.App.Deposit, docs.Ref);
        var problems = Said();
        // The Form 121 box stands on this page, shown by the switch; what the last
        // upload had to say about it comes with the page.
        docs.TdsFormWanted = true;
        docs.Shown = TempData[FlashKey(docs)] is string said ? JsonSerializer.Deserialize<Flash>(said) : null;
        return View(new DepositViewModel(docs, form, await QuoteAsync(docs, docs.IsRenewal || form.AmountProblem(docs.Config) is null ? docs.App.Deposit : null), problems));
    }

    /// <summary>
    /// Every choice saves the deposit and redraws it with the backend's quote;
    /// Proceed moves on once nothing is missing.
    /// </summary>
    [HttpPost("")]
    public async Task<IActionResult> Save(DepositForm form, string? refresh, string? draft)
    {
        if (await LoadAsync() is not { } docs) return Start();
        // A renewal's amount is the deposit's, whatever was posted.
        if (docs.IsRenewal) form.Amount = Money.Group(docs.Renewal!.Amount);
        if (await Applications.SaveDepositAsync(docs.AppNo, docs.App.Version, form.ToDetails()) is null)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        // Save draft: kept as it stands, checked only on Proceed.
        if (draft is not null) return Back(nameof(Index), new());
        var problems = form.Problems(docs.Config, docs.Ref, docs.IsRenewal);
        // No TDS is a claim the investor signs: the form is filed here before Proceed.
        docs.TdsFormWanted = form.NoTds;
        if (form.NoTds && docs.View(DocumentsViewModel.TdsFormSlot).Doc is null) problems["TdsForm"] = "Upload the Form 121 before proceeding, or turn the switch off";
        if (refresh is not null)
        {
            // Redrawn around the choice that changed; an amount that is wrong says so as it is typed.
            var said = new Dictionary<string, string>();
            if (!docs.IsRenewal && form.AmountProblem(docs.Config) is { } amount && form.Amount.Length > 0) said["Amount"] = amount;
            return Back(nameof(Index), said, refresh);
        }
        return problems.Count > 0 ? Back(nameof(Index), problems) : RedirectToAction(nameof(ReviewController.Index), "Review");
    }

    /// <summary>
    /// The Form 121, filed from this page: the deposit is saved as it stands - the
    /// switch on, with it - and the copy taken as Upload Documents takes one.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(DepositForm form)
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.IsRenewal) form.Amount = Money.Group(docs.Renewal!.Amount);
        if (await Applications.SaveDepositAsync(docs.AppNo, docs.App.Version, form.ToDetails()) is not { } version)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        docs.App.Version = version;
        docs.TdsFormWanted = form.NoTds;
        var at = await docs.UploadAsync(DocumentsViewModel.TdsFormSlot.Key, Request.Form.Files);
        if (await Applications.SaveUploadAsync(docs.AppNo, docs.App.Version, docs.State) is null)
        {
            docs.Said = new Flash { Banner = Changed };
            at = null;
        }
        else
        {
            await docs.SettleAsync();
        }
        if (docs.Said is not null) TempData[FlashKey(docs)] = JsonSerializer.Serialize(docs.Said);
        return RedirectToAction(nameof(Index), null, null, at);
    }

    private static string FlashKey(DocumentsViewModel docs) => "flash-deposit:" + docs.AppNo;

    /// <summary>
    /// A choice on FD Configuration, made without the page: the backend's quote for
    /// the deposit as it stands, drawn alone (_Quote) for deposit-quote.js to put in
    /// place. It saves nothing - as on a bank's form, the step is saved by Proceed.
    /// </summary>
    [HttpPost("quote")]
    public async Task<IActionResult> Quote(DepositForm form)
    {
        if (await LoadAsync() is not { } docs) return NotFound();
        if (docs.IsRenewal) form.Amount = Money.Group(docs.Renewal!.Amount);
        var deposit = form.ToDetails();
        var quote = await QuoteAsync(docs, docs.IsRenewal || form.AmountProblem(docs.Config) is null ? deposit : null);
        return PartialView("_Quote", new DepositViewModel(docs, form, quote, new Dictionary<string, string>()));
    }
}
