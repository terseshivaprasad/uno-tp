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

    /// <summary>
    /// Whether a number typed by hand can be an Aadhaar number at all: 12 digits,
    /// not starting with 0 or 1, and its last digit the Verhoeff check digit of the
    /// rest - as UIDAI issues them - so a slip of the finger is caught here rather
    /// than asked about.
    /// </summary>
    public static bool IsValid(string number)
    {
        if (!IsWhole(number) || number[0] is '0' or '1') return false;
        var check = 0;
        for (var i = 0; i < number.Length; i++)
            check = Multiply[check, Permute[i % 8, number[number.Length - 1 - i] - '0']];
        return check == 0;
    }

    // The Verhoeff tables: the dihedral group D5 and its permutation.
    private static readonly int[,] Multiply =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 }, { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 }, { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 }, { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 }, { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 }, { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
    };

    private static readonly int[,] Permute =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 }, { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 }, { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 }, { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 }, { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
    };
}
