using System.Text.RegularExpressions;
using UnoTP.Models;
using UnoTP.Services;

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

    /// <summary>The fields a typed communication address posts as, after "Holder{n}.".</summary>
    public const string Comm = "Comm.";

    /// <summary>
    /// What the form last posted, as the backend saves it. A communication address
    /// typed carries the district and state <paramref name="places"/> gives its PIN
    /// code - the backend's, never what a browser sent.
    /// </summary>
    public static ApplicationDetails ToDetails(InvestorInfoState state, IReadOnlyDictionary<string, PinPlace>? places = null)
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
                InvestorViewModel.CodeOf(n),
                F(p + "Gender"), F(p + "NameType"), F(p + "ParentName"),
                F(p + "AnnualIncome"), F(p + "Occupation"), F(p + "SubOccupation"), F(p + "MaritalStatus"),
                F(p + "Mobile"), F(p + "Email").ToUpperInvariant(),
                F(p + "FatcaTaxResident") is Yes or "true", F(p + "FatcaPermanentResident") is Yes or "true",
                F(p + "Pep"), F(p + "PepRelated"),
                Typed(p + Comm)));
        }
        // Only a holder whose address fields were posted - their post goes elsewhere
        // and nothing proves it - has one typed.
        TypedAddress? Typed(string c)
        {
            if (!state.Fields.ContainsKey(c + "Line1")) return null;
            var pin = F(c + "PinCode");
            var place = places?.GetValueOrDefault(pin);
            return new TypedAddress(F(c + "Line1"), F(c + "Line2"), F(c + "Line3"), F(c + "City"), pin,
                place?.District ?? "", place?.State ?? "");
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
            if (h.Communication is { } c)
            {
                state.Fields[p + Comm + "Line1"] = c.Line1;
                Set(Comm + "Line2", c.Line2); Set(Comm + "Line3", c.Line3);
                Set(Comm + "City", c.City); Set(Comm + "PinCode", c.PinCode);
            }
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
public sealed class InvestorViewModel(InvestorInfoState state, DocumentsViewModel? docs)
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
    public DocumentsViewModel Docs { get; } = docs!;

    /// <summary>Each joint holder's identification, checked from the session, in order.</summary>
    public List<NewApplicationViewModel> Joint { get; } = [];

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

    /// <summary>Proceed stopped: the category on Upload Documents is a women's one and the applicant is male. Corrected there.</summary>
    public string? CategoryConflict { get; init; }

    /// <summary>The nominees on record against the investor's folio, to fill the nominee's fields from; empty without a folio.</summary>
    public IReadOnlyList<NomineeOnRecord> NomineesOnRecord { get; init; } = [];

    /// <summary>Proceed stopped: name screening does not allow these holders (their names, a comma between) to invest online.</summary>
    public string? ScreeningNotAllowed { get; init; }

    /// <summary>
    /// What stopped Proceed, said as an error the partner can act on: what is wrong,
    /// what to do about it, and where. Null while nothing has.
    /// </summary>
    public StopNotice? Stopped
    {
        get
        {
            if (CategoryConflict is not null)
            {
                return new StopNotice("Cannot proceed: the deposit category does not fit the applicant",
                    CategoryConflict,
                    "Open Upload Documents, choose a category that is not a women's one, and come back to Proceed.",
                    "Go to Upload Documents", "Documents");
            }
            if (ScreeningNotAllowed is not null)
            {
                return new StopNotice("Cannot proceed: not allowed to invest online",
                    $"Name screening does not allow {ScreeningNotAllowed} to invest online.",
                    "This investment cannot be made here. Kindly ask the investor to visit the nearest Mahindra Finance branch to invest offline; a list of all branches is on our website.",
                    null, null);
            }
            if (Offline)
            {
                return new StopNotice("Cannot proceed: the investment has to be made offline",
                    "A holder is a tax or permanent resident of a country other than India.",
                    "Kindly ask the investor to visit the nearest Mahindra Finance branch to invest offline; a list of all branches is on our website.",
                    null, null);
            }
            return null;
        }
    }

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
    public static bool PepAsked(DocumentsViewModel.DocHolder h) => h.Who.Folio.Length == 0;

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
        ("AnnualIncome", "investor{0}Income", "Select the annual income"),
        ("Occupation", "investor{0}Occupation", "Select the occupation"),
        ("SubOccupation", "investor{0}SubOccupation", "Select the sub occupation"),
        ("MaritalStatus", "investor{0}Marital", "Select the marital status"),
        ("Mobile", "investor{0}Mobile", "Enter the mobile number"),
        ("Email", "investor{0}Email", "Enter the e-mail"),
    ];

    /// <summary>
    /// Everything Proceed cannot go on without, in page order: each added holder's
    /// card (<see cref="UnfilledFor"/>), then the nominee's, and a minor nominee's
    /// guardian's. Each comes with the id of the control to bring the partner to.
    /// </summary>
    public static List<(string Field, string Id, string Error)> Unfilled(InvestorInfoState state, IEnumerable<(int Holder, DocumentsViewModel.DocHolder Who)> holders, int minorUnder,
        Func<DocumentsViewModel.DocHolder, bool>? typesMail = null, IReadOnlyDictionary<string, PinPlace>? places = null,
        Func<string, IReadOnlyList<string>>? subOccupationsFor = null)
    {
        var found = new List<(string Field, string Id, string Error)>();
        foreach (var (holder, who) in holders) found.AddRange(UnfilledFor(state, holder, who, typesMail, places, subOccupationsFor));
        if (!state.Nominee) return found;

        var need = new Needs(state.Fields, found);
        need.Need("Nominee.Name", "investorNomineeName", "Enter the nominee's name", LettersOnly, IsName);
        need.AtMost("Nominee.Name", "investorNomineeName", InputRules.MaxField);
        var (dd, mm, yyyy) = (need.Of("Nominee.Dd"), need.Of("Nominee.Mm"), need.Of("Nominee.Yyyy"));
        if (dd.Length == 0 || mm.Length == 0 || yyyy.Length == 0) found.Add(("Nominee.Dob", "investorNomineeDd", "Enter the nominee's date of birth"));
        else if (!IsDate(dd, mm, yyyy, DateTime.Today)) found.Add(("Nominee.Dob", "investorNomineeDd", "Enter a real date of birth, not a future one"));
        need.Need("Nominee.Relation", "investorNomineeRelation", "Select the relation with the primary holder");
        if (IsMinor(dd, mm, yyyy, DateTime.Today, minorUnder))
        {
            need.Need("Nominee.GuardianName", "investorNomineeGuardian", "Enter the guardian's name", LettersOnly, IsName);
            need.Need("Nominee.GuardianAddress.Line1", "investorGuardian1", "Enter the first line of the address", NoSpecialCharacters, IsText);
            need.Shape("Nominee.GuardianAddress.Line2", "investorGuardian2", NoSpecialCharacters, IsText);
            need.Shape("Nominee.GuardianAddress.Line3", "investorGuardian3", NoSpecialCharacters, IsText);
            need.Need("Nominee.GuardianAddress.PinCode", "investorGuardianPin", "Enter the PIN code", "Enter a 6-digit PIN code", IsPin);
            need.Need("Nominee.GuardianAddress.City", "investorGuardianCity", "Enter the city", LettersOnly, IsName);
            need.AtMost("Nominee.GuardianName", "investorNomineeGuardian", InputRules.MaxField);
            need.AtMost("Nominee.GuardianAddress.Line1", "investorGuardian1", InputRules.MaxAddressLine);
            need.AtMost("Nominee.GuardianAddress.Line2", "investorGuardian2", InputRules.MaxAddressLine);
            need.AtMost("Nominee.GuardianAddress.Line3", "investorGuardian3", InputRules.MaxAddressLine);
            need.AtMost("Nominee.GuardianAddress.City", "investorGuardianCity", InputRules.MaxField);
        }
        return found;
    }

    /// <summary>
    /// What one added holder's card still lacks, in page order: a communication
    /// address typed, what nothing read, More Information, and for a holder with no
    /// folio the PEP answers. Empty once the card is complete.
    /// </summary>
    /// <param name="subOccupationsFor">The sub occupations an occupation offers; an occupation with none asks for no sub occupation.</param>
    public static List<(string Field, string Id, string Error)> UnfilledFor(InvestorInfoState state, int holder, DocumentsViewModel.DocHolder who,
        Func<DocumentsViewModel.DocHolder, bool>? typesMail = null, IReadOnlyDictionary<string, PinPlace>? places = null,
        Func<string, IReadOnlyList<string>>? subOccupationsFor = null)
    {
        var found = new List<(string Field, string Id, string Error)>();
        var need = new Needs(state.Fields, found);
        if (typesMail?.Invoke(who) == true)
        {
            var c = $"Holder{holder}.{InvestorDetailsForm.Comm}";
            need.Need(c + "Line1", $"h{holder}-comm1", "Enter the first line of the address", NoSpecialCharacters, IsText);
            need.Shape(c + "Line2", $"h{holder}-comm2", NoSpecialCharacters, IsText);
            need.Shape(c + "Line3", $"h{holder}-comm3", NoSpecialCharacters, IsText);
            need.Need(c + "City", $"h{holder}-commcity", "Enter the city", LettersOnly, IsName);
            need.AtMost(c + "Line1", $"h{holder}-comm1", InputRules.MaxAddressLine);
            need.AtMost(c + "Line2", $"h{holder}-comm2", InputRules.MaxAddressLine);
            need.AtMost(c + "Line3", $"h{holder}-comm3", InputRules.MaxAddressLine);
            need.AtMost(c + "City", $"h{holder}-commcity", InputRules.MaxField);
            need.Need(c + "PinCode", $"h{holder}-commpin", "Enter the PIN code", "Enter a 6-digit PIN code", IsPin);
            if (IsPin(need.Of(c + "PinCode")) && places?.ContainsKey(need.Of(c + "PinCode")) == false)
                found.Add((c + "PinCode", $"h{holder}-commpin", "No district is found for this PIN code; check it"));
        }
        foreach (var (field, id, empty) in HolderFields)
        {
            var name = $"Holder{holder}.{field}";
            if (field == "Gender" && !state.Fields.ContainsKey(name)) continue;
            var at = string.Format(id, holder);
            // The sub occupation goes with the occupation: one of those offered for it, or none where it offers none.
            if (field == "SubOccupation" && subOccupationsFor is not null)
            {
                var offered = subOccupationsFor(need.Of($"Holder{holder}.Occupation"));
                if (offered.Count == 0) continue;
                need.Need(name, at, empty, "Select a sub occupation that goes with the occupation", v => offered.Contains(v));
                continue;
            }
            if (field == "Mobile") need.Need(name, at, empty, "Enter a 10-digit mobile number", v => Regex.IsMatch(v, @"^[6-9]\d{9}$"));
            else if (field == "Email") need.Need(name, at, empty, "Enter a valid e-mail", v => Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));
            else if (field == "ParentName") need.Need(name, at, empty, LettersOnly, IsName);
            else need.Need(name, at, empty);
            if (field is "ParentName" or "Email") need.AtMost(name, at, InputRules.MaxField);
        }
        if (!PepAsked(who)) return found;
        foreach (var (field, _) in PepQuestions)
        {
            var name = $"Holder{holder}.{field}";
            if (need.Of(name) is not ("yes" or "no")) found.Add((name, "pep-" + name, "Choose Yes or No"));
        }
        return found;
    }

    // A field that has to hold something, and hold it rightly.
    private sealed class Needs(Dictionary<string, string> fields, List<(string Field, string Id, string Error)> found)
    {
        /// <summary>A posted field's value, trimmed ("" when missing).</summary>
        public string Of(string name) => fields.GetValueOrDefault(name)?.Trim() ?? "";

        /// <summary>Records an error for a field that is empty, or filled in but not valid.</summary>
        public void Need(string name, string id, string empty, string? wrong = null, Func<string, bool>? valid = null)
        {
            var value = Of(name);
            if (value.Length == 0) found.Add((name, id, empty));
            else if (valid is not null && !valid(value)) found.Add((name, id, wrong!));
        }

        /// <summary>A field that may be left blank, but holds only what it should when it is not.</summary>
        public void Shape(string name, string id, string wrong, Func<string, bool> valid)
        {
            var value = Of(name);
            if (value.Length > 0 && !valid(value)) found.Add((name, id, wrong));
        }

        /// <summary>A field that may not run past so many characters (a field 50, an address line 40).</summary>
        public void AtMost(string name, string id, int max)
        {
            if (found.Any(f => f.Field == name)) return;
            if (Of(name).Length > max) found.Add((name, id, InputRules.TooLong(max)));
        }
    }

    /// <summary>
    /// Whether the last post was about a joint holder's card - an error on it, or the
    /// part of the page to come back to is in it - so the card is shown open.
    /// </summary>
    /// <summary>
    /// Where an added joint holder's card stands, for its folded one-line summary: who they
    /// are, how many of the documents asked for are filed, and how many fields are still to
    /// fill. Complete when nothing on the card is left to do.
    /// </summary>
    public (bool Complete, string Line) HolderStanding(int holder, DocumentsViewModel.DocHolder h)
    {
        var slots = new List<DocumentsViewModel.SlotView>();
        foreach (var slot in DocumentsViewModel.HolderSlots)
        {
            var view = Docs.View(slot, h);
            if (view.Used) slots.Add(view);
        }
        var filed = slots.Count(v => v.Doc is not null);
        var fieldsToFill = UnfilledFor(State, holder, h, Docs.MailTyped, Places, Docs.Ref.SubOccupationsFor).Count;
        var complete = slots.All(v => !v.Missing) && fieldsToFill == 0;

        var line = $"{h.Who.Name} · PAN {DocumentsViewModel.MaskPan(h.Who.Pan)}";
        if (h.Who.Folio.Length > 0) line += $" · Folio {h.Who.Folio}";
        else line += " · New folio";
        if (slots.Count > 0)
        {
            var word = slots.Count == 1 ? "document" : "documents";
            line += $" · {filed} of {slots.Count} {word} filed";
        }
        if (fieldsToFill == 0) line += " · details complete";
        else if (fieldsToFill == 1) line += " · 1 field to fill";
        else line += $" · {fieldsToFill} fields to fill";
        return (complete, line);
    }

    public bool About(int holder, DocumentsViewModel.DocHolder h) =>
        Errors.Keys.Any(k => k.StartsWith($"Holder{holder}."))
        || (Docs.Shown?.Errors.Keys.Any(k => k.StartsWith($"h{h.Code}-")) ?? false)
        || Focus is { } f && (f.StartsWith($"holder-{holder}") || f.StartsWith($"h{holder}-") || f.StartsWith($"investor{holder}")
            || f.StartsWith($"pep-Holder{holder}.") || f.Contains($"h{h.Code}-"));

    private static bool IsDate(string dd, string mm, string yyyy, DateTime today) =>
        int.TryParse(dd, out var d) && int.TryParse(mm, out var m) && int.TryParse(yyyy, out var y)
        && y >= 1900 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m)
        && new DateTime(y, m, d) <= today;

    /// <summary>The part of the page the last post was about.</summary>
    public string? Focus { get; init; }

    /// <summary>The district and state of each PIN code typed on the page, by PIN code.</summary>
    public IReadOnlyDictionary<string, PinPlace> Places { get; init; } = new Dictionary<string, PinPlace>();

    /// <summary>Every 6-digit PIN code typed for a communication address, to be placed.</summary>
    public static IEnumerable<string> PinCodes(InvestorInfoState state) =>
        state.Fields.Where(f => f.Key.EndsWith("." + InvestorDetailsForm.Comm + "PinCode") && IsPin(f.Value.Trim())).Select(f => f.Value.Trim()).Distinct();

    public static bool IsPin(string value) => Regex.IsMatch(value, @"^[1-9]\d{5}$");

    /// <summary>A name as printed: letters and spaces only (InputRules).</summary>
    public static bool IsName(string value) => InputRules.IsName(value);

    /// <summary>A line of text: letters, digits, spaces and - , & / . only (InputRules).</summary>
    public static bool IsText(string value) => InputRules.IsClean(value);

    public const string LettersOnly = InputRules.LettersOnly;
    public const string NoSpecialCharacters = InputRules.OnlyAllowed;
}

/// <summary>A stop on a page, said as an error: the title, what is wrong, what to do, and a link to where it is done (null when there is nowhere to go).</summary>
public sealed record StopNotice(string Title, string Why, string WhatToDo, string? LinkText, string? LinkController);
