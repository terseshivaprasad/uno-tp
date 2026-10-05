using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
        // FD Configuration comes after the banks are picked.
        if (await BackToBankIfNotDoneAsync(docs) is { } toBank) return toBank;
        var rates = await RateTableAsync(docs, docs.App.Deposit?.Amount ?? 0);
        var form = DepositForm.From(docs.App.Deposit, rates);
        if (docs.App.Renewal is { } renewal) form.TakeRenewalAmount(renewal);
        var problems = Said();
        // The Form 121 box stands on this page, shown by the switch; what the last
        // upload had to say about it comes with the page.
        docs.TdsFormWanted = true;
        docs.Shown = TempData[FlashKey(docs)] is string said ? JsonSerializer.Deserialize<Flash>(said) : null;
        var quote = await QuoteAsync(docs, AmountAccepted(docs, form) ? docs.App.Deposit : null);
        var sourceOfFunds = await SourceOfFundsAsync(docs, form.AmountValue);
        var publicRates = await PublicRateTableAsync(docs, form.AmountValue);
        return View(new DepositViewModel(docs, form, rates, publicRates, quote, sourceOfFunds, problems));
    }

    /// <summary>
    /// Every choice saves the deposit and redraws it with the backend's quote;
    /// Proceed moves on once nothing is missing.
    /// </summary>
    [HttpPost("")]
    public async Task<IActionResult> Save(DepositForm form, string? refresh, string? draft)
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (await BackToBankIfNotDoneAsync(docs) is { } toBank) return toBank;
        // A renewal's amount is the deposit's, whatever was posted.
        if (docs.App.Renewal is { } renewal) form.TakeRenewalAmount(renewal);
        // The source of funds is kept only where it is asked.
        var sourceOfFunds = await SourceOfFundsAsync(docs, form.AmountValue);
        if (!sourceOfFunds.Asked) form.DropSourceOfFunds();
        if (await Applications.SaveDepositAsync(docs.AppNo, docs.App.Version, form.ToDetails(sourceOfFunds)) is null)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        // Save draft: kept as it stands, checked only on Proceed.
        if (draft is not null) return Back(nameof(Index), new());
        var rates = await RateTableAsync(docs, form.AmountValue);
        var problems = form.Problems(docs.Config, rates, sourceOfFunds, docs.App.Renewal);
        // No TDS is a claim the investor signs: the form is filed here before Proceed.
        docs.TdsFormWanted = form.NoTds;
        if (form.NoTds && docs.View(DocumentsViewModel.TdsFormSlot).Doc is null) problems["TdsForm"] = "Upload the Form 121 before proceeding, or turn the switch off";
        // Nothing else wanting, the deposit as it stands is put to the rate card itself.
        if (refresh is null && problems.Count == 0 && await NotOnRateCardAsync(docs, form, rates) is { } notOnCard) problems["Scheme"] = notOnCard;
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
        if (docs.App.Renewal is { } renewal) form.TakeRenewalAmount(renewal);
        var sourceOfFunds = await SourceOfFundsAsync(docs, form.AmountValue);
        if (await Applications.SaveDepositAsync(docs.AppNo, docs.App.Version, form.ToDetails(sourceOfFunds)) is not { } version)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        docs.App.Version = version;
        docs.TdsFormWanted = form.NoTds;
        var at = await docs.UploadAsync(DocumentsViewModel.TdsFormSlot.Key, Request.Form.Files);
        if (await Applications.SaveUploadAsync(docs.AppNo, docs.App.Version, docs.State) is null)
        {
            docs.Said = new Flash { Banner = Changed, BannerIsError = true };
            at = null;
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
        if (docs.App.Renewal is { } renewal) form.TakeRenewalAmount(renewal);
        var rates = await RateTableAsync(docs, form.AmountValue);
        var sourceOfFunds = await SourceOfFundsAsync(docs, form.AmountValue);
        var quote = await QuoteAsync(docs, AmountAccepted(docs, form) ? form.ToDetails(sourceOfFunds) : null);
        var publicRates = await PublicRateTableAsync(docs, form.AmountValue);
        return PartialView("_Quote", new DepositViewModel(docs, form, rates, publicRates, quote, sourceOfFunds, new Dictionary<string, string>()));
    }

    // FD Configuration's check against the master when it proceeds: the rate card
    // must hold a row in effect for the deposit's category, mode (a fresh
    // application or a renewal), scheme, interest frequency, tenure and rate, with
    // the amount within that row's limits. Null when it does; otherwise what to say.
    private async Task<string?> NotOnRateCardAsync(DocumentsViewModel docs, DepositForm form, RateTable rates)
    {
        var card = docs.App.RateCardRequest(docs.State.Category, docs.BranchUser);
        var payout = rates.Payouts.FirstOrDefault(p => p.Code == form.InterestPayout)?.Name ?? form.InterestPayout;
        var line = rates.Row(form.TenureMonths, form.InterestPayout);
        if (line is null)
            return $"The rate card offers no {payout} payout for {form.TenureMonths} months on {Money.Rupees(form.AmountValue)}. Change the tenure, the payout or the amount.";

        var check = new SchemeCheck(card.Category, card.ApplicationType, line.Scheme, line.Payout, line.TenureMonths, line.Rate, form.AmountValue, card.BranchUser);
        if (await Deposits.OnRateCardAsync(check)) return null;

        var mode = card.ApplicationType == RateCard.Renew ? "a renewal" : "a fresh application";
        return $"This deposit is not on the rate card: no {line.Scheme} scheme is in effect for category {card.Category} and {mode} "
            + $"that pays {payout} for {line.TenureMonths} months at {line.Rate:0.##}% on {Money.Rupees(form.AmountValue)}. "
            + "Check the category on Upload Documents, and the amount, tenure and payout here.";
    }

    // The returns are worked out once the amount passes its own checks; a renewal's amount always does.
    private static bool AmountAccepted(DocumentsViewModel docs, DepositForm form)
    {
        if (docs.IsRenewal) return true;
        return form.AmountProblem(docs.Config) is null;
    }
}
