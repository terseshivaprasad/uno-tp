using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Review Summary: the application as a whole, the declarations, and Submit.</summary>
[Route("unotp/applications/{appNo}/review")]
public class ReviewController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services)
    : ApplicationStepController(applications, deposits, services)
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.App.Submitted is not null) return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
        var payment = docs.App.Payment;
        var (pay, repay) = await BranchesAsync(payment?.Payment?.Ifsc ?? "", payment?.Repayment?.Ifsc ?? "");
        return View(new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), pay, repay)
        {
            Refused = Said().GetValueOrDefault("banner"),
        });
    }

    /// <summary>Submits the application once nothing blocks it and every declaration is signed.</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(int[]? declarations)
    {
        if (await LoadAsync() is not { } docs) return Start();
        var review = new ReviewViewModel(docs, null, null, null);
        if (!review.Ready) return Back(nameof(Index), new() { ["banner"] = "Something is still missing, so the application was not submitted." });
        if ((declarations ?? []).Distinct().Count(i => i >= 0 && i < docs.Ref.Declarations.Count) != docs.Ref.Declarations.Count)
            return Back(nameof(Index), new() { ["banner"] = "Every declaration has to be signed before the application is submitted." });
        if (await Applications.SubmitAsync(docs.AppNo, docs.App.Version) is null)
            return Back(nameof(Index), new() { ["banner"] = Changed });
        return RedirectToAction(nameof(SubmittedController.Index), "Submitted");
    }
}
