using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>
/// The partner's drafts as Views/Shared/_Drafts.cshtml lists them, on Investor
/// Identification and on the dashboard alike: those shown, how many there are in
/// all, and whether Continue posts through partial-forms.js (the page loads it).
/// </summary>
public sealed record DraftsBlock(IReadOnlyList<DraftSummary> Shown, int Total, bool Partial);
