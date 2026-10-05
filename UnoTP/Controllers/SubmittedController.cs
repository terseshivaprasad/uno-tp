using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>Application Submitted: where the payment link went, and the deposit it carries.</summary>
[Route("ApplicationSubmitted/{appNo}")]
public class SubmittedController(IApplicationApi applications, IDepositApi deposits, IServiceProvider services, PaymentLinkSender paymentLinks)
    : ApplicationStepController(applications, deposits, services)
{
    protected override bool ShowsCancelled => true;

    private const string NoLink = "A link could not be sent: the application is paid, cancelled, or past its window.";

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

    /// <summary>
    /// Sends a payment link, while the application's window is open. One submitted with Try later has none yet: its
    /// first is made here, shortened and put on record. Otherwise the link it has is sent again, for the full validity.
    /// </summary>
    [HttpPost("resend")]
    public async Task<IActionResult> Resend()
    {
        if (HttpContext.CurrentApplication() is not { } appNo) return Start();
        var submitted = (await Applications.FindAsync(appNo))?.Submitted;
        if (submitted is { LinkSent: false })
        {
            var (sent, shortened) = await paymentLinks.SendAsync(appNo, HttpContext.RequestAborted);
            if (sent is null) return Back(nameof(Index), new() { ["banner"] = NoLink });
            if (!shortened) return Back(nameof(Index), new() { ["banner"] = PaymentLinkSender.NotShortened });
            return RedirectToAction(nameof(Index));
        }
        return await Applications.ResendLinkAsync(appNo) is null
            ? Back(nameof(Index), new() { ["banner"] = NoLink })
            : RedirectToAction(nameof(Index));
    }
}
