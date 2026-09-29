namespace UnoTP.Backend;

/// <summary>Where a PIN code is: the district and state an address typed by hand is shown with.</summary>
public interface IPlaceApi
{
    /// <summary>GET pincodes/{pin}: the district and state a 6-digit PIN code is in, or null (404) for none.</summary>
    Task<PinPlace?> PinCodeAsync(string pin, CancellationToken ct = default);
}

public sealed record PinPlace(string PinCode, string District, string State);
