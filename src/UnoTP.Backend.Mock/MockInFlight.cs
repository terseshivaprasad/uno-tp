namespace UnoTP.Backend.Mock;

/// <summary>
/// The applications in flight, which Investor Identification lists as drafts and
/// Short URL raises links against.
/// </summary>
internal static class MockInFlight
{
    /// <param name="StatusKey">needs-you while the application is the partner's to
    /// finish; operations or realisation once it has left their hands.</param>
    public sealed record InFlight(string AppNo, string HolderMask, long Amount, string StatusKey);

    public static readonly InFlight[] Applications =
    {
        new("FBBMFL26F71CD4", "D•••• K•••• +1", 1_50_000, "needs-you"),
        new("FBBMFL26F03BBD2", "A•••• D•••••• +1", 3_00_000, "needs-you"),
        new("FBBMFL26F55QP9", "M•••• S•••", 10_00_000, "needs-you"),
        new("FBBMFL26F62LK3", "V•••• J•••• +2", 2_00_000, "needs-you"),
        new("FBBMFL26F48TR1", "S•••• B••••", 5_00_000, "needs-you"),
        new("FBBMFL26F99AAC1", "R•••••• T••••• +2", 5_00_000, "operations"),
        new("FBBMFL26E88XZ7", "K•••• P••••", 7_50_000, "realisation"),
    };

    // The holders behind the drafts, written to fit the initials and lengths the
    // console masks them to.
    private static readonly Dictionary<string, string> DraftHolders = new()
    {
        ["FBBMFL26F71CD4"] = "DEEPA KAMAT",
        ["FBBMFL26F03BBD2"] = "ARJUN DAMODAR",
        ["FBBMFL26F55QP9"] = "MEERA SHAH",
        ["FBBMFL26F62LK3"] = "VIKAS JOSHI",
        ["FBBMFL26F48TR1"] = "SUNIL BORSE",
    };

    /// <summary>
    /// The in-flight rows still the partner's to finish, masked for the list and in
    /// full for the application behind each. A draft has no PAN or date of birth of
    /// its own in this mock, so both are derived from the application number and
    /// stay the same from one load to the next.
    /// </summary>
    public static IEnumerable<(DraftSummary Summary, Holder Holder)> Drafts =>
        Applications
            .Where(a => a.StatusKey == "needs-you")
            .Take(5)
            .Select(a =>
            {
                var seed = a.AppNo.Sum(c => c);
                var head = $"{(char)('A' + seed % 20)}{(char)('B' + seed * 3 % 22)}{(char)('C' + seed * 7 % 23)}P{(char)('B' + seed * 5 % 22)}";
                var tail = (char)('A' + seed * 11 % 26);
                var year = 1962 + seed % 34;
                return (
                    new DraftSummary(a.AppNo, a.HolderMask, $"{head}••••{tail}", $"••/••/{year}", a.Amount),
                    new Holder($"{head}{1000 + seed % 9000:0000}{tail}", $"14-08-{year}",
                        DraftHolders.GetValueOrDefault(a.AppNo, a.HolderMask), "", false));
            });
}
