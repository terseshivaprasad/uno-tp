using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// One card of what a holder's copies were read to say, as the shared
/// Views/Shared/_ReadCards.cshtml draws it: its title, what it says, and for a PAN
/// NSDL has not verified, how NSDL is asked again without the copy being uploaded again.
/// </summary>
/// <param name="Id">What the card's element id ends with, where the page may be sent back to it.</param>
/// <param name="Error">What Proceed said this check still lacks, shown under the card; null when nothing.</param>
public sealed record ReadItem(string Kind, ReadCard Card, string? Id = null, NsdlRetry? Retry = null, string? Error = null)
{
    public static implicit operator ReadItem((string Kind, ReadCard Card) card) => new(card.Kind, card.Card);
}

/// <summary>
/// NSDL asked again about a PAN copy already filed. Where NSDL held the PAN against
/// another name, the name printed on the card is typed first; otherwise the same PAN,
/// date of birth and name are put to it again with one button.
/// </summary>
/// <param name="Field">The name field, which the row's ids are made from too.</param>
/// <param name="Value">The name last put to NSDL.</param>
/// <param name="Error">What was wrong with the last retry; null when nothing.</param>
/// <param name="TypesName">Whether the name is typed before NSDL is asked.</param>
/// <param name="Title">What the row says happened.</param>
/// <param name="Text">What the row says to do.</param>
public sealed record NsdlRetry(string Field, string Value, string? Error, bool TypesName, string Title, string Text);

/// <summary>
/// The read cards, and where an NSDL retry posts. The cards are drawn by
/// _ReadCards; the retry row by _NsdlRetry, which the page puts in its document
/// grid straight under the PAN's row.
/// </summary>
public sealed record ReadCardsBlock(IReadOnlyList<ReadItem> Items, string? RetryUrl = null)
{
    /// <summary>Whether NSDL can be asked again about the PAN copy filed.</summary>
    public bool AsksNsdl => RetryUrl is not null && Items.Any(i => i.Retry is not null);
}

/// <summary>One read card.</summary>
public sealed record ReadCardBlock(ReadItem Item);

/// <summary>
/// The row asking for a holder's 12-digit Aadhaar number, where OCR could not read
/// it off the Aadhaar filed: the field, where it posts, and what was wrong with the
/// number last typed. The number is never put back in the field.
/// </summary>
public sealed record AadhaarNumberBlock(string Field, string PostUrl, string? Error);
