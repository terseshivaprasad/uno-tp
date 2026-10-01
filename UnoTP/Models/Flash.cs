namespace UnoTP.Models;

/// <summary>
/// What one post leaves for the page after it: errors by field or slot, and a line
/// to say. Kept in the session between the post and the page it redirects to, and
/// said once.
/// </summary>
public sealed class Flash
{
    public Dictionary<string, string> Errors { get; init; } = [];

    /// <summary>The history entry a slot's refusal points back to.</summary>
    public Dictionary<string, string> ErrorLog { get; init; } = [];

    public string? Banner { get; set; }

    /// <summary>The field that takes the caret: the first one with something wrong.</summary>
    public string? Focus { get; set; }
}
