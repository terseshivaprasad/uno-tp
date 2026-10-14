using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>Application Submitted: where the payment link went, and the deposit it carries.</summary>
public class SubmittedModel(IApplicationApi applications, IDepositApi deposits, IServiceProvider services, PaymentLinkSender paymentLinks)
    : ApplicationStepPage(applications, deposits, services)
{
    protected override bool ShowsCancelled => true;

    private const string NoLink = Messages.ReviewSummary.LinkNotSent;

    /// <summary>What the page shows.</summary>
    public ReviewViewModel View { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.App.Submitted is null) return RedirectToPage("/Review/Index");
        View = new ReviewViewModel(docs, await QuoteAsync(docs, docs.App.Deposit), null, null)
        {
            Refused = Said().GetValueOrDefault("banner"),
        };
        return Page();
    }

    /// <summary>
    /// Sends a payment link, while the application's window is open. One submitted with Try later has none yet: its
    /// first is made here, shortened and put on record. Otherwise the same short link is sent again, open for 24 hours.
    /// </summary>
    public async Task<IActionResult> OnPostResendAsync()
    {
        if (HttpContext.CurrentApplication() is not { } appNo) return Start();
        var submitted = (await Applications.FindAsync(appNo))?.Submitted;
        if (submitted is { LinkSent: false })
        {
            var (sending, shortened) = await paymentLinks.SendAsync(appNo, LinkPage.ApplicationSubmitted, HttpContext.RequestAborted);
            if (sending.Sent is null) return Back(new() { ["banner"] = sending.Refused ?? NoLink });
            if (!shortened) return Back(new() { ["banner"] = PaymentLinkSender.NotShortened });
            return RedirectToPage();
        }
        var again = await Applications.ResendLinkAsync(appNo);
        return again.Sent is null
            ? Back(new() { ["banner"] = again.Refused ?? NoLink })
            : RedirectToPage();
    }
}
