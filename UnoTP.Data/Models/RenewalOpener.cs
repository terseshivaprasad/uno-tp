namespace UnoTP.Models;

/// <summary>
/// Where Renew FD opens its application: whatever keeps the applications answers
/// this, and whatever answers <see cref="IRenewalApi"/> calls it. What comes over
/// from the deposit is on the application from the start.
/// </summary>
public interface IRenewalOpener
{
    /// <summary>A renewal's application: opened as any other, with the deposit and what comes over from it already on it.</summary>
    Task<Application> OpenRenewalAsync(Holder holder, RenewalOf renewal, UploadState upload, PaymentDetails payment, DepositDetails deposit);

    /// <summary>Cancels the partner's renewal application for the deposit, while it is not yet submitted; false when there is none.</summary>
    Task<bool> CancelRenewalAsync(string depositNumber);
}
