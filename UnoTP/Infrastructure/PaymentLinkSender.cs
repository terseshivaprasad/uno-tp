using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.Infrastructure;

/// <summary>
/// Makes a submitted application's payment link and puts it on record. The
/// application is saved first, always; then the page the investor pays on is built
/// (PaymentLink:Template for a purchase, PaymentLink:RenewalTemplate for a renewal),
/// shortened when the shortener answers, and saved against
/// the application - in the payment link table for a purchase, in the re-payment
/// link table for a renewal. Called on Submit &amp; send link, and later for an
/// application submitted with Try later.
/// </summary>
public sealed class PaymentLinkSender(
    IApplicationApi applications, IShortLinkService shortLinks, IOptions<PaymentLinkOptions> paymentLink, ILogger<PaymentLinkSender> log)
{
    public const string NotShortened = "The payment link could not be shortened, so the investor gets it in full.";

    /// <summary>
    /// The submission with its link on record, or null when no link may go any more
    /// (the application is paid, cancelled, or past its days). Shortened is false
    /// when the shortener did not answer and the long link went instead.
    /// </summary>
    public async Task<(Submission? Sent, bool Shortened)> SendAsync(string appNo, CancellationToken ct)
    {
        var renewal = (await applications.FindAsync(appNo, ct))?.Renewal is not null;
        var (link, shortened) = await LinkAsync(appNo, renewal, ct);
        return (await applications.RecordPaymentLinkAsync(appNo, link, ct), shortened);
    }

    // The link for the application - a purchase's or a renewal's, each from its own
    // template - shortened when it can be. Whatever stops the shortening is logged,
    // and the long link goes instead; with no template set there is no link.
    private async Task<(PaymentLink? Link, bool Shortened)> LinkAsync(string appNo, bool renewal, CancellationToken ct)
    {
        if (paymentLink.Value.For(appNo, renewal) is not { } url) return (null, true);
        try
        {
            return (new PaymentLink(url, await shortLinks.ShortenAsync(url, ct)), true);
        }
        catch (ExternalServiceException e)
        {
            log.LogWarning(e, "Payment link for {AppNo} not shortened (trace {TraceId}): {Message}", appNo, e.TraceId, e.Message);
            return (new PaymentLink(url, null), false);
        }
    }
}
