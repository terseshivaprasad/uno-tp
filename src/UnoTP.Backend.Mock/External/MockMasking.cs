using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock masking check: a copy named as masked is masked, and "unmasked" is not.
/// The mock does not hold out for the holder's consent the way IDfy does.
/// </summary>
public sealed class MockMasking : IMaskingService
{
    public Task<bool> IsMaskedAsync(UploadFile file, bool consent, CancellationToken ct = default) =>
        Task.FromResult(MockScans.Named(file, "masked") && !MockScans.Named(file, "unmasked"));
}
