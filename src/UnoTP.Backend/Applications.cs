namespace UnoTP.Backend;

/// <summary>
/// The partner's applications: opening one, reading it back, saving what the
/// upload step holds against it, and the lists the console shows. Every call is
/// the asking partner's (see <see cref="IPartner"/>): an application belonging to
/// anybody else is never found.
/// </summary>
public interface IApplicationApi
{
    /// <summary>Opens a new application for the holder. The backend mints its number.</summary>
    Task<Application> OpenAsync(Holder holder, CancellationToken ct = default);

    /// <summary>The partner's application under this number - a draft among them - or null.</summary>
    Task<Application?> FindAsync(string appNo, CancellationToken ct = default);

    /// <summary>
    /// Saves the upload step against the application. Returns the application's
    /// new version, or null when it has changed since <paramref name="version"/>
    /// was read, in which case nothing is saved.
    /// </summary>
    Task<int?> SaveUploadAsync(string appNo, int version, UploadState upload, CancellationToken ct = default);

    /// <summary>The partner's saved applications that are theirs to finish, newest first.</summary>
    Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default);

    /// <summary>The partner's applications, for looking one up.</summary>
    Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default);
}

/// <summary>Who an application is for, as Investor Identification established it.</summary>
/// <param name="PanFiled">Set when a PAN copy is on the application already.</param>
/// <param name="Address">The address on record, or empty for an investor with none yet.</param>
/// <param name="OnRecord">What the folio already holds, for an application opened on one.</param>
public sealed record Holder(
    string Pan, string Dob, string Name, string Folio, bool PanFiled,
    string Address = "", DocsOnRecord? OnRecord = null);

/// <summary>One application: who it is for, and the upload step's state.</summary>
public sealed class Application
{
    public required string AppNo { get; init; }

    public required Holder Holder { get; init; }

    /// <summary>Moves on with every save; a save made against an older one is refused.</summary>
    public int Version { get; set; }

    /// <summary>The upload step, null until it is first saved.</summary>
    public UploadState? Upload { get; set; }

    /// <summary>Attempts on record from before the upload step was first opened, newest first.</summary>
    public List<LogEntry> Prior { get; init; } = [];
}

/// <summary>One application's upload step, as it is saved.</summary>
public sealed class UploadState
{
    // ----- What was typed or chosen
    public string AppType { get; set; } = "DIGITAL";
    public string PoaType { get; set; } = "";
    public string PayMode { get; set; } = "";
    public string Sourcing { get; set; } = "";
    public string SourceCode { get; set; } = "";
    public string SubBroker { get; set; } = "";
    public string Category { get; set; } = "";
    public string EmpCode { get; set; } = "";
    public string EmpCompany { get; set; } = "";
    public string EmpHolder { get; set; } = "";
    public string EmpRelation { get; set; } = "";
    public string EmpProofType { get; set; } = "";
    public string FormNo { get; set; } = "0000";

    /// <summary>A paper form's number, kept through a switch to digital and back.</summary>
    public string TypedFormNo { get; set; } = "";

    /// <summary>Set once the KYC is to come from CERSAI; it cannot be taken back.</summary>
    public bool Ckyc { get; set; }

    public DateTime? SavedAt { get; set; }

    // ----- Documents
    public Dictionary<string, StoredDoc> Docs { get; init; } = [];

    /// <summary>Refusals in a row per document; a copy the checks take clears it.</summary>
    public Dictionary<string, int> Attempts { get; init; } = [];

    /// <summary>What each copy was read to say, by the slot it came from.</summary>
    public Dictionary<string, ReadCard> Reads { get; init; } = [];

    /// <summary>Every attempt, newest first.</summary>
    public List<LogEntry> Log { get; init; } = [];

    public int AttemptsOf(string key) => Attempts.GetValueOrDefault(key);
}

/// <summary>
/// A document on the application. The copy itself is filed with the backend
/// (see <see cref="IDocumentApi"/>); this is what the step says about it.
/// <see cref="Before"/> is one that came over from the step before or the folio,
/// which has a name but no copy here to show.
/// </summary>
public sealed record StoredDoc(string FileName, long Size, string ContentType, string Check, string CheckKind, bool Before = false);

/// <summary>What a card below the documents says; <see cref="Reset"/> puts back how it opened.</summary>
public sealed class ReadCard
{
    public ReadCard()
    {
    }

    public ReadCard(string state, string lines, string from, string kind = "")
    {
        (State, Lines, From, Kind) = (state, lines, from, kind);
        Opened = new ReadFace(state, lines, from, kind);
    }

    public string State { get; set; } = "";
    public string Lines { get; set; } = "";
    public string From { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Was { get; set; } = "";

    /// <summary>How the card opened.</summary>
    public ReadFace Opened { get; set; } = new("", "", "", "");

    public void Reset()
    {
        // What a read replaced is on the card's own line: that goes back first.
        Lines = Was.Length > 0 ? Was : Opened.Lines;
        (State, From, Kind, Was) = (Opened.State, Opened.From, Opened.Kind, "");
    }
}

public sealed record ReadFace(string State, string Lines, string From, string Kind);

public sealed record LogStage(string Text, string Kind = "");

public sealed class LogEntry(string id, string document, int attempt, string at, string file)
{
    public string Id { get; } = id;
    public string Document { get; } = document;
    public int Attempt { get; } = attempt;
    public string At { get; } = at;
    public string File { get; } = file;
    public string Mark { get; set; } = "Checking";
    public string Kind { get; set; } = "";
    public List<LogStage> Stages { get; init; } = [];

    public void Add(string text, string kind = "") => Stages.Add(new LogStage(text, kind));

    public void End(string mark, string kind) => (Mark, Kind) = (mark, kind);
}

/// <summary>A saved application the partner can pick up again, its holder masked.</summary>
public sealed record DraftSummary(string AppNo, string Name, string Pan, string Dob, long Amount);

/// <summary>One application as the console lists it.</summary>
/// <param name="Folio">Null for a new customer until the deposit books.</param>
/// <param name="Fdr">The deposit receipt, once booked.</param>
/// <param name="Step">The wizard step an unfinished application stopped on.</param>
public sealed record ApplicationRecord(
    string AppNo,
    string? Folio,
    string Investor,
    string Pan,
    long Amount,
    bool Cumulative,
    int Months,
    string Payout,
    int Holders,
    DateTime Applied,
    bool Digital,
    string Instrument,
    string Branch,
    string State,
    string? Fdr,
    string Step);
