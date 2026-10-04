using System.Globalization;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// The big tables the purchase journey looks rows up in (db/create_tables.sql): the
/// investors on record, the sourcing registers (brokers, staff), bank branches and
/// PIN codes. The rate card is in SqlMasters.RateCard.cs.
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
        public bool Compliant { get; set; }

        public FolioRecord Record() =>
            new(Pan, Dates.FromDb(Dob), Folio, Name, Gender, Address, new DocsOnRecord(DocPan, DocPhoto, DocPoa), Note, Source ?? "", Compliant);
    }

    // One row of MasterQueries.FolioKyc, as this reads it.
    private sealed class FolioKycRow
    {
        public string? NameType { get; set; }
        public string? ParentName { get; set; }
        public string? AnnualIncome { get; set; }
        public string? Occupation { get; set; }
        public string? SubOccupation { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Pep { get; set; }
        public string? PepRelated { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
    }

    // Every folio held against the PAN: more than one is a record Operations has to merge.
    public async Task<IReadOnlyList<FolioRecord>> FoliosByPanAsync(string pan, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<FolioRow>(
            $"SELECT * FROM ({MasterQueries.Folios}) f WHERE f.Pan = @Pan ORDER BY f.Folio",
            new { Pan = pan.Trim().ToUpperInvariant() })).Select(f => f.Record()).ToList();
    }

    public async Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QuerySingleOrDefaultAsync<FolioRow>(
            $"SELECT * FROM ({MasterQueries.Folios}) f WHERE f.Folio = @Folio",
            new { Folio = folio.Trim().ToUpperInvariant() }))?.Record();
    }

    // The details a compliant folio holds: the latest row MasterQueries.FolioKyc gives
    // for it. The marital status is kept as its code, and given as the name the page offers.
    public async Task<HolderDetails?> KycOnFolioAsync(string folio, CancellationToken ct = default)
    {
        var record = await FolioAsync(folio, ct);
        if (record is null || !record.Compliant) return null;

        var maritalStatuses = ((await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None).MaritalStatuses;
        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<FolioKycRow>(
            $"SELECT TOP (1) * FROM ({MasterQueries.FolioKyc}) k WHERE k.Folio = @Folio ORDER BY k.SavedOn DESC",
            new { record.Folio });
        if (row is null) return null;

        return new HolderDetails(
            Holder: "",
            NameType: row.NameType ?? "",
            ParentName: row.ParentName ?? "",
            AnnualIncome: row.AnnualIncome ?? "",
            Occupation: row.Occupation ?? "",
            SubOccupation: row.SubOccupation ?? "",
            MaritalStatus: MasterLists.NameOf(maritalStatuses, row.MaritalStatus ?? ""),
            Mobile: row.Mobile ?? "",
            Email: row.Email ?? "",
            Pep: row.Pep ?? "",
            PepRelated: row.PepRelated ?? "");
    }

    // What a deposit of the folio holds, for its renewal: the nominees and repayment
    // accounts on the application the deposit was booked from.
    public async Task<IReadOnlyList<NomineeOnRecord>> NomineesOnDepositAsync(string folio, string depositNumber, CancellationToken ct = default)
    {
        var found = new List<NomineeOnRecord>();
        // The relation is kept as its code, and shown as the name the page offers.
        var relations = ((await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None).NomineeRelations;
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Name, DateTime? Dob, string Relation, string GuardianName, string AppNo)>("""
            SELECT n.f_Nominee_Name, n.f_Nominee_DOB, n.f_Nominee_Relations, ISNULL(n.f_GuardianName, N''), m.c_App_No
            FROM dbo.t_FD_BT_Nominee_Dtl n
            JOIN dbo.t_Unotp_Application_Mst m ON m.c_App_No = n.f_Appl_No AND m.f_Active = 1
            WHERE m.c_Folio = @Folio AND m.c_Fdr_No = @DepositNumber AND n.f_Status = 'APR' AND n.f_Active = 1
            ORDER BY m.d_Submitted_On DESC
            """, new { Folio = folio.Trim(), DepositNumber = depositNumber.Trim() });
        foreach (var r in rows)
        {
            if (found.Any(f => f.Name == r.Name && f.Dob == Dates.FromDb(r.Dob))) continue;
            found.Add(new NomineeOnRecord(r.Name, Dates.FromDb(r.Dob), MasterLists.NameOf(relations, r.Relation), r.GuardianName, r.AppNo));
        }
        return found;
    }

    public async Task<IReadOnlyList<AccountOnRecord>> AccountsOnDepositAsync(string folio, string depositNumber, CancellationToken ct = default)
    {
        var found = new List<AccountOnRecord>();
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Ifsc, string AccountNo, string Bank, string Branch, string AppNo)>("""
            SELECT b.f_NEFTCode, b.f_BankAccountNo, b.f_BankName, b.f_BranchName, m.c_App_No
            FROM dbo.t_FD_BT_Investor_Bank_Dtl b
            JOIN dbo.t_Unotp_Application_Mst m ON m.c_App_No = b.f_Appl_No AND m.f_Active = 1
            WHERE m.c_Folio = @Folio AND m.c_Fdr_No = @DepositNumber AND b.f_Status = 'APR' AND b.f_Active = 1
              AND b.f_NEFTCode <> '' AND b.f_BankAccountNo <> ''
            ORDER BY m.d_Submitted_On DESC
            """, new { Folio = folio.Trim(), DepositNumber = depositNumber.Trim() });
        foreach (var r in rows)
        {
            if (found.Any(f => f.Ifsc == r.Ifsc && f.AccountNumber == r.AccountNo)) continue;
            found.Add(new AccountOnRecord(r.Ifsc, r.AccountNo, r.Bank, r.Branch, r.AppNo));
        }
        return found;
    }

    // ----- Sourcing registers ------------------------------------------------------

    // The brokers' and the staff's own queries are in MasterQueries. Both registers are
    // big (the staff one has tens of thousands of rows), so neither is ever read whole:
    // a code is looked up by itself, and a search asks only for what was typed.

    public Task<Party?> BrokerAsync(string code, CancellationToken ct = default) =>
        PartyAsync(MasterQueries.Brokers, code, [], ct);

    public Task<Party?> StaffMemberAsync(string code, IReadOnlyList<string> departments, CancellationToken ct = default) =>
        PartyAsync(MasterQueries.Staff, code, departments, ct);

    public Task<IReadOnlyList<Party>> SearchBrokersAsync(string query, CancellationToken ct = default) =>
        SearchPartiesAsync(MasterQueries.Brokers, query, [], ct);

    public Task<IReadOnlyList<Party>> SearchStaffAsync(string query, IReadOnlyList<string> departments, CancellationToken ct = default) =>
        SearchPartiesAsync(MasterQueries.Staff, query, departments, ct);

    // The one row a code names, or null.
    private async Task<Party?> PartyAsync(string parties, string code, IReadOnlyList<string> departments, CancellationToken ct)
    {
        code = code.Trim();
        if (code.Length == 0) return null;
        var args = new DynamicParameters();
        args.Add("Code", code);
        var inDepartments = DepartmentFilter(departments, args);
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<Party>(
            $"SELECT TOP (1) p.Code, p.Name FROM ({parties}) p WHERE p.Code = @Code{inDepartments}", args);
    }

    // Every word typed must be in the code or the name; a code that starts with the text comes first.
    // Nothing is asked until enough is typed (TypedSearch.FromLength).
    private async Task<IReadOnlyList<Party>> SearchPartiesAsync(string parties, string query, IReadOnlyList<string> departments, CancellationToken ct)
    {
        if (!TypedSearch.LongEnough(query)) return [];
        var words = Words(query, ' ');
        if (words.Length == 0) return [];
        var (where, args) = AllWords(words, "p.Code + ' ' + p.Name");
        args.Add("First", EscapeLikePattern(words[0]) + "%");
        var inDepartments = DepartmentFilter(departments, args);
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<Party>($"""
            SELECT TOP ({Found}) p.Code, p.Name FROM ({parties}) p
            WHERE {where}{inDepartments}
            ORDER BY CASE WHEN p.Code LIKE @First ESCAPE '\' THEN 0 ELSE 1 END, p.Name
            """, args)).ToList();
    }

    // " AND p.Department IN @Departments" where departments are given; nothing where none is,
    // so every department is looked in. Only the staff query gives a Department back.
    private static string DepartmentFilter(IReadOnlyList<string> departments, DynamicParameters args)
    {
        if (departments.Count == 0) return "";
        args.Add("Departments", departments);
        return " AND p.Department IN @Departments";
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
    // branch whose IFSC or MICR starts with what was typed comes first. The bank master
    // is big, so nothing is asked until enough is typed (TypedSearch.FromLength).
    public async Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default)
    {
        if (!TypedSearch.LongEnough(query)) return [];
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
        await using var connection = await db.OpenAsync(ct);
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
