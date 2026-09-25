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
/// can only be opened once the second is added.
/// </summary>
public sealed class InvestorInfoViewModel(InvestorInfoState state)
{
    /// <summary>A second and a third holder, beside the investor.</summary>
    public const int MaxJoint = 2;

    /// <summary>The investor this page shows, whose PAN a joint holder cannot repeat.</summary>
    public const string PrimaryPan = "BISPM4465N";

    /// <summary>What the old screen says when a holder answers Yes to a FATCA question.</summary>
    public const string FatcaOffline =
        "This investments needs to be done through offline mode. Kindly reach out to the nearest Mahindra branch. A list of all our branches is available on our website.";

    public static string Ordinal(int holder) => holder == 2 ? "Second" : "Third";

    public InvestorInfoState State { get; } = state;

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

    /// <summary>Set when a holder answered Yes to a FATCA question and Proceed was pressed.</summary>
    public bool Offline { get; init; }

    /// <summary>The joint holder Proceed stopped at, not yet added.</summary>
    public int? Unfinished { get; init; }

    /// <summary>The part of the page the last post was about.</summary>
    public string? Focus { get; init; }
}
