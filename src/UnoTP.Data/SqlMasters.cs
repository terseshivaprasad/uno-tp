using System.Globalization;
using Dapper;
using UnoTP.Backend;

namespace UnoTP.Data;

/// <summary>
/// The masters the purchase journey reads (db/004): the investors on record, the
/// sourcing registers, bank branches, PIN codes and the rate card.
/// </summary>
public sealed class SqlMasters(Db db, SqlReference reference) : IInvestorApi, ISourcingApi, IDepositApi, IPlaceApi
{
    private const int Found = 20;

    // ----- Investors ---------------------------------------------------------------

    private const string FolioColumns = """
        c_Pan AS Pan, d_Dob AS Dob, c_Folio AS Folio, c_Name AS Name, c_Gender AS Gender, c_Address AS Address,
        f_Doc_Pan AS DocPan, f_Doc_Photo AS DocPhoto, f_Doc_Poa AS DocPoa, c_Note AS Note
        """;

    private sealed class FolioRow
    {
        public string Pan { get; set; } = "";
        public DateTime? Dob { get; set; }
        public string Folio { get; set; } = "";
        public string Name { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Address { get; set; } = "";
        public bool DocPan { get; set; }
        public bool DocPhoto { get; set; }
        public bool DocPoa { get; set; }
        public string Note { get; set; } = "";

        public FolioRecord Record() =>
            new(Pan, Dates.FromDb(Dob), Folio, Name, Gender, Address, new DocsOnRecord(DocPan, DocPhoto, DocPoa), Note);
    }

    // Every folio held against the PAN: more than one is a record Operations has to merge.
    public async Task<IReadOnlyList<FolioRecord>> FoliosByPanAsync(string pan, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<FolioRow>(
            $"SELECT {FolioColumns} FROM dbo.t_Unotp_Investor_Folio WHERE c_Pan = @Pan AND f_Active = 1 ORDER BY c_Folio",
            new { Pan = pan.Trim().ToUpperInvariant() })).Select(f => f.Record()).ToList();
    }

