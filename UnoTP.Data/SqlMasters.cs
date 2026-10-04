using System.Globalization;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// The masters the purchase journey reads (db/create_master_tables.sql): the investors on record (the
/// folio database), the sourcing registers, bank branches and PIN codes (the masters
/// database). The rate card is in SqlMasters.RateCard.cs.
/// </summary>
public sealed partial class SqlMasters(Db db, SqlReference reference) : IInvestorApi, ISourcingApi, IDepositApi, IPlaceApi
{
    private const int Found = 20;

    // ----- Investors ---------------------------------------------------------------

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
        public string? Source { get; set; }

        public FolioRecord Record() =>
            new(Pan, Dates.FromDb(Dob), Folio, Name, Gender, Address, new DocsOnRecord(DocPan, DocPhoto, DocPoa), Note, Source ?? "");
    }

    // Every folio held against the PAN: more than one is a record Operations has to merge.
    public async Task<IReadOnlyList<FolioRecord>> FoliosByPanAsync(string pan, CancellationToken ct = default)
    {
        await using var connection = await db.OpenMastersAsync(ct);
        return (await connection.QueryAsync<FolioRow>(
            $"SELECT * FROM ({MasterQueries.Folios}) f WHERE f.Pan = @Pan ORDER BY f.Folio",
            new { Pan = pan.Trim().ToUpperInvariant() })).Select(f => f.Record()).ToList();
    }

    public async Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default)
    {
        await using var connection = await db.OpenMastersAsync(ct);
        return (await connection.QuerySingleOrDefaultAsync<FolioRow>(
            $"SELECT * FROM ({MasterQueries.Folios}) f WHERE f.Folio = @Folio",
            new { Folio = folio.Trim().ToUpperInvariant() }))?.Record();
    }

    // What is on record against a folio: the nominees and repayment accounts on the
    // app's own submitted applications, latest first. Those on the folio's earlier
    // deposits are the FD system's, and are not read until it answers.
    public async Task<IReadOnlyList<NomineeOnRecord>> NomineesByFolioAsync(string folio, CancellationToken ct = default)
    {
        var found = new List<NomineeOnRecord>();
        // The relation is kept as the FD system's code, and shown as the name the page offers.
        var relations = ((await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None).NomineeRelations;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Name, DateTime? Dob, string Relation, string GuardianName, string AppNo)>("""
            SELECT n.f_Nominee_Name, n.f_Nominee_DOB, n.f_Nominee_Relations, ISNULL(n.f_GuardianName, N''), m.c_App_No
            FROM dbo.t_FD_BT_Nominee_Dtl n
            JOIN dbo.t_Unotp_Application_Mst m ON m.c_App_No = n.f_Appl_No AND m.f_Active = 1
            WHERE m.c_Folio = @Folio AND n.f_Status = 'APR' AND n.f_Active = 1
            ORDER BY m.d_Submitted_On DESC
            """, new { Folio = folio.Trim() });
        foreach (var r in rows)
        {
            if (found.Any(f => f.Name == r.Name && f.Dob == Dates.FromDb(r.Dob))) continue;
            found.Add(new NomineeOnRecord(r.Name, Dates.FromDb(r.Dob), MasterLists.NameOf(relations, r.Relation), r.GuardianName, r.AppNo));
        }
        return found;
    }

    public async Task<IReadOnlyList<AccountOnRecord>> AccountsByFolioAsync(string folio, CancellationToken ct = default)
    {
        var found = new List<AccountOnRecord>();
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Ifsc, string AccountNo, string Bank, string Branch, string AppNo)>("""
            SELECT b.f_NEFTCode, b.f_BankAccountNo, b.f_BankName, b.f_BranchName, m.c_App_No
            FROM dbo.t_FD_BT_Investor_Bank_Dtl b
            JOIN dbo.t_Unotp_Application_Mst m ON m.c_App_No = b.f_Appl_No AND m.f_Active = 1
            WHERE m.c_Folio = @Folio AND b.f_Status = 'APR' AND b.f_Active = 1 AND b.f_NEFTCode <> '' AND b.f_BankAccountNo <> ''
            ORDER BY m.d_Submitted_On DESC
            """, new { Folio = folio.Trim() });
        foreach (var r in rows)
        {
            if (found.Any(f => f.Ifsc == r.Ifsc && f.AccountNumber == r.AccountNo)) continue;
            found.Add(new AccountOnRecord(r.Ifsc, r.AccountNo, r.Bank, r.Branch, r.AppNo));
        }
        return found;
    }

    // ----- Sourcing registers ------------------------------------------------------

    // The brokers' and the staff's own queries are in MasterQueries: these list and search what they give back.

    public Task<IReadOnlyList<Party>> BrokersAsync(CancellationToken ct = default) =>
        PartiesAsync(MasterQueries.Brokers, ct);

    public Task<IReadOnlyList<Party>> StaffAsync(CancellationToken ct = default) =>
        PartiesAsync(MasterQueries.Staff, ct);

    public Task<IReadOnlyList<Party>> SearchBrokersAsync(string query, CancellationToken ct = default) =>
        SearchPartiesAsync(MasterQueries.Brokers, query, ct);

    public Task<IReadOnlyList<Party>> SearchStaffAsync(string query, CancellationToken ct = default) =>
        SearchPartiesAsync(MasterQueries.Staff, query, ct);

    private async Task<IReadOnlyList<Party>> PartiesAsync(string parties, CancellationToken ct)
    {
        await using var connection = await db.OpenMastersAsync(ct);
        return (await connection.QueryAsync<Party>($"SELECT p.Code, p.Name FROM ({parties}) p ORDER BY p.Name")).ToList();
    }

    // Every word typed must be in the code or the name; a code that starts with the text comes first.
    private async Task<IReadOnlyList<Party>> SearchPartiesAsync(string parties, string query, CancellationToken ct)
    {
        var words = Words(query, ' ');
        if (words.Length == 0) return [];
        var (where, args) = AllWords(words, "p.Code + ' ' + p.Name");
        args.Add("First", EscapeLikePattern(words[0]) + "%");
        await using var connection = await db.OpenMastersAsync(ct);
        return (await connection.QueryAsync<Party>($"""
            SELECT TOP ({Found}) p.Code, p.Name FROM ({parties}) p
            WHERE {where}
            ORDER BY CASE WHEN p.Code LIKE @First ESCAPE '\' THEN 0 ELSE 1 END, p.Name
            """, args)).ToList();
    }

    // ----- Bank branches ------------------------------------------------------------

    public async Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<BankBranch>(
            $"SELECT b.Ifsc, b.Bank, b.Branch, b.Micr FROM ({MasterQueries.BankBranches}) b WHERE b.Ifsc = @Ifsc",
            new { Ifsc = ifsc.Trim().ToUpperInvariant() });
    }

    // Every word typed must be in the bank, the branch, the IFSC or the MICR; a
    // branch whose IFSC or MICR starts with what was typed comes first.
    public async Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default)
    {
        var words = Words(query, ' ', ',', '—', '-');
        if (words.Length == 0) return [];
        var (where, args) = AllWords(words, "b.Bank + ' ' + b.Branch + ' ' + b.Ifsc + ' ' + b.Micr");
        args.Add("Start", EscapeLikePattern(query.Trim()) + "%");
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<BankBranch>($"""
            SELECT TOP ({Found}) b.Ifsc, b.Bank, b.Branch, b.Micr FROM ({MasterQueries.BankBranches}) b
            WHERE {where}
            ORDER BY CASE WHEN b.Ifsc LIKE @Start ESCAPE '\' OR b.Micr LIKE @Start ESCAPE '\' THEN 0 ELSE 1 END, b.Bank, b.Branch
            """, args)).ToList();
    }

    // ----- PIN codes ------------------------------------------------------------------

    public async Task<PinPlace?> PinCodeAsync(string pin, CancellationToken ct = default)
    {
        pin = pin.Trim();
        if (pin.Length != 6 || !pin.All(char.IsAsciiDigit)) return null;
        await using var connection = await db.OpenMastersAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PinPlace>(
            $"SELECT p.PinCode, p.District, p.State FROM ({MasterQueries.PinCodes}) p WHERE p.PinCode = @Pin",
            new { Pin = pin });
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
