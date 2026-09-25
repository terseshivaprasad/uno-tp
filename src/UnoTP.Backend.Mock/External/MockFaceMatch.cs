using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock face match. It cannot see a face, so it goes by the proof's file name:
/// one named "otherface" is someone else, one named "noface" shows no face, and
/// anything else is the same person.
/// </summary>
public sealed class MockFaceMatch : IFaceMatchService
{
    public Task<FaceMatch> CompareAsync(UploadFile panCopy, UploadFile proof, CancellationToken ct = default) =>
        Task.FromResult(
            MockScans.Named(proof, "noface") ? new FaceMatch(false, 0, "no face could be found on the proof")
            : MockScans.Named(proof, "otherface") ? new FaceMatch(false, 31)
            : new FaceMatch(true, 94));
}
