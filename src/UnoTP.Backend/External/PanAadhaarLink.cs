using System.Text.Json.Serialization;

namespace UnoTP.Backend.External;

/// <summary>
/// The Income Tax Department's PAN-Aadhaar link: whether an Aadhaar is held
/// against a PAN. It is asked with both numbers, so it can only be asked once an
/// Aadhaar number has been read on the application. An unlinked PAN does not stop
/// an application - TDS runs at the higher rate until the investor links it.
/// </summary>
public interface IPanAadhaarLinkService
{
    /// <param name="aadhaarNumber">12 digits, or empty when none has been read yet.
    /// Anything that is not 12 digits cannot be asked with.</param>
    Task<PanAadhaarLink> CheckAsync(string pan, string aadhaarNumber, CancellationToken ct = default);
}

[JsonConverter(typeof(JsonStringEnumConverter<PanAadhaarLink>))]
public enum PanAadhaarLink
{
    Linked,
    NotLinked,

    /// <summary>Not asked: there is no Aadhaar number to ask with yet.</summary>
    NeedsAadhaar,
}

/// <summary>POST check { pan, aadhaarNumber } → { link }.</summary>
public sealed class PanAadhaarLinkClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "The PAN-Aadhaar link check"), IPanAadhaarLinkService
{
    public const string Name = "PanAadhaarLink";

    public async Task<PanAadhaarLink> CheckAsync(string pan, string aadhaarNumber, CancellationToken ct = default) =>
        !AadhaarNumbers.IsWhole(aadhaarNumber)
            ? PanAadhaarLink.NeedsAadhaar
            : (await Ask(() => Send<Answer>(HttpMethod.Post, "check", Body(new { pan, aadhaarNumber }), ct), ct)).Link;

    private sealed record Answer(PanAadhaarLink Link);
}

public static class AadhaarNumbers
{
    /// <summary>
    /// Whether a number is a whole Aadhaar number: 12 digits. A masked one, or the
    /// part of one a secure QR code carries, is not.
    /// </summary>
    public static bool IsWhole(string number) => number.Length == 12 && number.All(char.IsAsciiDigit);
}
