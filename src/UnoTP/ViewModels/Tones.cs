namespace UnoTP.ViewModels;

/// <summary>
/// A status's tone - the text colour class a view model gives it (text-success, text-amber,
/// text-danger, text-muted) - as a Bootstrap badge: the tone's tint behind its own colour.
/// </summary>
public static class Tones
{
    public static string Badge(string? tone) => "badge " + (tone switch
    {
        "text-success" => "bg-success-subtle text-success-emphasis",
        "text-amber" or "text-warning" => "bg-warning-subtle text-warning-emphasis",
        "text-danger" => "bg-danger-subtle text-danger-emphasis",
        "text-blue" or "text-info" => "bg-info-subtle text-info-emphasis",
        _ => "bg-secondary-subtle text-secondary-emphasis",
    });
}
