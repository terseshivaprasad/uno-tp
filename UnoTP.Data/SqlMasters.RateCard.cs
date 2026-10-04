using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

public sealed partial class SqlMasters
{
    // ----- The rate card: t_FD_BOTC_SCHEME ---------------------------------------------
    //
    // The FD system's own rate card, in the main database. A row is one scheme code:
    // a category, mode (MODE_STATUS: a fresh application or a renewal), scheme, tenure
    // and interest frequency, for deposits from MINIMUM_AMOUNT to MAXIMUM_AMOUNT, in
    // effect from FROM_DATE to TO_DATE (no TO_DATE: still in effect).

    private sealed class SchemeRow
    {
        // The numbers are read through a cast: a row whose number does not read is left out.
        public const string Columns = """
            RTRIM(CATEGORY) AS Category, RTRIM(SCHEME) AS Scheme, RTRIM(SCHEME_CODE) AS SchemeCode, RTRIM(INTEREST_FREQ) AS InterestFreq,
            TRY_CAST(PERIOD AS INT) AS TenureMonths, TRY_CAST(INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
            CAST(TRY_CAST(MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
            CAST(TRY_CAST(MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount, FROM_DATE AS FromDate
            """;

        public string? Category { get; set; }
        public string? Scheme { get; set; }
        public string? SchemeCode { get; set; }
        public string? InterestFreq { get; set; }
        public int? TenureMonths { get; set; }
        public decimal? Rate { get; set; }
        public long? MinAmount { get; set; }
        public long? MaxAmount { get; set; }
        public DateTime? FromDate { get; set; }
    }

    /// <summary>
    /// The rate card in effect today, for the deposit's category
    /// (or, failing rows of its own, defaultRateCategory's) and the application's mode:
    /// one row per tenure, payout and minimum amount, the latest in effect. Only the
    /// rows of a scheme and interest frequency the payouts list names are offered.
    /// Rows come in the order the tenures and payouts are listed. A renewal is read
    /// off today's card as well, not the card of the day its deposit matures.
    /// </summary>
    public async Task<IReadOnlyList<RateOption>> RatesAsync(RatesRequest request, CancellationToken ct = default)
    {
        var lists = await reference.ReferenceAsync(ct);
        var category = Sections.RateCategoryOf(request.Category, lists.Categories);
        var fallback = Sections.RateCategoryOf(await reference.SettingAsync("defaultRateCategory", ct), lists.Categories);

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<SchemeRow>($"""
            SELECT {SchemeRow.Columns}
            FROM dbo.t_FD_BOTC_SCHEME
            WHERE CATEGORY IN (@Category, @Fallback) AND MODE_STATUS = @Mode
              AND FROM_DATE <= @Today AND (TO_DATE IS NULL OR TO_DATE >= @Today)
            ORDER BY CASE WHEN CATEGORY = @Category THEN 0 ELSE 1 END, FROM_DATE DESC, SCHEME_ID DESC
            """, new { Category = category, Fallback = fallback, Mode = request.ApplicationType, DateTime.Today });

        // The rows come the category's own first and the latest first, so the first
        // row seen for a tenure, payout and minimum amount is the one that stands.
        var seen = new HashSet<(int, string, long)>();
        var lines = new List<RateOption>();
        foreach (var row in rows)
        {
            if (row.TenureMonths is not { } tenure || row.Rate is not { } rate || row.MinAmount is not { } minAmount || row.FromDate is not { } from) continue;
            var payout = PayoutOf(row, lists.Payouts);
            if (payout is null) continue;
            if (!seen.Add((tenure, payout.Code, minAmount))) continue;
            lines.Add(new RateOption(tenure, row.Scheme ?? "", payout.Code, rate, minAmount, row.MaxAmount, DateOnly.FromDateTime(from), row.SchemeCode ?? ""));
        }

        // In the order the page lists the tenures and payouts.
        var ordered = new List<RateOption>();
        foreach (var tenure in lists.Tenures)
        {
            foreach (var payout in lists.Payouts)
            {
                foreach (var line in lines)
                {
                    if (line.TenureMonths == tenure && line.Payout == payout.Code) ordered.Add(line);
                }
            }
        }
        return ordered;
    }

    // The payout a row of the card is: the one the payouts list files under the row's
    // interest frequency and, where the list names one, its scheme. Null for a row
    // of a scheme the app does not offer.
    private static PayoutOption? PayoutOf(SchemeRow row, IReadOnlyList<PayoutOption> payouts)
    {
        foreach (var payout in payouts)
        {
            if (payout.InterestFreq.Length == 0) continue;
            if (!SameName(payout.InterestFreq, row.InterestFreq)) continue;
            if (payout.Scheme.Length > 0 && !SameName(payout.Scheme, row.Scheme)) continue;
            return payout;
        }
        return null;
    }

    private static bool SameName(string listed, string? onCard) =>
        string.Equals(listed.Trim(), (onCard ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The card rate for the deposit as it stands and what it comes to. A cumulative
    /// deposit compounds compoundingPerYear times a year (DepositMaths); one that pays
    /// out pays simple interest each period. It matures its tenure after the day it
    /// starts: today, or for a renewal the day the old deposit matures.
    /// </summary>
    public async Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var starts = request.Card.StartsOn ?? today;
        var compounding = await reference.NumberAsync("compoundingPerYear", ct);
        var payout = (await reference.ReferenceAsync(ct)).Payouts.FirstOrDefault(p => p.Code == request.Payout)
            ?? throw new ArgumentException($"No payout called {request.Payout}.");

        var card = await RatesAsync(request.Card, ct);
        var line = RateCard.Line(card, request.TenureMonths, request.Payout, request.Amount);
        if (line is null) throw new ArgumentException($"The rate card offers no {payout.Name} payout for {request.TenureMonths} months on a deposit of {request.Amount} on {today:dd-MM-yyyy}.");

        decimal amount = request.Amount;
        var maturity = amount;
        if (payout.PerYear == 0) maturity = DepositMaths.MaturityAmount(amount, line.Rate, request.TenureMonths, compounding);
        var each = DepositMaths.InterestEach(amount, line.Rate, payout.PerYear);
        return new DepositQuote(line.Rate, each, maturity, starts.AddMonths(request.TenureMonths), line.AsOn);
    }
}
