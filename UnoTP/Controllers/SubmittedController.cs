using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Services;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Application Submitted: where the payment link went, and the deposit it carries.</summary>
[Route("ApplicationSubmitted/{appNo}")]
public class SubmittedController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services)
    : ApplicationStepController(applications, deposits, services)
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.App.Submitted is null) return RedirectToAction(nameof(ReviewController.Index), "Review");
        return View(new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), null, null)
        {
            Refused = Said().GetValueOrDefault("banner"),
        });
    }

    /// <summary>Sends a new payment link, while the application's window is open: it runs for the full validity again.</summary>
    [HttpPost("resend")]
    public async Task<IActionResult> Resend()
    {
        if (HttpContext.CurrentApplication() is not { } appNo) return Start();
        return await Applications.ResendLinkAsync(appNo) is null
            ? Back(nameof(Index), new() { ["banner"] = "A new link could not be sent: the application is paid, cancelled, or past its window." })
            : RedirectToAction(nameof(Index));
    }
}
