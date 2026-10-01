namespace UnoTP.ViewModels;

/// <summary>
/// What typed text may carry, on every page: letters, digits, spaces, and the
/// punctuation an address or a name needs - hyphen, comma, ampersand, slash and
/// full stop. Nothing else, on the server and in the browser alike
/// (field-checks.js keeps the same set). An address line is at most 40 characters.
/// </summary>
public static class InputRules
{
    /// <summary>The punctuation allowed beside letters, digits and spaces.</summary>
    public const string AllowedPunctuation = "-,&/.";

    /// <summary>What a field may run to: 50 characters, an address line 40, a remark 200.</summary>
    public const int MaxField = 50;
    public const int MaxAddressLine = 40;
    public const int MaxRemark = 200;

    public const string OnlyAllowed = "Only letters, digits, spaces and - , & / . are allowed";

    public static string TooLong(int max) => $"At most {max} characters";

    /// <summary>Whether every character of the text is allowed.</summary>
    public static bool IsClean(string text)
    {
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) continue;
            if (char.IsWhiteSpace(c)) continue;
            if (AllowedPunctuation.Contains(c)) continue;
            return false;
        }
        return true;
    }

    /// <summary>A name as printed: letters and spaces only, starting with a letter.</summary>
    public static bool IsName(string text)
    {
        if (text.Length == 0) return false;
        if (!char.IsLetter(text[0])) return false;
        foreach (var c in text)
        {
            if (char.IsLetter(c)) continue;
            if (c == ' ') continue;
            return false;
        }
        return true;
    }

    public const string LettersOnly = "Enter letters only";
}
