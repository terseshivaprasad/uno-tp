using System.Net.Http.Headers;

namespace UnoTP.Backend.External;

/// <summary>
/// Face match: whether the photograph on a holder's PAN copy is the same person as
/// the one on their proof of address. For now its answer is shown, not acted on:
/// a copy is filed whatever it says.
/// </summary>
public interface IFaceMatchService
{
    Task<FaceMatch> CompareAsync(UploadFile panCopy, UploadFile proof, CancellationToken ct = default);
}

/// <param name="Matched">Whether the two faces are the same person.</param>
/// <param name="Score">How alike they are, 0 to 100.</param>
/// <param name="Unsure">Set when no answer either way could be given - no face found, or too close to call - and why.</param>
public sealed record FaceMatch(bool Matched, int Score, string? Unsure = null);

/// <summary>POST compare (multipart: pan, proof) → { matched, score, unsure }.</summary>
public sealed class FaceMatchClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Face match"), IFaceMatchService
{
    public const string Name = "FaceMatch";

    public Task<FaceMatch> CompareAsync(UploadFile panCopy, UploadFile proof, CancellationToken ct = default) =>
        Ask(() => Send<FaceMatch>(HttpMethod.Post, "compare", Files(("pan", panCopy), ("proof", proof)), ct), ct);

    private static MultipartFormDataContent Files(params (string Name, UploadFile File)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, file) in files)
        {
            var bytes = new ByteArrayContent(file.Bytes);
            bytes.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
                ? type
                : new MediaTypeHeaderValue("application/octet-stream");
            form.Add(bytes, name, file.FileName);
        }
        return form;
    }
}
