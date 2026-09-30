namespace UnoTP.ViewModels;

/// <summary>
/// The application section by section (Views/Shared/_ApplicationSummary.cshtml): Review
/// Summary's cards, each with its Edit, or - read-only - View Application's details sheet.
/// </summary>
public sealed record ApplicationSummary(ReviewViewModel Review, bool Editable);
