using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// The partner's drafts as Views/Shared/_Drafts.cshtml lists them, on Investor
/// Identification and on the dashboard alike: those shown, and whether Continue
/// posts through partial-forms.js (the page loads it).
/// </summary>
public sealed record DraftsBlock(IReadOnlyList<DraftSummary> Shown, bool Partial)
{
    /// <summary>Rows drawn open; any more wait behind "Show more", so a long list does not bury the page.</summary>
    public const int Open = 10;

    /// <summary>The initials an avatar shows: the first letter of the first two words of a masked name.</summary>
    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
}
