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

        public FolioRecord Record() =>
            new(Pan, Dates.FromDb(Dob), Folio, Name, Gender, Address, new DocsOnRecord(DocPan, DocPhoto, DocPoa), Note, Source ?? "");
    }

    // One row of MasterQueries.KycOnCommon or KycOnBt, as this reads it.
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
        public string? MailLine1 { get; set; }
        public string? MailLine2 { get; set; }
        public string? MailLine3 { get; set; }
        public string? MailCity { get; set; }
        public string? MailPinCode { get; set; }

        // The mailing address the row carries; null where it holds none.
        public TypedAddress? Mail()
        {
            if (string.IsNullOrWhiteSpace(MailLine1)) return null;
            return new TypedAddress(MailLine1.Trim(), (MailLine2 ?? "").Trim(), (MailLine3 ?? "").Trim(), (MailCity ?? "").Trim(), (MailPinCode ?? "").Trim());
        }
    }

    // Every folio held against the PAN: more than one is a record Operations has to merge.
    public Task<IReadOnlyList<FolioDeposit>> FolioDepositsByPanAsync(string pan, CancellationToken ct = default) =>
        FolioDepositsAsync("d.Pan = @Value", pan, ct);

    public Task<IReadOnlyList<FolioDeposit>> FolioDepositsByFolioAsync(string folio, CancellationToken ct = default) =>
        FolioDepositsAsync("d.Folio = @Value", folio, ct);

    // The deposits the folio check looks at, for one PAN or one folio.
    private async Task<IReadOnlyList<FolioDeposit>> FolioDepositsAsync(string where, string value, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Folio, string Pan, DateTime? Dob)>(
            $"SELECT d.Folio, d.Pan, d.Dob FROM ({MasterQueries.FolioDeposits}) d WHERE {where} ORDER BY d.Folio",
            new { Value = value.Trim().ToUpperInvariant() });
        return rows.Select(r => new FolioDeposit((r.Folio ?? "").Trim(), (r.Pan ?? "").Trim(), Dates.FromDb(r.Dob))).ToList();
    }

    // One row of MasterQueries.KycSource. The id is a number in most sources and blank for the folio master.
    private sealed class KycSourceRow
    {
        public string Source { get; set; } = "";
        public object? Id { get; set; }
    }

    public async Task<FolioRecord?> FolioOnRecordAsync(string folio, string pan, string dob, CancellationToken ct = default)
    {
        var f = await FolioAsync(folio, ct);
        if (f is null) return null;

        // The folio master's own source, address and documents stand where no
        // source holds a row for the holder, or holds none of that kind.
        var kyc = await KycSourceAsync(pan, dob, f.Folio, ct);
        var source = kyc?.Source ?? f.Source;
        var address = await AddressOnFolioAsync(source, f.Folio, ct);
        if (address.Length == 0) address = f.Address;
        var docs = await DocumentsOnFolioAsync(source, f.Folio, ct) ?? f.Docs;
        return f with { Source = source, Address = address, Docs = docs };
    }

    // Where a holder's latest KYC data, address and documents are kept; null when no source holds them.
    private async Task<KycSource?> KycSourceAsync(string pan, string dob, string folio, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<KycSourceRow>(MasterQueries.KycSource,
            new { pan = pan.Trim().ToUpperInvariant(), dob = Dates.ParseDdMmYyyy(dob), folio = folio.Trim().ToUpperInvariant() });
        if (row is null) return null;
        return new KycSource(row.Source.Trim(), Convert.ToString(row.Id, CultureInfo.InvariantCulture) ?? "");
    }

    public async Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QuerySingleOrDefaultAsync<FolioRow>(
            $"SELECT * FROM ({MasterQueries.Folios}) f WHERE f.Folio = @Folio",
            new { Folio = folio.Trim().ToUpperInvariant() }))?.Record();
    }

    // The details held for a folio, read from where its latest KYC is kept: the latest
    // row that source's query gives for the folio. The marital status is kept as its
    // code, and given as the name the page offers.
    public async Task<HolderDetails?> KycOnFolioAsync(string source, string folio, CancellationToken ct = default)
    {
        var query = KycDetailsQuery(source);
        if (query is null || folio.Trim().Length == 0) return null;

        var maritalStatuses = ((await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None).MaritalStatuses;
        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<FolioKycRow>(
            $"SELECT TOP (1) * FROM ({query}) k WHERE k.Folio = @Folio ORDER BY k.SavedOn DESC",
            new { Folio = folio.Trim() });
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
            PepRelated: row.PepRelated ?? "",
            Communication: row.Mail());
    }

    // The query that reads KYC details from a source, by folio; null for the folio
    // master, which holds none.
    private static string? KycDetailsQuery(string source)
    {
        if (source == KycSources.Common) return MasterQueries.KycOnCommon;
        if (source == KycSources.Bt) return MasterQueries.KycOnBt;
        return null;
    }

    // The address the source holds for the folio: its latest permanent address row.
    // Empty for the folio master, whose address comes with the folio itself.
    private async Task<string> AddressOnFolioAsync(string source, string folio, CancellationToken ct)
    {
        var query = AddressQuery(source);
        if (query is null || folio.Trim().Length == 0) return "";

        await using var connection = await db.OpenAsync(ct);
        var address = await connection.QueryFirstOrDefaultAsync<string>(
            $"SELECT TOP (1) a.Address FROM ({query}) a WHERE a.Folio = @Folio ORDER BY a.SavedOn DESC",
            new { Folio = folio.Trim() });
        // The last part may be missing, which leaves the comma before it.
        return (address ?? "").Trim().TrimEnd(',').Trim();
    }

    // The query that reads a source's permanent address, by folio; null for the folio master.
    private static string? AddressQuery(string source)
    {
        if (source == KycSources.Common) return MasterQueries.AddressOnCommon;
        if (source == KycSources.Bt) return MasterQueries.AddressOnBt;
        return null;
    }

    // Which of the PAN copy, the photograph and a proof of address are on record at
    // the source for the folio, told by the sub-type each is filed under. A PAN copy
    // or a proof counts only once it is verified there; a photograph, which nothing
    // verifies, counts when it is there. No other document is looked for.
    private async Task<DocsOnRecord?> DocumentsOnFolioAsync(string source, string folio, CancellationToken ct)
    {
        var query = DocumentsQuery(source);
        if (query is null || folio.Trim().Length == 0) return null;

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string SubTypeCode, int Verified)>(
            $"SELECT d.SubTypeCode, MAX(d.Verified) FROM ({query}) d WHERE d.Folio = @Folio GROUP BY d.SubTypeCode",
            new { Folio = folio.Trim() });
        var filed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var subType = (row.SubTypeCode ?? "").Trim();
            filed.Add(subType);
            if (row.Verified == 1) verified.Add(subType);
        }

        var codes = await reference.DocumentCodesAsync(ct);
        bool In(HashSet<string> held, string document) => codes.TryGetValue(document, out var code) && held.Contains(code.SubTypeCode);
        var proofs = codes.Keys.Where(document => document.StartsWith("poa:", StringComparison.Ordinal));
        return new DocsOnRecord(In(verified, "pan"), In(filed, "photo"), proofs.Any(proof => In(verified, proof)));
    }

    // The query that reads a source's documents, by folio; null for the folio master.
    private static string? DocumentsQuery(string source)
    {
        if (source == KycSources.Common) return MasterQueries.DocumentsOnCommon;
        if (source == KycSources.Bt) return MasterQueries.DocumentsOnBt;
        return null;
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
            SELECT n.f_Nominee_Name, n.f_Nominee_DOB, n.f_Nominee_Relations, ISNULL(n.f_GuardianName, N''), m.f_App_No
            FROM dbo.t_FD_BT_Nominee_Dtl n
            JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = n.f_Appl_No AND m.f_Active = 1
            WHERE m.f_Folio = @Folio AND m.f_Fdr_No = @DepositNumber AND n.f_Status IN ('APR', 'PEN_E') AND n.f_Active = 1
            ORDER BY m.f_Submitted_On DESC
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
            SELECT b.f_NEFTCode, b.f_BankAccountNo, b.f_BankName, b.f_BranchName, m.f_App_No
            FROM dbo.t_FD_BT_Investor_Bank_Dtl b
            JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = b.f_Appl_No AND m.f_Active = 1
            WHERE m.f_Folio = @Folio AND m.f_Fdr_No = @DepositNumber AND b.f_Status IN ('APR', 'PEN_E') AND b.f_Active = 1
              AND b.f_NEFTCode <> '' AND b.f_BankAccountNo <> ''
            ORDER BY m.f_Submitted_On DESC
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
        PartyAsync(MasterQueries.Brokers, code, ct);

    public Task<Party?> StaffMemberAsync(string code, string staffRule, CancellationToken ct = default) =>
        PartyAsync(StaffQuery(staffRule), code, ct);

    public Task<IReadOnlyList<Party>> SearchBrokersAsync(string query, CancellationToken ct = default) =>
        SearchPartiesAsync(MasterQueries.Brokers, query, ct);

    public Task<IReadOnlyList<Party>> SearchStaffAsync(string query, string staffRule, CancellationToken ct = default) =>
        SearchPartiesAsync(StaffQuery(staffRule), query, ct);

    // The staff a sourcing mode takes, by the rule its row names; any employee in
    // service for a mode that names none.
    private static string StaffQuery(string staffRule)
    {
        if (staffRule == "branch") return MasterQueries.StaffForBranch;
        if (staffRule == "mfis") return MasterQueries.StaffForMfis;
        if (staffRule == "mflEx") return MasterQueries.StaffForMflEx;
        return MasterQueries.Staff;
    }

    // The one row a code names, or null.
    private async Task<Party?> PartyAsync(string parties, string code, CancellationToken ct)
    {
        code = code.Trim();
        if (code.Length == 0) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<Party>(
            $"SELECT TOP (1) p.Code, p.Name FROM ({parties}) p WHERE p.Code = @Code", new { Code = code });
    }

    // Every word typed must be in the code or the name; a code that starts with the text comes first.
    // Nothing is asked until enough is typed (TypedSearch.FromLength).
    private async Task<IReadOnlyList<Party>> SearchPartiesAsync(string parties, string query, CancellationToken ct)
    {
        if (!TypedSearch.LongEnough(query)) return [];
        var words = Words(query, ' ');
        if (words.Length == 0) return [];
        var (where, args) = AllWords(words, "p.Code + ' ' + p.Name");
        args.Add("First", EscapeLikePattern(words[0]) + "%");
        await using var connection = await db.OpenAsync(ct);
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