    public async Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QuerySingleOrDefaultAsync<FolioRow>(
            $"SELECT {FolioColumns} FROM dbo.t_Unotp_Investor_Folio WHERE c_Folio = @Folio AND f_Active = 1",
            new { Folio = folio.Trim().ToUpperInvariant() }))?.Record();
    }

    // ----- Sourcing registers ------------------------------------------------------

    public Task<IReadOnlyList<Party>> BrokersAsync(CancellationToken ct = default) => PartiesAsync("t_Unotp_Broker_Mst", ct);

    public Task<IReadOnlyList<Party>> StaffAsync(CancellationToken ct = default) => PartiesAsync("t_Unotp_Staff_Mst", ct);

    public Task<IReadOnlyList<Party>> SearchBrokersAsync(string query, CancellationToken ct = default) => SearchPartiesAsync("t_Unotp_Broker_Mst", query, ct);

    public Task<IReadOnlyList<Party>> SearchStaffAsync(string query, CancellationToken ct = default) => SearchPartiesAsync("t_Unotp_Staff_Mst", query, ct);

    private async Task<IReadOnlyList<Party>> PartiesAsync(string table, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<Party>($"SELECT c_Code AS Code, c_Name AS Name FROM dbo.{table} WHERE f_Active = 1 ORDER BY c_Name")).ToList();
    }

    // Every word typed must be in the code or the name; a code that starts with the text comes first.
    private async Task<IReadOnlyList<Party>> SearchPartiesAsync(string table, string query, CancellationToken ct)
    {
        var words = Words(query, ' ');
        if (words.Length == 0) return [];
        var (where, args) = AllWords(words, "c_Code + ' ' + c_Name");
        args.Add("First", EscapeLikePattern(words[0]) + "%");
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<Party>($"""
            SELECT TOP ({Found}) c_Code AS Code, c_Name AS Name FROM dbo.{table}
            WHERE f_Active = 1 AND {where}
            ORDER BY CASE WHEN c_Code LIKE @First ESCAPE '\' THEN 0 ELSE 1 END, c_Name
            """, args)).ToList();
    }

    // ----- Bank branches ------------------------------------------------------------

    public async Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<BankBranch>(
            "SELECT c_Ifsc AS Ifsc, c_Bank AS Bank, c_Branch AS Branch, c_Micr AS Micr FROM dbo.t_Unotp_Ifsc_Mst WHERE c_Ifsc = @Ifsc AND f_Active = 1",
            new { Ifsc = ifsc.Trim().ToUpperInvariant() });
    }

    // Every word typed must be in the bank, the branch, the IFSC or the MICR; a
    // branch whose IFSC or MICR starts with what was typed comes first.
    public async Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default)
    {
        var words = Words(query, ' ', ',', '—', '-');
        if (words.Length == 0) return [];
        var (where, args) = AllWords(words, "c_Bank + ' ' + c_Branch + ' ' + c_Ifsc + ' ' + c_Micr");
        args.Add("Start", EscapeLikePattern(query.Trim()) + "%");
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<BankBranch>($"""
            SELECT TOP ({Found}) c_Ifsc AS Ifsc, c_Bank AS Bank, c_Branch AS Branch, c_Micr AS Micr FROM dbo.t_Unotp_Ifsc_Mst
            WHERE f_Active = 1 AND {where}
            ORDER BY CASE WHEN c_Ifsc LIKE @Start ESCAPE '\' OR c_Micr LIKE @Start ESCAPE '\' THEN 0 ELSE 1 END, c_Bank, c_Branch
            """, args)).ToList();
    }

    // ----- PIN codes ------------------------------------------------------------------

    public async Task<PinPlace?> PinCodeAsync(string pin, CancellationToken ct = default)
    {
        pin = pin.Trim();
        if (pin.Length != 6 || !pin.All(char.IsAsciiDigit)) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PinPlace>(
            "SELECT c_Pin_Code AS PinCode, c_District AS District, c_State AS State FROM dbo.t_Unotp_Pincode_Mst WHERE c_Pin_Code = @Pin AND f_Active = 1",
            new { Pin = pin });
    }

    // ----- The rate card ----------------------------------------------------------------

    private sealed record RateRow(string Category, int TenureMonths, string Scheme, string Payout, decimal Rate,
        long MinAmount, long? MaxAmount, DateTime EffectiveFrom);

    /// <summary>
    /// The rate card in effect on the day the deposit starts, for the deposit's category
    /// (or, failing rows of its own, defaultRateCategory's), the holder's gender and the
    /// application type: one row per tenure, payout and minimum amount, the latest in
    /// effect. A row with a blank gender or application type stands for every one.
    /// Rows come in the order the tenures and payouts are listed.
    /// </summary>
    public async Task<IReadOnlyList<RateOption>> RatesAsync(RatesRequest request, CancellationToken ct = default)
    {
        var starts = request.StartsOn ?? DateOnly.FromDateTime(DateTime.Today);
        var fallback = await reference.SettingAsync("defaultRateCategory", ct);
        var lists = await reference.ReferenceAsync(ct);

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<RateRow>("""
            SELECT c_Category AS Category, n_Tenure_Months AS TenureMonths, c_Scheme AS Scheme, c_Payout AS Payout, n_Rate AS Rate,
                   n_Min_Amount AS MinAmount, n_Max_Amount AS MaxAmount, d_Effective_From AS EffectiveFrom
            FROM dbo.t_Unotp_Rate_Card
            WHERE c_Category IN (@Category, @Fallback)
              AND c_Gender IN (@Gender, '')
              AND c_App_Type IN (@ApplicationType, '')
              AND f_Active = 1 AND d_Effective_From <= @Starts
            ORDER BY CASE WHEN c_Category = @Category THEN 0 ELSE 1 END, d_Effective_From DESC
            """, new { request.Category, Fallback = fallback, request.Gender, request.ApplicationType, Starts = starts.ToDateTime(TimeOnly.MinValue) });

        // The rows come the category's own first and the latest first, so the first
        // row seen for a tenure, payout and minimum amount is the one that stands.
        var seen = new HashSet<(int, string, long)>();
        var lines = new List<RateOption>();
        foreach (var row in rows)
        {
            if (!seen.Add((row.TenureMonths, row.Payout, row.MinAmount))) continue;
            lines.Add(new RateOption(row.TenureMonths, row.Scheme, row.Payout, row.Rate, row.MinAmount, row.MaxAmount, DateOnly.FromDateTime(row.EffectiveFrom)));
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

    /// <summary>
    /// The card rate for the deposit as it stands and what it comes to. A cumulative
    /// deposit compounds compoundingPerYear times a year (DepositMaths); one that pays
    /// out pays simple interest each period.
    /// </summary>
    public async Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var starts = request.Card.StartsOn ?? today;
        var compounding = await reference.NumberAsync("compoundingPerYear", ct);
        var payout = (await reference.ReferenceAsync(ct)).Payouts.FirstOrDefault(p => p.Code == request.Payout)
            ?? throw new ArgumentException($"No payout called {request.Payout}.");

        RateOption? line = null;
        foreach (var option in await RatesAsync(request.Card with { StartsOn = starts }, ct))
        {
            if (option.TenureMonths != request.TenureMonths) continue;
            if (option.Payout != request.Payout) continue;
            if (!option.Offers(request.Amount)) continue;
            line = option;
            break;
        }
        if (line is null) throw new ArgumentException($"The rate card offers no {payout.Name} payout for {request.TenureMonths} months on a deposit of {request.Amount} on {starts:dd-MM-yyyy}.");

        decimal amount = request.Amount;
        var maturity = amount;
        if (payout.PerYear == 0) maturity = DepositMaths.MaturityAmount(amount, line.Rate, request.TenureMonths, compounding);
        var each = DepositMaths.InterestEach(amount, line.Rate, payout.PerYear);
        return new DepositQuote(line.Rate, each, maturity, starts.AddMonths(request.TenureMonths), line.AsOn);
    }

    // ----- Searching ---------------------------------------------------------------------

    private static string[] Words(string query, params char[] separators) =>
        query.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Each word, anywhere in the text: "text LIKE @w0 AND text LIKE @w1 ...".
    private static (string Where, DynamicParameters Args) AllWords(string[] words, string text)
    {
        var args = new DynamicParameters();
        var where = string.Join(" AND ", words.Select((w, i) =>
        {
            args.Add("w" + i.ToString(CultureInfo.InvariantCulture), "%" + EscapeLikePattern(w) + "%");
            return $"{text} LIKE @w{i} ESCAPE '\\'";
        }));
        return (where, args);
    }

    // What was typed, taken as it is: LIKE's own characters are escaped.
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
