namespace UnoTP.Api;

/// <summary>How the backend masks an investor's identifiers wherever a list shows them.</summary>
internal static class Masks
{
    /// <summary>9876543210 as ••••••3210.</summary>
    public static string Mobile(string mobile) =>
        mobile.Length >= 4 ? new string('•', mobile.Length - 4) + mobile[^4..] : mobile;

    /// <summary>RAHUL S TERSE as R•••• S T••••.</summary>
    public static string Name(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length <= 1 ? w : w[0] + new string('•', w.Length - 1)));

    /// <summary>ABCPT1234Q as ABCPT••••Q.</summary>
    public static string Pan(string pan) =>
        pan.Length == 10 ? pan[..5] + "••••" + pan[^1] : pan;

    /// <summary>14-08-1988 as ••/••/1988.</summary>
    public static string Dob(string dob) =>
        dob.Length >= 4 ? "••/••/" + dob[^4..] : dob;

    /// <summary>investor@example.com as in••••@example.com.</summary>
    public static string Email(string email) =>
        email.IndexOf('@') is > 0 and var at ? email[..Math.Min(2, at)].ToLowerInvariant() + "••••" + email[at..].ToLowerInvariant() : "";
}
