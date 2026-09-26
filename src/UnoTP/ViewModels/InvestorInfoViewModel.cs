using System.Text.RegularExpressions;
using UnoTP.Backend;

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
/// Investor Information's form as the backend keeps it: each holder's details under
/// their holder type, and the nominee's. The form posts them as "Holder{n}.Field"
/// and "Nominee.Field"; these carry them to the typed record and back.
/// </summary>
public static class InvestorDetailsForm
{
    private const string Yes = "on";

    /// <summary>What the form last posted, as the backend saves it.</summary>
    public static ApplicationDetails ToDetails(InvestorInfoState state)
    {
        string F(string name) => state.Fields.GetValueOrDefault(name, "").Trim();
        var details = new ApplicationDetails();
        // The holders the form posted fields for, in order: Holder1., Holder2., ...
        var posted = state.Fields.Keys
            .Select(k => k.StartsWith("Holder") && k.IndexOf('.') is > 6 and var dot && int.TryParse(k[6..dot], out var at) ? at : 0)
            .Where(n => n > 0).Distinct().Order();
        foreach (var n in posted)
        {
            var p = $"Holder{n}.";
            details.Holders.Add(new HolderDetails(
                InvestorInfoViewModel.CodeOf(n),
                F(p + "Gender"), F(p + "NameType"), F(p + "ParentName"),
                F(p + "AnnualIncome"), F(p + "Occupation"), F(p + "SubOccupation"), F(p + "MaritalStatus"),
                F(p + "Mobile"), F(p + "Email").ToUpperInvariant(),
                F(p + "FatcaTaxResident") is Yes or "true", F(p + "FatcaPermanentResident") is Yes or "true",
                F(p + "Pep"), F(p + "PepRelated")));
        }
        if (state.Nominee)
        {
            var (dd, mm, yyyy) = (F("Nominee.Dd"), F("Nominee.Mm"), F("Nominee.Yyyy"));
            details.Nominee = new NomineeDetails(
                F("Nominee.Name"),
                dd.Length + mm.Length + yyyy.Length == 0 ? "" : $"{dd.PadLeft(2, '0')}-{mm.PadLeft(2, '0')}-{yyyy}",
                F("Nominee.Relation"), F("Nominee.GuardianName"),
                F("Nominee.GuardianAddress.Line1"), F("Nominee.GuardianAddress.Line2"), F("Nominee.GuardianAddress.Line3"),
                F("Nominee.GuardianAddress.PinCode"), F("Nominee.GuardianAddress.City"));
        }
        return details;
    }

    /// <summary>The form as the backend last saved it, for a page opened afresh.</summary>
    public static void FromDetails(InvestorInfoState state, ApplicationDetails details)
    {
        state.Fields.Clear();
        foreach (var h in details.Holders)
        {
            var p = $"Holder{int.Parse(h.Holder)}.";
            void Set(string field, string value) { if (value.Length > 0) state.Fields[p + field] = value; }
            Set("Gender", h.Gender); Set("NameType", h.NameType); Set("ParentName", h.ParentName);
            Set("AnnualIncome", h.AnnualIncome); Set("Occupation", h.Occupation); Set("SubOccupation", h.SubOccupation);
            Set("MaritalStatus", h.MaritalStatus); Set("Mobile", h.Mobile); Set("Email", h.Email);
            if (h.FatcaTaxResident) Set("FatcaTaxResident", Yes);
            if (h.FatcaPermanentResident) Set("FatcaPermanentResident", Yes);
            Set("Pep", h.Pep); Set("PepRelated", h.PepRelated);
        }
        state.Nominee = details.Nominee is not null;
        if (details.Nominee is { } n)
        {
            var dob = n.Dob.Split('-');
            state.Fields["Nominee.Name"] = n.Name;
            state.Fields["Nominee.Dd"] = dob.ElementAtOrDefault(0) ?? "";
            state.Fields["Nominee.Mm"] = dob.ElementAtOrDefault(1) ?? "";
            state.Fields["Nominee.Yyyy"] = dob.ElementAtOrDefault(2) ?? "";
            state.Fields["Nominee.Relation"] = n.Relation;
            state.Fields["Nominee.GuardianName"] = n.GuardianName;
            state.Fields["Nominee.GuardianAddress.Line1"] = n.GuardianLine1;
            state.Fields["Nominee.GuardianAddress.Line2"] = n.GuardianLine2;
            state.Fields["Nominee.GuardianAddress.Line3"] = n.GuardianLine3;
            state.Fields["Nominee.GuardianAddress.PinCode"] = n.GuardianPinCode;
            state.Fields["Nominee.GuardianAddress.City"] = n.GuardianCity;
        }
    }
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
    /// <summary>Joint holders the backend's rules allow beside the investor.</summary>
    public int MaxJoint => Docs.Config.MaxJointHolders;

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

    /// <summary>The age under which a nominee is a minor, and needs a guardian named: the backend's minimum age.</summary>
    public int MinorUnder => Docs.Config.MinAge;

    /// <summary>
    /// Whether the nominee's date of birth, as last posted, makes them a minor. Only
    /// then is a guardian asked for; with no whole date yet, nobody is.
    /// </summary>
    public bool NomineeMinor => IsMinor(Value("Nominee.Dd"), Value("Nominee.Mm"), Value("Nominee.Yyyy"), DateTime.Today, MinorUnder);

