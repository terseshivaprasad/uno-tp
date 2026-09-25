using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>
/// One card of what a holder's copies were read to say, as the shared
/// Views/Shared/_ReadCards.cshtml draws it: its title, what it says, and for NSDL
/// holding a PAN against another name, where the printed name is typed to ask again.
/// </summary>
/// <param name="Id">What the card's element id ends with, where the page may be sent back to it.</param>
public sealed record ReadItem(string Kind, ReadCard Card, string? Id = null, NameRetry? Retry = null)
{
    public static implicit operator ReadItem((string Kind, ReadCard Card) card) => new(card.Kind, card.Card);
}

/// <summary>The name typed from the PAN card for NSDL: the field, what it holds, and what was wrong with it.</summary>
public sealed record NameRetry(string Field, string Value, string? Error);

/// <summary>The read cards, and where a name typed for NSDL posts.</summary>
public sealed record ReadCardsBlock(IReadOnlyList<ReadItem> Items, string? RetryUrl = null);

/// <summary>One read card.</summary>
public sealed record ReadCardBlock(ReadItem Item);
