using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Bank Details &amp; Payment: the account the deposit is paid from, and the one it repays to.</summary>
[Route("BankDetails/{appNo}")]
public class PaymentController(IApplicationApi applications, IDepositApi deposits, IDemoApi demo, FeatureSet features, IInvestorApi investors, IServiceProvider services)
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
            Demo = features.Flags.DemoData ? await demo.BanksAsync() : null,
            FilledFromCheque = filled,
            AccountsOnRecord = await AccountsOnRecordAsync(docs),
        });
    }

    /// <summary>
    /// The bank search's suggestions: the branches whose bank name, branch, IFSC or
    /// MICR holds what was typed, as the backend finds them.
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
        if (text.Length < 2) return Json(Array.Empty<BankBranch>());
        return Json(await Deposits.SearchBranchesAsync(text[..Math.Min(text.Length, 60)]));
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

        // The CMS branch is typed: kept as the backend spells it, whatever the case typed.
        var cms = form.Cheque.CmsLocation.Trim();
        form.Cheque.CmsLocation = docs.Ref.CmsLocations.FirstOrDefault(l => string.Equals(l, cms, StringComparison.OrdinalIgnoreCase)) ?? cms;
        if (await Applications.SavePaymentAsync(docs.AppNo, docs.App.Version, form.ToDetails(byCheque)) is null)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        if (form.Find is not null) return RedirectToAction(nameof(Index), null, null, form.Find == "repayment" ? "repay-ifsc" : "pay-ifsc");
        // Save draft: kept as it stands, checked only on Proceed.
        if (draft is not null) return Back(nameof(Index), new());

        var (pay, repay) = await BranchesAsync(byCheque ? form.Payment.CleanIfsc : "", form.Repayment.CleanIfsc);
        var problems = form.Problems(byCheque, pay, form.RepaysToPayment(byCheque) ? pay : repay, docs.Ref.CmsLocations);
        // Paid online, the repayment bank has to be one the payment gateway takes.
        if (docs.State.PayMode == "Online" && form.Repayment.CleanIfsc.Length >= 4 && !docs.Ref.OnPaymentGateway(form.Repayment.CleanIfsc))
        {
            problems["Repayment.Ifsc"] = PaymentViewModel.GatewayProblemFor(repay?.Bank ?? "This bank");
        }
        return problems.Count > 0 ? Back(nameof(Index), problems) : RedirectToAction(nameof(DepositController.Index), "Deposit");
    }

    // Paid by an instrument - a cheque - rather than electronically: only then is
    // there an account the deposit is paid from to ask for.
    private static bool ByCheque(DocumentsViewModel docs) => docs.State.PayMode.Length > 0 && docs.DocumentOf(docs.State.PayMode) is not null;

    // The repayment accounts on the folio's earlier deposits; none for an investor without a folio.
    private async Task<IReadOnlyList<AccountOnRecord>> AccountsOnRecordAsync(DocumentsViewModel docs)
    {
        var folio = docs.App.Holder.Folio;
        if (folio.Length == 0) return [];
        return await investors.AccountsByFolioAsync(folio);
    }
}