    public static bool IsMinor(string dd, string mm, string yyyy, DateTime today, int minorUnder)
    {
        if (!int.TryParse(dd, out var d) || !int.TryParse(mm, out var m) || !int.TryParse(yyyy, out var y)
            || y < 1900 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return false;
        var born = new DateTime(y, m, d);
        return born <= today && born.AddYears(minorUnder) > today;
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

    /// <summary>What Proceed found missing or wrong, by field name ("Holder2.Pep", "Nominee.Dob").</summary>
    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();

    /// <summary>What Proceed said about a field, if anything.</summary>
    public string? ErrorOf(string name) => Errors.GetValueOrDefault(name);

    /// <summary>
    /// What every added holder types or chooses, in the order the page draws it: the
    /// field, the id the page gives its control, and what an empty one says. The
    /// gender is asked only where nothing read it, so is checked only when posted.
    /// </summary>
    private static readonly (string Field, string Id, string Empty)[] HolderFields =
    [
        ("Gender", "h{0}-gender", "Select the gender"),
        ("NameType", "h{0}-nametype", "Select the name type"),
        ("ParentName", "h{0}-parent", "Enter the father's, mother's or spouse's name"),
        ("AnnualIncome", "cii{0}Income", "Select the annual income"),
        ("Occupation", "cii{0}Occupation", "Select the occupation"),
        ("SubOccupation", "cii{0}SubOccupation", "Select the sub occupation"),
        ("MaritalStatus", "cii{0}Marital", "Select the marital status"),
        ("Mobile", "cii{0}Mobile", "Enter the mobile number"),
        ("Email", "cii{0}Email", "Enter the e-mail"),
    ];

    /// <summary>
    /// Everything Proceed cannot go on without, in page order: each added holder's
    /// fields and PEP answers, then the nominee's, and a minor nominee's guardian's.
    /// Each comes with the id of the control to bring the partner to.
    /// </summary>
    public static List<(string Field, string Id, string Error)> Unfilled(InvestorInfoState state, IEnumerable<(int Holder, UploadDocumentsViewModel.DocHolder Who)> holders, int minorUnder)
    {
        var found = new List<(string, string, string)>();
        var fields = state.Fields;
        string Of(string name) => fields.GetValueOrDefault(name)?.Trim() ?? "";
        void Need(string name, string id, string empty, string? wrong = null, Func<string, bool>? valid = null)
        {
            var value = Of(name);
            if (value.Length == 0) found.Add((name, id, empty));
            else if (valid is not null && !valid(value)) found.Add((name, id, wrong!));
        }

        foreach (var (holder, who) in holders)
        {
            foreach (var (field, id, empty) in HolderFields)
            {
                var name = $"Holder{holder}.{field}";
                if (field == "Gender" && !fields.ContainsKey(name)) continue;
                var at = string.Format(id, holder);
                if (field == "Mobile") Need(name, at, empty, "Enter a 10-digit mobile number", v => Regex.IsMatch(v, @"^[6-9]\d{9}$"));
                else if (field == "Email") Need(name, at, empty, "Enter a valid e-mail", v => Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));
                else Need(name, at, empty);
            }
            if (!PepAsked(who)) continue;
            foreach (var (field, _) in PepQuestions)
            {
                var name = $"Holder{holder}.{field}";
                if (Of(name) is not ("yes" or "no")) found.Add((name, "pep-" + name, "Choose Yes or No"));
            }
        }

        if (!state.Nominee) return found;
        Need("Nominee.Name", "ciiNomName", "Enter the nominee's name");
        var (dd, mm, yyyy) = (Of("Nominee.Dd"), Of("Nominee.Mm"), Of("Nominee.Yyyy"));
        if (dd.Length == 0 || mm.Length == 0 || yyyy.Length == 0) found.Add(("Nominee.Dob", "ciiNomDd", "Enter the nominee's date of birth"));
        else if (!IsDate(dd, mm, yyyy, DateTime.Today)) found.Add(("Nominee.Dob", "ciiNomDd", "Enter a real date of birth, not a future one"));
        Need("Nominee.Relation", "ciiNomRelation", "Select the relation with the primary holder");
        if (IsMinor(dd, mm, yyyy, DateTime.Today, minorUnder))
        {
            Need("Nominee.GuardianName", "ciiNomGuardian", "Enter the guardian's name");
            Need("Nominee.GuardianAddress.Line1", "ciiGdn1", "Enter the first line of the address");
            Need("Nominee.GuardianAddress.PinCode", "ciiGdnPin", "Enter the PIN code", "Enter a 6-digit PIN code", v => Regex.IsMatch(v, @"^[1-9]\d{5}$"));
            Need("Nominee.GuardianAddress.City", "ciiGdnCity", "Enter the city");
        }
        return found;
    }

    private static bool IsDate(string dd, string mm, string yyyy, DateTime today) =>
        int.TryParse(dd, out var d) && int.TryParse(mm, out var m) && int.TryParse(yyyy, out var y)
        && y >= 1900 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m)
        && new DateTime(y, m, d) <= today;

    /// <summary>The part of the page the last post was about.</summary>
    public string? Focus { get; init; }
}
