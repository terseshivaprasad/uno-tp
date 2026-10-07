namespace UnoTP.ViewModels;

/// <summary>
/// A status's tone - the text colour class a view model gives it (text-success, text-amber,
/// text-danger, text-muted) - as a status word: the word in the tone's colour, no box round it.
/// </summary>
public static class Tones
{
    public static string Word(string? tone) => "status-word " + (tone switch
    {
        "text-success" => "text-success-emphasis",
        "text-amber" or "text-warning" => "text-warning-emphasis",
        "text-danger" => "text-danger-emphasis",
        "text-blue" or "text-info" => "text-info-emphasis",
        _ => "text-secondary-emphasis",
    });
}
