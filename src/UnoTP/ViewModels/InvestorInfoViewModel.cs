namespace UnoTP.ViewModels;

/// <summary>
/// Investor Information as the session holds it between a post and the page after
/// it: what the page's form last posted, and each joint holder's identification.
/// </summary>
public sealed class InvestorInfoState
{
    /// <summary>Every field the page's form last posted, by name; files aside.</summary>
    public Dictionary<string, string> Fields { get; init; } = [];

    /// <summary>The joint holders opened, in order: the second holder, then the third.</summary>
    public List<SearchState> Joint { get; init; } = [];

    public bool Nominee { get; set; }
}

/// <summary>
/// Investor Information, the old WA_FD_UNOTP/InvestorInformation: a card for the
/// investor, one for each joint holder, and one for the nominee. A joint holder goes
/// through Investor Identification's workflow on their card, by PAN and date of birth
/// (see <see cref="HolderSearch"/>), and is added once identified; the third holder
/// can only be opened once the second is added. Once added, they file their PAN copy,
/// photograph and proof of address the way the investor does on Upload Documents,
/// through the same model (<see cref="Docs"/>).
/// </summary>
public sealed class InvestorInfoViewModel(InvestorInfoState state, UploadDocumentsViewModel? docs)
{
    /// <summary>A second and a third holder, beside the investor.</summary>
    public const int MaxJoint = 2;

    /// <summary>What the old screen says when a FATCA question - the investor's card asks them for every holder - is answered Yes.</summary>
    public const string FatcaOffline =
        "This investments needs to be done through offline mode. Kindly reach out to the nearest Mahindra branch. A list of all our branches is available on our website.";

    public static string Ordinal(int holder) => holder == 2 ? "Second" : "Third";

    /// <summary>The holder type a joint holder's documents are filed under: 02 for the second, 03 for the third.</summary>
    public static string CodeOf(int holder) => holder.ToString("00");

    public InvestorInfoState State { get; } = state;

    /// <summary>The application, and the joint holders' documents on it. Left out only
    /// where the model is asked a question and the page is not drawn.</summary>
    public UploadDocumentsViewModel Docs { get; } = docs!;

    /// <summary>Each joint holder's identification, checked from the session, in order.</summary>
    public List<InvestorIdentificationViewModel> Joint { get; } = [];

    /// <summary>What a field last held, or what it opens with before anything is posted.</summary>
    public string Value(string name, string opening = "") =>
        State.Fields.TryGetValue(name, out var value) ? value : State.Fields.Count == 0 ? opening : "";

    /// <summary>Whether a switch is on.</summary>
    public bool On(string name) => State.Fields.TryGetValue(name, out var value) && value is "on" or "true";

    /// <summary>
    /// Another joint holder can be opened: there is room, and every one opened is
    /// completely added. Until the second is, the question is not asked at all.
    /// </summary>
    public bool CanAddJoint => State.Joint.Count < MaxJoint && State.Joint.All(j => j.Added);

    /// <summary>The age under which a nominee is a minor, and needs a guardian named.</summary>
    public const int MinorUnder = 18;

    /// <summary>
    /// Whether the nominee's date of birth, as last posted, makes them a minor. Only
    /// then is a guardian asked for; with no whole date yet, nobody is.
    /// </summary>
    public bool NomineeMinor => IsMinor(Value("Nominee.Dd"), Value("Nominee.Mm"), Value("Nominee.Yyyy"), DateTime.Today);

    public static bool IsMinor(string dd, string mm, string yyyy, DateTime today)
    {
        if (!int.TryParse(dd, out var d) || !int.TryParse(mm, out var m) || !int.TryParse(yyyy, out var y)
            || y < 1900 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return false;
        var born = new DateTime(y, m, d);
        return born <= today && born.AddYears(MinorUnder) > today;
    }

    /// <summary>Set when a FATCA question was answered Yes and Proceed was pressed.</summary>
    public bool Offline { get; init; }

    /// <summary>The joint holder Proceed stopped at, not yet added.</summary>
    public int? Unfinished { get; init; }

    /// <summary>The two questions every holder with no folio answers, by field and wording.</summary>
    public static readonly (string Field, string Question)[] PepQuestions =
    [
        ("Pep", "Are you a politically exposed person (PEP)?"),
        ("PepRelated", "Are you related to a PEP?"),
    ];

    /// <summary>
    /// Whether a holder is asked the PEP questions: only one with no folio yet. A
    /// holder on a folio has answered them already.
    /// </summary>
    public static bool PepAsked(UploadDocumentsViewModel.DocHolder h) => h.Who.Folio.Length == 0;

    /// <summary>The PEP answers Proceed found missing, by field name ("Holder2.Pep").</summary>
    public IReadOnlySet<string> PepMissing { get; init; } = new HashSet<string>();

    /// <summary>The PEP answers still to give, by field name, for every holder asked.</summary>
    public static List<string> PepUnanswered(InvestorInfoState state, IEnumerable<(int Holder, UploadDocumentsViewModel.DocHolder Who)> holders) =>
        [.. from h in holders
            where PepAsked(h.Who)
            from q in PepQuestions
            let field = $"Holder{h.Holder}.{q.Field}"
            where state.Fields.GetValueOrDefault(field) is not ("yes" or "no")
            select field];

    /// <summary>The part of the page the last post was about.</summary>
    public string? Focus { get; init; }
}
