using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock;

/// <summary>Allows every holder to invest online, except the test PAN the register lists as not allowed.</summary>
public sealed class MockNameScreening : INameScreeningService
{
    /// <summary>The test PAN name screening does not allow (MockInvestors.Pans).</summary>
    public const string NotAllowedPan = "XXXXK1011K";

    public Task<NameScreeningResult> ScreenAsync(NameScreeningRequest request, CancellationToken ct = default)
    {
        var allowed = !string.Equals(request.Pan.Trim(), NotAllowedPan, StringComparison.OrdinalIgnoreCase);
        var reference = "NS-" + MockInvestors.Seed(request.Pan + request.Name).ToString("D6");
        return Task.FromResult(new NameScreeningResult(allowed, reference));
    }
}
