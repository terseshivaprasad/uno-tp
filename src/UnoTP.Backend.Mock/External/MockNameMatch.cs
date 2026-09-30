using System.Text.RegularExpressions;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// Matches two names the way the pages did before the name match became an outside
/// call: the same words in any order match; every word of the shorter name found in
/// the longer, whole or as its initial, is a partial match; anything else a mismatch.
/// </summary>
public sealed class MockNameMatch : INameMatchService
{
    public Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default) =>
        Task.FromResult(new NameMatchResult(Compare(name, other)));

    /// <summary>match, partial or mismatch.</summary>
    public static string Compare(string read, string holder)
    {
        static string[] Words(string name) =>
            Regex.Replace(name.ToUpperInvariant(), "[^A-Z ]", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (a, b) = (Words(read), Words(holder));
        if (a.Length == 0 || b.Length == 0) return NameMatchOutcome.Mismatch;
        if (a.SequenceEqual(b) || a.Order().SequenceEqual(b.Order())) return NameMatchOutcome.Match;
        // Every word of the shorter name stands in the longer one, whole or as its
        // initial - KARAN D MEHTA against KARAN DEEPAK MEHTA.
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        var left = longer.ToList();
        foreach (var w in shorter)
        {
            var i = left.FindIndex(l => l == w || w.Length == 1 && l[0] == w[0] || l.Length == 1 && w[0] == l[0]);
            if (i < 0) return NameMatchOutcome.Mismatch;
            left.RemoveAt(i);
        }
        return shorter.Any(w => w.Length > 1) ? NameMatchOutcome.Partial : NameMatchOutcome.Mismatch;
    }
}
