using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Bank Details &amp; Payment: the account the deposit is paid from, and the one it repays to.</summary>
[Route("BankDetails/{appNo}")]
public class PaymentController(IApplicationApi applications, IDepositApi deposits, IInvestorApi investors, IServiceProvider services)
    : ApplicationStepController(applications, deposits, services)
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        var form = BankForm.From(docs.App.Payment);
        var byCheque = ByCheque(docs);
        // Paid by a cheque whose bank confirmed it, what is still empty comes off the cheque.
        var filled = byCheque && docs.State.Docs.ContainsKey("payment") && form.FillFrom(docs.State.ChequeRead);
        var (pay, repay) = await BranchesAsync(byCheque ? form.Payment.CleanIfsc : "", form.RepaysToPayment(byCheque) ? form.Payment.CleanIfsc : form.Repayment.CleanIfsc);
        return View(new PaymentViewModel(docs, form, pay, repay, Said())
        {
            FilledFromCheque = filled,
            AccountsOnRecord = await AccountsOnRecordAsync(docs),
        });
    }

    /// <summary>
    /// The bank search's suggestions: the branches whose bank name, branch, IFSC or
    /// MICR holds what was typed, as the backend finds them. The bank master is big:
    /// nothing is looked up until three characters are typed.
    /// </summary>
    // The answer is bank master data, nothing of the investor's: the browser keeps it a
    // minute (privately), so typing back over the same words asks nothing, and a bank
    // added to the backend is offered within the minute.
    [HttpGet("branches")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Branches(string? q)
    {
        if (HttpContext.CurrentApplication() is null) return NotFound();
        var text = (q ?? "").Trim();
        if (!TypedSearch.LongEnough(text)) return Json(Array.Empty<BankBranch>());
        return Json(await Deposits.SearchBranchesAsync(text[..Math.Min(text.Length, 60)]));
    }

    /// <summary>
    /// The Axis CMS branch search's suggestions: the branches whose label - name, location
    /// and PIN code - holds what was typed, each shown by that label. Like the bank master,
    /// the Axis CMS master is searched and never listed whole.
    /// </summary>
    [HttpGet("cms-locations")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> CmsLocations(string? q)
    {
        if (HttpContext.CurrentApplication() is null) return NotFound();
        var text = (q ?? "").Trim();
        if (!TypedSearch.LongEnough(text)) return Json(Array.Empty<CmsLocation>());
        return Json(await Deposits.SearchCmsLocationsAsync(text[..Math.Min(text.Length, 60)]));
    }

    /// <summary>
    /// Saves what was typed, finished or not. Find looks an IFSC up and comes back;
    /// Proceed moves on once nothing is missing.
    /// </summary>
    [HttpPost("")]
    public async Task<IActionResult> Save(BankForm form, string? draft)
    {
        if (await LoadAsync() is not { } docs) return Start();
        var byCheque = ByCheque(docs);

        // An account on record picked: its IFSC and number fill the repayment fields,
        // and the page comes back with its branch looked up, as for a typed one.
        if (form.UseRepayment is { } n)
        {
            var onRecord = await AccountsOnRecordAsync(docs);
            if (n >= 0 && n < onRecord.Count)
            {
                form.Repayment.Ifsc = onRecord[n].Ifsc;
                form.Repayment.AccountNumber = onRecord[n].AccountNumber;
                form.Repayment.AccountNumberConfirm = onRecord[n].AccountNumber;
                form.Repayment.SameAsPayment = false;
            }
            form.Find = "repayment";
        }

        // The Axis CMS branch is picked from its search, which gives its code: the name
        // kept is the master's own for that code. A name typed without a pick has no
        // code, and a code the master does not hold is not kept.
        var cms = byCheque ? await Deposits.CmsLocationAsync(form.Cheque.CmsCode) : null;
        if (cms is null) form.Cheque.CmsCode = "";
        else form.Cheque.CmsLocation = cms.Name;
        if (await Applications.SavePaymentAsync(docs.AppNo, docs.App.Version, form.ToDetails(byCheque)) is null)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        if (form.Find is not null) return RedirectToAction(nameof(Index), null, null, form.Find == "repayment" ? "repay-ifsc" : "pay-ifsc");
        // Save draft: kept as it stands, checked only on Proceed.
        if (draft is not null) return Back(nameof(Index), new());

        // Without a payment bank (for a cheque) and a repayment bank picked, it does not go on.
        var problems = await BankProblemsAsync(docs, form);
        return problems.Count > 0 ? Back(nameof(Index), problems) : RedirectToAction(nameof(DepositController.Index), "Deposit");
    }

    // The repayment accounts on the deposit being renewed. None for a fresh purchase:
    // an account is carried over only in a renewal, from the folio's selected deposit.
    private async Task<IReadOnlyList<AccountOnRecord>> AccountsOnRecordAsync(DocumentsViewModel docs)
    {
        var folio = docs.App.Holder.Folio;
        if (folio.Length == 0) return [];
        if (docs.App.Renewal is null) return [];
        return await investors.AccountsOnDepositAsync(folio, docs.App.Renewal.DepositNumber);
    }
}
