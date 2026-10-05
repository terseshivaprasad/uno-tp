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

    // How many branches the bank search gives back, as the FD system's own does.
    private const int BanksFound = 15;

    // ----- Investors ---------------------------------------------------------------

    private sealed class FolioRow
    {
        public string Pan { get; set; } = "";
        public DateTime? Dob { get; set; }
        public string Folio { get; set; } = "";
        public string Name { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Address { get; set; } = "";
        public string? Line1 { get; set; }
        public string? Line2 { get; set; }
        public string? Line3 { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? PinCode { get; set; }
        public bool DocPhoto { get; set; }
        public bool DocPoa { get; set; }
        public string Note { get; set; } = "";
        public string? Source { get; set; }

        // The address the folio master holds for the holder.
        public AddressOnRecord AddressOnRecord => new(Address, PartsOf(Line1, Line2, Line3, City, District, State, PinCode));

        public FolioRecord Record() =>
            new(Pan, Dates.FromDb(Dob), Folio, Name, Gender, Address, OnRecord(DocPhoto, DocPoa, AddressOnRecord), Note, Source ?? "");
    }

    // An address on record for a holder: as one line, and in its own parts.
    private sealed record AddressOnRecord(string Address, TypedAddress Parts);

    // The parts of an address as a source keeps them; one it does not hold is empty.
    private static TypedAddress PartsOf(string? line1, string? line2, string? line3, string? city, string? district, string? state, string? pinCode) =>
        new((line1 ?? "").Trim(), (line2 ?? "").Trim(), (line3 ?? "").Trim(), (city ?? "").Trim(), (pinCode ?? "").Trim(),
            (district ?? "").Trim(), (state ?? "").Trim());

    // What a holder on a folio need not upload again.
    //   - The PAN copy is never asked of them: the folio was opened on it.
    //   - The photograph counts when its document is on record.
    //   - The proof of address counts when its document is on record, verified or
    //     not, and the address on record gives its first line, its city and its PIN
    //     code. Without those the address has to be read off a proof filed here, so
    //     the proof is asked for. A record with a district and no city has its
    //     district saved as the city, so the district counts for it.
    private static DocsOnRecord OnRecord(bool photo, bool poa, AddressOnRecord address)
    {
        var parts = address.Parts;
        var hasCity = parts.City.Length > 0 || parts.District.Length > 0;
        var addressComplete = parts.Line1.Length > 0 && hasCity && parts.PinCode.Length > 0;
        return new DocsOnRecord(Pan: true, Photo: photo, Poa: poa && addressComplete);
    }

    // One row of MasterQueries.KycOnCommon or KycOnBt, as this reads it.
    private sealed class FolioKycRow
    {
        public string? NamePrefix { get; set; }
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

    // A PAN, a folio number or a code a row is looked up by, sent as plain (VARCHAR)
    // text. The FD system's tables are big and the lookup has to use their index:
    // text sent the usual way (NVARCHAR) against a VARCHAR column makes SQL Server
    // convert the column and read the whole index instead. Plain text is found by
    // the index whichever of the two the column is. These keys are letters and
    // digits only, so nothing is lost.
    private static DbString Key(string value) => Plain(value.Trim().ToUpperInvariant());

    // Text sent as plain (VARCHAR) text, as it stands: a name looked up, or what a search matches.
    private static DbString Plain(string value) => new() { Value = value, IsAnsi = true };

    // Every folio held against the PAN: more than one is a record Operations has to merge.
    public Task<IReadOnlyList<FolioDeposit>> FolioDepositsByPanAsync(string pan, CancellationToken ct = default) =>
        FolioDepositsAsync(MasterQueries.FolioDepositsByPan, new { Pan = Key(pan) }, ct);

    public Task<IReadOnlyList<FolioDeposit>> FolioDepositsByFolioAsync(string folio, CancellationToken ct = default) =>
        FolioDepositsAsync(MasterQueries.FolioDepositsByFolio, new { Folio = Key(folio) }, ct);

    public async Task<string> FolioOfFirstHolderAsync(string pan, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var folio = await connection.QueryFirstOrDefaultAsync<string>(
            $"SELECT TOP (1) h.Folio FROM ({MasterQueries.FirstHolders}) h ORDER BY h.Folio",
            new { Pan = Key(pan) });
        return (folio ?? "").Trim();
    }

    // The deposits the folio check looks at, for one PAN or one folio: the query
    // carries the key it reads by.
    private async Task<IReadOnlyList<FolioDeposit>> FolioDepositsAsync(string deposits, object key, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Folio, string Pan, DateTime? Dob)>(
            $"SELECT d.Folio, d.Pan, d.Dob FROM ({deposits}) d ORDER BY d.Folio", key);
        return rows.Select(r => new FolioDeposit((r.Folio ?? "").Trim(), (r.Pan ?? "").Trim(), Dates.FromDb(r.Dob))).ToList();
    }

    public async Task<FolioRecord?> FolioOnRecordAsync(string folio, string pan, string dob, CancellationToken ct = default)
    {
        // The holder's own row: the folio master holds one for every holder of the folio.
        var row = await HolderRowAsync(folio, pan, ct);
        if (row is null) return null;
        var f = row.Record();

        // The folio master's own source, address and documents stand where no
        // source holds a row for the holder, or holds none of that kind.
        var source = await KycSourceAsync(pan, dob, f.Folio, ct) ?? f.Source;
        var holder = HolderOf(f.Folio, pan, dob);
        var address = await AddressOnFolioAsync(source, holder, ct) ?? row.AddressOnRecord;
        var documents = await DocumentsOnFolioAsync(source, holder, ct) ?? (row.DocPhoto, row.DocPoa);
        var docs = OnRecord(documents.Photo, documents.Poa, address);

        // The gender, where the folio master holds none: the one the holder's latest
        // KYC row at their source gives, where it is kept as the prefix to their name.
        var gender = f.Gender;
        if (Genders.Of(gender).Length == 0) gender = await GenderOnSourceAsync(source, holder, ct);
        return f with { Source = source, Address = address.Address, Docs = docs, Gender = gender };
    }

    // The gender the source's latest KYC row gives for the holder, off the prefix to
    // their name; empty for the folio master, or where the row has no prefix it knows.
    private async Task<string> GenderOnSourceAsync(string source, FolioHolder holder, CancellationToken ct)
    {
        var query = KycDetailsQuery(source);
        if (query is null) return "";
        await using var connection = await db.OpenAsync(ct);
        var prefix = await connection.QueryFirstOrDefaultAsync<string>(
            $"SELECT TOP (1) k.NamePrefix FROM ({query}) k ORDER BY k.SavedOn DESC", holder);
        return NamePrefixes.GenderOf(prefix);
    }

    // A holder of a folio, as the sources are asked for them: a folio can have more
    // than one holder, so a row is theirs only by folio, PAN and date of birth together.
    private sealed record FolioHolder(DbString Folio, DbString Pan, DateTime? Dob);

    private static FolioHolder HolderOf(string folio, string pan, string dob) =>
        new(Key(folio), Key(pan), Dates.ParseDdMmYyyy(dob));

    // Where a holder's latest KYC data, address and documents are kept (KycSources);
    // null when no source holds them. Only the query's first column, source, is read.
    private async Task<string?> KycSourceAsync(string pan, string dob, string folio, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var source = await connection.QueryFirstOrDefaultAsync<string>(MasterQueries.KycSource,
            new { pan = Key(pan), dob = Dates.ParseDdMmYyyy(dob), folio = Key(folio) });
        return source?.Trim();
    }

    // The folio by its number alone is its first holder's row.
    public async Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default) =>
        (await HolderRowAsync(folio, "", ct))?.Record();

    // One holder's row of a folio in the folio master: the holder with this PAN, or
    // with no PAN the first holder. The first row found is taken, so a holder the
    // master happens to hold twice does not stop the search.
    private async Task<FolioRow?> HolderRowAsync(string folio, string pan, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<FolioRow>(
            MasterQueries.Folios, new { Folio = Key(folio), Pan = Key(pan) });
    }

    // The details held for a holder of a folio, read from where their latest KYC is
    // kept: the latest row that source's query gives for the folio, PAN and date of
    // birth. The marital status is kept as its code, and given as the name the page offers.
    public async Task<HolderDetails?> KycOnFolioAsync(string source, string folio, string pan, string dob, CancellationToken ct = default)
    {
        var query = KycDetailsQuery(source);
        if (query is null || folio.Trim().Length == 0) return null;

        var maritalStatuses = ((await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None).MaritalStatuses;
        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<FolioKycRow>(
            $"SELECT TOP (1) * FROM ({query}) k ORDER BY k.SavedOn DESC",
            HolderOf(folio, pan, dob));
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

    // The query that reads KYC details from a source; null for the folio master,
    // which holds none.
    private static string? KycDetailsQuery(string source)
    {
        if (source == KycSources.Common) return MasterQueries.KycOnCommon;
        if (source == KycSources.Bt) return MasterQueries.KycOnBt;
        return null;
    }

    // The address the source holds for the holder: their latest permanent address row.
    // Null for the folio master, whose address comes with the folio itself, and where
    // the source holds none.
    private async Task<AddressOnRecord?> AddressOnFolioAsync(string source, FolioHolder holder, CancellationToken ct)
    {
        var query = AddressQuery(source);
        if (query is null) return null;

        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<AddressRowOnRecord>(
            $"SELECT TOP (1) a.Address, a.Line1, a.Line2, a.Line3, a.City, a.District, a.State, a.PinCode FROM ({query}) a ORDER BY a.SavedOn DESC",
            holder);
        // The last part may be missing, which leaves the comma before it.
        var address = (row?.Address ?? "").Trim().TrimEnd(',').Trim();
        if (row is null || address.Length == 0) return null;
        return new AddressOnRecord(address, PartsOf(row.Line1, row.Line2, row.Line3, row.City, row.District, row.State, row.PinCode));
    }

    // A source's permanent address row, under the names its query gives.
    private sealed class AddressRowOnRecord
    {
        public string? Address { get; set; }
        public string? Line1 { get; set; }
        public string? Line2 { get; set; }
        public string? Line3 { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? PinCode { get; set; }
    }

    /// <summary>
    /// The permanent address a holder of a folio has on record, in its own parts as the
    /// record keeps them: their source's, else the folio master's. It is what is
    /// written against a new application of theirs when no proof of address is filed
    /// on it. Null for a holder with no folio, or with no address on record.
    /// </summary>
    public async Task<TypedAddress?> PermanentAddressOnRecordAsync(Holder holder, CancellationToken ct = default)
    {
        if (holder.Folio.Length == 0) return null;
        var atSource = await AddressOnFolioAsync(holder.Source, HolderOf(holder.Folio, holder.Pan, holder.Dob), ct);
        if (atSource is not null) return atSource.Parts;
        return (await HolderRowAsync(holder.Folio, holder.Pan, ct))?.AddressOnRecord.Parts;
    }

    // The query that reads a source's permanent address; null for the folio master.
    private static string? AddressQuery(string source)
    {
        if (source == KycSources.Common) return MasterQueries.AddressOnCommon;
        if (source == KycSources.Bt) return MasterQueries.AddressOnBt;
        return null;
    }

    // Whether the photograph and a proof of address are on record at the source for
    // the holder, told by the sub-type each is filed under. A document that is there
    // counts, verified or not. Null for the folio master, which says so itself. No
    // other document is looked for: the PAN copy is never asked of a holder on a folio.
    private async Task<(bool Photo, bool Poa)?> DocumentsOnFolioAsync(string source, FolioHolder holder, CancellationToken ct)
    {
        var query = DocumentsQuery(source);
        if (query is null) return null;

        await using var connection = await db.OpenAsync(ct);
        var subTypes = await connection.QueryAsync<string>($"SELECT DISTINCT d.SubTypeCode FROM ({query}) d", holder);
        var filed = new HashSet<string>(subTypes.Select(subType => (subType ?? "").Trim()), StringComparer.OrdinalIgnoreCase);

        var codes = await reference.DocumentCodesAsync(ct);
        bool Filed(string document) => codes.TryGetValue(document, out var code) && filed.Contains(code.SubTypeCode);
        var proofs = codes.Keys.Where(document => document.StartsWith("poa:", StringComparison.Ordinal));
        return (Filed("photo"), proofs.Any(Filed));
    }

    /// <summary>
    /// The copies a holder of a folio has on record at their source, the newest first:
    /// the rows the check above counts, with the file each names. None for the folio
    /// master, which keeps no copies.
    /// </summary>
    public async Task<IReadOnlyList<CopyOnRecord>> CopiesOnRecordAsync(Holder holder, CancellationToken ct = default)
    {
        var query = DocumentsQuery(holder.Source);
        if (query is null || holder.Folio.Length == 0) return [];

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string SubTypeCode, string FileName, string FilePath)>(
            $"SELECT d.SubTypeCode, d.FileName, d.FilePath FROM ({query}) d ORDER BY d.SavedOn DESC",
            HolderOf(holder.Folio, holder.Pan, holder.Dob));
        return rows.Select(r => new CopyOnRecord((r.SubTypeCode ?? "").Trim(), r.FileName, r.FilePath)).ToList();
    }

    // The query that reads a source's documents; null for the folio master.
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
            $"SELECT TOP (1) p.Code, p.Name FROM ({parties}) p {ForThisCall}", OneRow("Code", Key(code)));
    }

    // The parties whose code and name hold what was typed, in the order typed; a code that
    // starts with the first word comes first. Nothing is asked until enough is typed (TypedSearch.FromLength).
    private async Task<IReadOnlyList<Party>> SearchPartiesAsync(string parties, string query, CancellationToken ct)
    {
        if (!TypedSearch.LongEnough(query)) return [];
        var words = Words(query, ' ');
        if (words.Length == 0) return [];
        var args = Typed("Code", words);
        args.Add("First", Plain(words[0] + "%"));
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<Party>($"""
            SELECT TOP ({Found}) p.Code, p.Name FROM ({parties}) p
            ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name {ForThisCall}
            """, args)).ToList();
    }

    // ----- Bank branches ------------------------------------------------------------

    public async Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<BankBranch>(
            $"SELECT TOP (1) b.Ifsc, b.Bank, b.Branch, b.Micr FROM ({MasterQueries.BankBranches}) b {ForThisCall}", OneRow("Ifsc", Key(ifsc), searchName: "Words"));
    }

    // Searched as it is typed, by the rule the FD system's own bank search goes by:
    // every word typed must be in the branch's search key, in any order; the
    // branches found are listed by MICR code, and at most BanksFound of them come
    // back. Nothing is asked until enough is typed (TypedSearch.FromLength). The
    // master is never sent to the page.
    public async Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default)
    {
        if (!TypedSearch.LongEnough(query)) return [];
        var words = Words(query, ' ', ',', '—', '-', '>', '(', ')');
        if (words.Length == 0) return [];
        var args = new DynamicParameters();
        args.Add("Ifsc", null, System.Data.DbType.AnsiString);
        args.Add("Words", Plain(string.Join(" ", words)));
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<BankBranch>($"""
            SELECT TOP ({BanksFound}) b.Ifsc, b.Bank, b.Branch, b.Micr FROM ({MasterQueries.BankBranches}) b
            ORDER BY b.Micr {ForThisCall}
            """, args)).ToList();
    }

    // ----- Axis CMS branches -----------------------------------------------------------
    // The Axis CMS master is not read whole either: a branch is searched as it is
    // typed, by its label - name, location and PIN code - and the one picked is kept
    // and looked up by its code.

    public async Task<IReadOnlyList<CmsLocation>> SearchCmsLocationsAsync(string query, CancellationToken ct = default)
    {
        if (!TypedSearch.LongEnough(query)) return [];
        var words = Words(query, ' ', ',', '—', '-');
        if (words.Length == 0) return [];
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<CmsLocation>($"""
            SELECT TOP ({Found}) c.Code, c.Name, c.Label FROM ({MasterQueries.CmsLocations}) c
            ORDER BY c.State, c.District {ForThisCall}
            """, Typed("Code", words))).ToList();
    }

    public async Task<CmsLocation?> CmsLocationAsync(string code, CancellationToken ct = default)
    {
        code = code.Trim();
        // A code is letters and digits: anything else was not picked from the search.
        if (code.Length == 0 || !code.All(char.IsAsciiLetterOrDigit)) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<CmsLocation>(
            $"SELECT TOP (1) c.Code, c.Name, c.Label FROM ({MasterQueries.CmsLocations}) c {ForThisCall}", OneRow("Code", Key(code)));
    }

    // ----- PIN codes ------------------------------------------------------------------

    public async Task<PinPlace?> PinCodeAsync(string pin, CancellationToken ct = default)
    {
        pin = pin.Trim();
        if (pin.Length != 6 || !pin.All(char.IsAsciiDigit)) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PinPlace>(
            MasterQueries.PinCodes, new { Pin = Key(pin) });
    }

    // ----- Reading a register: one row by its key, or a search ------------------------
    // A register's query takes both its key and @Search (MasterQueries). The app gives
    // one and leaves the other empty.

    // SQL Server is told to plan the read for this call's own values. With one of
    // the two parameters empty the query is really one of two different reads - a
    // row by its key, found by the index, or a search - and a plan kept from the
    // other kind would read the whole register to find one row.
    private const string ForThisCall = "OPTION (RECOMPILE)";

    // One row by its key: no search. The bank's query calls what was typed @Words.
    private static DynamicParameters OneRow(string keyName, object key, string searchName = "Search")
    {
        var args = new DynamicParameters();
        args.Add(keyName, key);
        args.Add(searchName, null, System.Data.DbType.AnsiString);
        return args;
    }

    // A search for what was typed: no key. The words are looked for in the order
    // typed, with anything between them: %word%word%.
    private static DynamicParameters Typed(string keyName, string[] words)
    {
        var args = new DynamicParameters();
        args.Add(keyName, null, System.Data.DbType.AnsiString);
        args.Add("Search", Plain("%" + string.Join("%", words) + "%"));
        return args;
    }

    // What was typed, as words. LIKE's own characters are left out of them, so a
    // word is only ever looked for as it reads.
    private static string[] Words(string query, params char[] separators) =>
        query.Replace("%", "").Replace("_", "").Replace("[", "")
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
