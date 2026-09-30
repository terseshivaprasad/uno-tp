namespace UnoTP.Backend.Mock;

/// <summary>
/// Where Renew FD opens its application: in memory here, in the database behind
/// UnoTP.Api. What comes over from the deposit is on it from the start.
/// </summary>
public interface IRenewalOpener
{
    /// <summary>A renewal's application: opened as any other, with the deposit and what comes over from it already on it.</summary>
    Task<Application> OpenRenewalAsync(Holder holder, RenewalOf renewal, UploadState upload, PaymentDetails payment, DepositDetails deposit);
}
