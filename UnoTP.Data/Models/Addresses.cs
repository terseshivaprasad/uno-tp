using System.Text.RegularExpressions;

namespace UnoTP.Models;

/// <summary>An address kept as one line, and the PIN code it ends with.</summary>
public static partial class Addresses
{
    /// <summary>
    /// An address and its PIN code, apart: the last six digits standing on their
    /// own in it ("411 001" too). Empty PIN when none can be found.
    /// </summary>
    public static (string Body, string Pin) SplitPin(string address)
    {
        var found = SixDigits().Matches(address);
        if (found.Count == 0) return (address, "");
        var last = found[^1];
        var body = (address[..last.Index] + address[(last.Index + last.Length)..]).Trim().TrimEnd(',', '-', ' ');
        return (body, last.Groups[1].Value + last.Groups[2].Value);
    }

    [GeneratedRegex(@"(?<!\d)(\d{3})\s?(\d{3})(?!\d)")]
    private static partial Regex SixDigits();
}
