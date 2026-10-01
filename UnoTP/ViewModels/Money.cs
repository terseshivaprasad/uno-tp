using System.Globalization;

namespace UnoTP.ViewModels;

/// <summary>Where a link went: by SMS and by e-mail, as the backend masked them.</summary>
public static class SentTo
{
    /// <summary>"98••••1000 and ab••••@gmail.com", or the one there is.</summary>
    public static string Both(string mobile, string email) =>
        string.Join(" and ", new[] { mobile, email }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Rupees as the old screens write them: Indian grouping, and in words.</summary>
public static class Money
{
    /// <summary>"₹ 5,00,000".</summary>
    public static string Rupees(decimal amount) => "₹ " + Group((long)Math.Round(amount, MidpointRounding.AwayFromZero));

    /// <summary>Lakh and crore grouping: 20000000 as "2,00,00,000".</summary>
    public static string Group(long n)
    {
        var digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= 3) return (n < 0 ? "-" : "") + digits;
        var head = digits[..^3];
        var groups = new List<string>();
        while (head.Length > 2) { groups.Insert(0, head[^2..]); head = head[..^2]; }
        if (head.Length > 0) groups.Insert(0, head);
        return (n < 0 ? "-" : "") + string.Join(",", groups) + "," + digits[^3..];
    }

    private static readonly string[] Ones =
    [
        "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    private static readonly string[] Tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private static string Below100(long n) => n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 > 0 ? "-" + Ones[n % 10] : "");

    /// <summary>"Five lakh rupees".</summary>
    public static string InWords(long n)
    {
        if (n <= 0) return "";
        var parts = new List<string>();
        foreach (var (unit, name) in new[] { (10_000_000L, "crore"), (100_000L, "lakh"), (1_000L, "thousand"), (100L, "hundred") })
        {
            var q = n / unit;
            if (q > 0) { parts.Add((q >= 100 ? InWords(q).Replace(" rupees", "").ToLowerInvariant() : Below100(q)) + " " + name); n %= unit; }
        }
        if (n > 0) parts.Add(Below100(n));
        var s = string.Join(" ", parts);
        return char.ToUpperInvariant(s[0]) + s[1..] + " rupees";
    }

    /// <summary>An account number as it is shown: all but the last four digits hidden.</summary>
    public static string MaskAccount(string account) =>
        account.Length > 4 ? new string('•', Math.Min(8, account.Length - 4)) + account[^4..] : account;

    /// <summary>A day as the pages write it: "14 Sep 2029".</summary>
    public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A moment as the pages write it: "14 Sep 2029, 3:05 PM".</summary>
    public static string Day(DateTime at) => at.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);
}
