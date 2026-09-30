using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>
/// One card of what a holder's copies were read to say, as the shared
/// Views/Shared/_ReadCards.cshtml draws it: its title, what it says, and for NSDL
/// holding a PAN against another name, where the printed name is typed to ask again.
/// </summary>
/// <param name="Id">What the card's element id ends with, where the page may be sent back to it.</param>
/// <param name="Error">What Proceed said this check still lacks, shown under the card; null when nothing.</param>
public sealed record ReadItem(string Kind, ReadCard Card, string? Id = null, NameRetry? Retry = null, string? Error = null)
{
    public static implicit operator ReadItem((string Kind, ReadCard Card) card) => new(card.Kind, card.Card);
}

/// <summary>The name typed from the PAN card for NSDL: the field, what it holds, and what was wrong with it.</summary>
public sealed record NameRetry(string Field, string Value, string? Error);

/// <summary>
/// The read cards, and where a name typed for NSDL posts. The cards are drawn by
/// _ReadCards; the row asking for the name by _NsdlRetry, which the page puts in
/// its document grid straight under the PAN's row.
/// </summary>
public sealed record ReadCardsBlock(IReadOnlyList<ReadItem> Items, string? RetryUrl = null)
{
    /// <summary>Whether NSDL is asking for the name printed on the PAN.</summary>
    public bool AsksName => RetryUrl is not null && Items.Any(i => i.Retry is not null);
}

/// <summary>One read card.</summary>
public sealed record ReadCardBlock(ReadItem Item);

/// <summary>
/// The row asking for a holder's 12-digit Aadhaar number, where OCR could not read
/// it off the Aadhaar filed: the field, where it posts, and what was wrong with the
/// number last typed. The number is never put back in the field.
/// </summary>
public sealed record AadhaarNumberBlock(string Field, string PostUrl, string? Error);
