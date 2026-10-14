using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// The big tables the purchase journey looks rows up in (db/create_new_tables.sql): the
/// investors on record, the sourcing registers (brokers, staff), bank branches and
/// PIN codes. The rate card is in SqlMasters.RateCard.cs.
/// </summary>
public sealed partial class SqlMasters(Db db, SqlReference reference) : IInvestorApi, ISourcingApi, IDepositApi, IPlaceApi
{
    // How many rows a search gives back is in its own query: 20 for a register and the Axis
    // CMS branches, 15 for bank branches, as the FD system's own bank search gives.

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

    // One row of InvestorIdentificationQueries.KycOnCommon or KycOnBt, as this reads it.
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
        FolioDepositsAsync(InvestorIdentificationQueries.FolioDepositsByPan, new { Pan = Key(pan) }, ct);

    public Task<IReadOnlyList<FolioDeposit>> FolioDepositsByFolioAsync(string folio, CancellationToken ct = default) =>
        FolioDepositsAsync(InvestorIdentificationQueries.FolioDepositsByFolio, new { Folio = Key(folio) }, ct);

    public async Task<string> FolioOfFirstHolderAsync(string pan, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var folio = await connection.QueryFirstOrDefaultAsync<string>(
            InvestorIdentificationQueries.FolioOfFirstHolder,
            new { Pan = Key(pan) });
        return (folio ?? "").Trim();
    }

