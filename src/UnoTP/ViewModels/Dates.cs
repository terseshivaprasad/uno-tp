namespace UnoTP.ViewModels;

/// <summary>
/// Dates as the screens show them: dd/MM/yyyy, as they are typed into the three
/// date boxes. The backend keeps and answers a date as dd-MM-yyyy, which stays
/// what the app sends and compares; only what is written on the page changes.
/// </summary>
public static class Dates
{
    /// <summary>A dd-MM-yyyy date shown as dd/MM/yyyy; anything else - empty, masked, a year - as it is.</summary>
    public static string Show(string? date) =>
        date is { Length: 10 } d && d[2] == '-' && d[5] == '-' ? d.Replace('-', '/') : date ?? "";

    /// <summary>How long ago, the way a list says it: just now, 5 min ago, 3 h ago, yesterday, 4 days ago, 12 Sep.</summary>
    public static string Ago(DateTime at, DateTime? now = null)
    {
        var span = (now ?? DateTime.Now) - at;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
            : span.TotalDays < 2 ? "yesterday"
            : span.TotalDays < 7 ? $"{(int)span.TotalDays} days ago"
            : at.ToString("d MMM");
    }
}
