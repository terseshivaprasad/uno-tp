using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// One card of what a holder's copies were read to say, as the shared
/// Views/Shared/_ReadCards.cshtml draws it: its title, what it says, and for a PAN
/// NSDL holds against another name, how NSDL is asked again without the copy being uploaded again.
/// </summary>
/// <param name="Id">What the card's element id ends with, where the page may be sent back to it.</param>
/// <param name="Error">What Proceed said this check still lacks, shown under the card; null when nothing.</param>
public sealed record ReadItem(string Kind, ReadCard Card, string? Id = null, NsdlRetry? Retry = null, string? Error = null)
{
    public static implicit operator ReadItem((string Kind, ReadCard Card) card) => new(card.Kind, card.Card);
}

/// <summary>
/// NSDL asked again about a PAN copy already filed, where NSDL held the PAN against
/// another name: the name printed on the card is typed and put to it. Any other
/// NSDL failure has no retry here - the PAN copy is uploaded again.
/// </summary>
/// <param name="Field">The name field, which the row's ids are made from too.</param>
/// <param name="Value">The name last put to NSDL.</param>
/// <param name="Error">What was wrong with the last retry; null when nothing.</param>
/// <param name="Title">What the row says happened.</param>
/// <param name="Text">What the row says to do.</param>
public sealed record NsdlRetry(string Field, string Value, string? Error, string Title, string Text);

/// <summary>
/// The read cards, and where an NSDL retry posts. The cards are drawn by
/// _ReadCards; the retry row by _NsdlRetry, which the page puts in its document
/// grid straight under the PAN's row.
/// </summary>
public sealed record ReadCardsBlock(IReadOnlyList<ReadItem> Items, string? RetryUrl = null)
{
    /// <summary>Whether NSDL can be asked again about the PAN copy filed.</summary>
    public bool AsksNsdl => RetryUrl is not null && Items.Any(i => i.Retry is not null);

    /// <summary>How many of the checks are done, as a phone says it on the folded section: "3 of 4 done".</summary>
    public string DoneCount => $"{Items.Count(i => i.Card.Kind == "is-done")} of {Items.Count} done";
}

/// <summary>One read card.</summary>
public sealed record ReadCardBlock(ReadItem Item);

/// <summary>
/// The row asking for a holder's Aadhaar number, where OCR read only its last four
/// digits off the Aadhaar filed: the field for the first eight, the last four they
/// are typed against (empty where none were read, and the whole number is typed),
/// where it posts, and what was wrong with the number last typed. What is typed is
/// never put back in the field.
/// </summary>
public sealed record AadhaarNumberBlock(string Field, string LastFour, string PostUrl, string? Error);