    public async Task<IReadOnlyList<ActiveDeposit>> ActiveDepositsAsync(string pan, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Number, decimal? Amount)>(
            FdConfigurationQueries.ActiveDepositAmounts, new { Pan = Key(pan) });
        return rows.Select(r => new ActiveDeposit((r.Number ?? "").Trim(), (long)Math.Round(r.Amount ?? 0))).ToList();
    }

    // The deposits the folio check looks at, for one PAN or one folio: the query
    // carries the key it reads by.
    private async Task<IReadOnlyList<FolioDeposit>> FolioDepositsAsync(string deposits, object key, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string Folio, string Pan, DateTime? Dob)>(
            deposits, key);
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
        var query = NamePrefixQuery(source);
        if (query is null) return "";
        await using var connection = await db.OpenAsync(ct);
        var prefix = await connection.QueryFirstOrDefaultAsync<string>(query, holder);
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
        var source = await connection.QueryFirstOrDefaultAsync<string>(InvestorIdentificationQueries.KycSource,
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
            InvestorIdentificationQueries.Folios, new { Folio = Key(folio), Pan = Key(pan) });
    }

    // The details held for a holder of a folio, read from where their latest KYC is
    // kept: the latest row that source's query gives for the folio, PAN and date of
    // birth. The marital status is kept as its code, and given as the name the page offers.
    public async Task<HolderDetails?> KycOnFolioAsync(string source, string folio, string pan, string dob, CancellationToken ct = default)
    {
        var query = LatestKycQuery(source);
        if (query is null || folio.Trim().Length == 0) return null;

        var maritalStatuses = ((await reference.ReferenceAsync(ct)).Masters ?? MasterLists.None).MaritalStatuses;
        await using var connection = await db.OpenAsync(ct);
        var row = await connection.QueryFirstOrDefaultAsync<FolioKycRow>(
            query,
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

    // The query that reads a holder's latest KYC row from a source; null for the folio
    // master, which holds none.
    private static string? LatestKycQuery(string source)
    {
        if (source == KycSources.Common) return InvestorInformationQueries.LatestKycOnCommon;
        if (source == KycSources.Bt) return InvestorInformationQueries.LatestKycOnBt;
        return null;
    }

    // The query that reads the name prefix on a holder's latest KYC row from a source;
    // null for the folio master.
    private static string? NamePrefixQuery(string source)
    {
        if (source == KycSources.Common) return InvestorIdentificationQueries.NamePrefixOnCommon;
        if (source == KycSources.Bt) return InvestorIdentificationQueries.NamePrefixOnBt;
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
            query,
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

    // The query that reads a holder's latest permanent address from a source; null for the folio master.
    private static string? AddressQuery(string source)
    {
        if (source == KycSources.Common) return InvestorIdentificationQueries.AddressOnCommon;
        if (source == KycSources.Bt) return InvestorIdentificationQueries.AddressOnBt;
        return null;
    }

    // Whether the photograph and a proof of address are on record at the source for
    // the holder, told by the sub-type each is filed under. A document that is there
    // counts, verified or not. Null for the folio master, which says so itself. No
    // other document is looked for: the PAN copy is never asked of a holder on a folio.
    private async Task<(bool Photo, bool Poa)?> DocumentsOnFolioAsync(string source, FolioHolder holder, CancellationToken ct)
    {
        var query = DocumentSubTypesQuery(source);
        if (query is null) return null;

        await using var connection = await db.OpenAsync(ct);
        var subTypes = await connection.QueryAsync<string>(query, holder);
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
        var query = DocumentCopiesQuery(holder.Source);
        if (query is null || holder.Folio.Length == 0) return [];

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string SubTypeCode, string FileName, string FilePath)>(
            query,
            HolderOf(holder.Folio, holder.Pan, holder.Dob));
        return rows.Select(r => new CopyOnRecord((r.SubTypeCode ?? "").Trim(), r.FileName, r.FilePath)).ToList();
    }

    // The query that reads the sub types of a source's documents; null for the folio master.
    private static string? DocumentSubTypesQuery(string source)
    {
        if (source == KycSources.Common) return InvestorIdentificationQueries.DocumentSubTypesOnCommon;
        if (source == KycSources.Bt) return InvestorIdentificationQueries.DocumentSubTypesOnBt;
        return null;
    }

    // The query that reads the copies a source holds; null for the folio master.
    private static string? DocumentCopiesQuery(string source)
    {
        if (source == KycSources.Common) return InvestorIdentificationQueries.DocumentCopiesOnCommon;
        if (source == KycSources.Bt) return InvestorIdentificationQueries.DocumentCopiesOnBt;
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
        var rows = await connection.QueryAsync<(string Name, DateTime? Dob, string Relation, string GuardianName, string AppNo)>(InvestorInformationQueries.NomineesOnDeposit, new { Folio = folio.Trim(), DepositNumber = depositNumber.Trim() });
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
        var rows = await connection.QueryAsync<(string Ifsc, string AccountNo, string Bank, string Branch, string AppNo)>(BankDetailsQueries.AccountsOnDeposit, new { Folio = folio.Trim(), DepositNumber = depositNumber.Trim() });
        foreach (var r in rows)
        {
            if (found.Any(f => f.Ifsc == r.Ifsc && f.AccountNumber == r.AccountNo)) continue;
            found.Add(new AccountOnRecord(r.Ifsc, r.AccountNo, r.Bank, r.Branch, r.AppNo));
        }
        return found;
    }

    // ----- Sourcing registers ------------------------------------------------------

    // The brokers' and the staff's own queries are in UploadDocumentsQueries. Both registers are
    // big (the staff one has tens of thousands of rows), so neither is ever read whole:
    // a code is looked up by itself, and a search asks only for what was typed.

    public Task<Party?> BrokerAsync(string code, CancellationToken ct = default) =>
        PartyAsync(UploadDocumentsQueries.Broker, code, ct);

    public Task<Party?> StaffMemberAsync(string code, string staffRule, CancellationToken ct = default) =>
        PartyAsync(StaffMemberQuery(staffRule), code, ct);

    public Task<IReadOnlyList<Party>> SearchBrokersAsync(string query, CancellationToken ct = default) =>
        SearchPartiesAsync(UploadDocumentsQueries.SearchBrokers, "brokers", query, ct);

    public Task<IReadOnlyList<Party>> SearchStaffAsync(string query, string staffRule, CancellationToken ct = default) =>
        SearchPartiesAsync(SearchStaffQuery(staffRule), "staff", query, ct);

    // The query for the one employee a code names, among the staff a sourcing mode takes:
    // by the rule its row names; any employee in service for a mode that names none.
    private static string StaffMemberQuery(string staffRule)
    {
        if (staffRule == "branch") return UploadDocumentsQueries.BranchStaffMember;
        if (staffRule == "mfis") return UploadDocumentsQueries.MfisStaffMember;
        if (staffRule == "mflEx") return UploadDocumentsQueries.MflExStaffMember;
        return UploadDocumentsQueries.StaffMember;
    }

    // The query that searches the staff a sourcing mode takes, by the same rule.
    private static string SearchStaffQuery(string staffRule)
    {
        if (staffRule == "branch") return UploadDocumentsQueries.SearchBranchStaff;
        if (staffRule == "mfis") return UploadDocumentsQueries.SearchMfisStaff;
        if (staffRule == "mflEx") return UploadDocumentsQueries.SearchMflExStaff;
        return UploadDocumentsQueries.SearchStaff;
    }

    // The one row a code names, or null. oneParty is the register's query for it.
    private async Task<Party?> PartyAsync(string oneParty, string code, CancellationToken ct)
    {
        code = code.Trim();
        if (code.Length == 0) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<Party>(
            oneParty, OneRow("Code", Key(code)));
    }

    // The parties whose code and name hold what was typed, in the order typed; a code that
    // starts with the first word comes first. Nothing is asked until enough is typed for
    // the register (TypedSearch.FromLengthOf): three characters for brokers, five for staff.
    private async Task<IReadOnlyList<Party>> SearchPartiesAsync(string searchParties, string register, string query, CancellationToken ct)
    {
        if (!TypedSearch.LongEnough(query, register)) return [];
        var words = Words(query, ' ');
        if (words.Length == 0) return [];
        var args = Typed("Code", words);
        args.Add("First", Plain(words[0] + "%"));
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<Party>(searchParties, args)).ToList();
    }

    // ----- Bank branches ------------------------------------------------------------

    public async Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<BankBranch>(
            BankDetailsQueries.BankBranch, OneRow("Ifsc", Key(ifsc), searchName: "Words"));
    }

    // Searched as it is typed, by the rule the FD system's own bank search goes by:
    // every word typed must be in the branch's search key, in any order; the
    // branches found are listed by MICR code, and at most 15 of them come
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
        return (await connection.QueryAsync<BankBranch>(BankDetailsQueries.SearchBankBranches, args)).ToList();
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
        return (await connection.QueryAsync<CmsLocation>(BankDetailsQueries.SearchCmsLocations, Typed("Code", words))).ToList();
    }

    public async Task<CmsLocation?> CmsLocationAsync(string code, CancellationToken ct = default)
    {
        code = code.Trim();
        // A code is letters and digits: anything else was not picked from the search.
        if (code.Length == 0 || !code.All(char.IsAsciiLetterOrDigit)) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<CmsLocation>(
            BankDetailsQueries.CmsLocation, OneRow("Code", Key(code)));
    }

    // ----- PIN codes ------------------------------------------------------------------

    public async Task<PinPlace?> PinCodeAsync(string pin, CancellationToken ct = default)
    {
        pin = pin.Trim();
        if (pin.Length != 6 || !pin.All(char.IsAsciiDigit)) return null;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PinPlace>(
            InvestorInformationQueries.PinCodes, new { Pin = Key(pin) });
    }

    // ----- Reading a register: one row by its key, or a search ------------------------
    // A register's query takes both its key and @Search (UploadDocumentsQueries, BankDetailsQueries). The app gives
    // one and leaves the other empty.

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
