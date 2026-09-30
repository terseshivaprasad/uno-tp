using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>Hands the copy back as it is: the mock cannot redraw an image, and no test looks inside one.</summary>
public sealed class MockMasking : IMaskingService
{
    public Task<UploadFile> MaskAsync(UploadFile file, bool consent, CancellationToken ct = default) =>
        Task.FromResult(file);
}
