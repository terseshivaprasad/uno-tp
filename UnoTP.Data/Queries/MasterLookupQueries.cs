namespace UnoTP.Data;

/// <summary>
/// What the app asks of the master queries in MasterQueries.cs: one row by its key, or
/// what a typed search finds. The masters' own tables and columns are in
/// MasterQueries.cs; here is only how its queries are asked. Nothing but SQL is in this file.
/// </summary>
internal static class MasterLookupQueries
{
    /// <summary>
    /// SQL Server is told to plan the read for this call's own values. A register's query takes
    /// both its key and @Search, and the app gives one and leaves the other empty: so the query
    /// is really one of two different reads - a row by its key, found by the index, or a search -
    /// and a plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string ForThisCall = "OPTION (RECOMPILE)";

    /// <summary>
    /// A PAN's active deposits, each with its amount.
    /// </summary>
    public const string ActiveDeposits = $"SELECT d.Number, d.Amount FROM ({MasterQueries.ActiveDeposits}) d";

    /// <summary>
    /// The one row a code names on a register. parties is the register's query.
    /// </summary>
    public static string Party(string parties) =>
        $"SELECT TOP (1) p.Code, p.Name FROM ({parties}) p {ForThisCall}";

    /// <summary>
    /// The parties whose code and name hold what was typed; a code that starts with the first word
    /// comes first. At most found of them. parties is the register's query.
    /// </summary>
    public static string SearchParties(int found, string parties) =>
        $"""
        SELECT TOP ({found}) p.Code, p.Name FROM ({parties}) p
        ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name {ForThisCall}
        """;

    /// <summary>
    /// The bank branch an IFSC names.
    /// </summary>
    public const string BankBranch = $"SELECT TOP (1) b.Ifsc, b.Bank, b.Branch, b.Micr FROM ({MasterQueries.BankBranches}) b {ForThisCall}";

    /// <summary>
    /// The bank branches a search finds, by MICR code. At most found of them.
    /// </summary>
    public static string SearchBankBranches(int found) =>
        $"""
        SELECT TOP ({found}) b.Ifsc, b.Bank, b.Branch, b.Micr FROM ({MasterQueries.BankBranches}) b
        ORDER BY b.Micr {ForThisCall}
        """;

    /// <summary>
    /// The Axis CMS locations a search finds, by state and district. At most found of them.
    /// </summary>
    public static string SearchCmsLocations(int found) =>
        $"""
        SELECT TOP ({found}) c.Code, c.Name, c.Label FROM ({MasterQueries.CmsLocations}) c
        ORDER BY c.State, c.District {ForThisCall}
        """;

    /// <summary>
    /// The Axis CMS location a code names.
    /// </summary>
    public const string CmsLocation = $"SELECT TOP (1) c.Code, c.Name, c.Label FROM ({MasterQueries.CmsLocations}) c {ForThisCall}";

    /// <summary>
    /// The rate card's rows in effect for a category (and the fallback category). rateCard is the
    /// branch user's or the partner's card.
    /// </summary>
    public static string Rates(string rateCard) =>
        $"""
        SELECT * FROM ({rateCard}) r
        WHERE r.Category IN (@Category, @Fallback) AND r.Mode = @Mode
          AND r.FromDate <= @Today
        ORDER BY CASE WHEN r.Category = @Category THEN 0 ELSE 1 END, r.FromDate DESC, r.SchemeId DESC
        """;

    /// <summary>
    /// How many rows the rate card has for a deposit as configured: none means it is not offered.
    /// </summary>
    public static string OnRateCard(string rateCard) =>
        $"""
        SELECT COUNT(*) FROM ({rateCard}) r
        WHERE r.Category = @Category AND r.Mode = @Mode
          AND r.Scheme = @Scheme AND r.InterestFreq = @InterestFreq
          AND r.TenureMonths = @TenureMonths AND r.Rate = @Rate
          AND r.MinAmount <= @Amount AND (r.MaxAmount IS NULL OR r.MaxAmount >= @Amount)
          AND r.FromDate <= @Today
        """;
}
