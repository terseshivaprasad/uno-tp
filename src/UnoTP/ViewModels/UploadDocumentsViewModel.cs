using UnoTP.Backend;
using UnoTP.Backend.External;
using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// The old WA_FD_UNOTP/UploadInvestorDocuments: the documents an application
/// carries, and the details it is sourced under. Step two of the classic wizard.
///
/// Everything happens on the server: a document is posted, put through its checks
/// and filed or refused; a choice that reshapes the step (the application type, a
/// proof's type, the payment or sourcing mode, the category) is posted and the page
/// redrawn from it. What the step holds against the application is read from the
/// backend and saved back to it (see <see cref="IApplicationApi"/>), so a reload or
/// a visit later finds it as it was left. Every post redirects back to the page, so
/// a refresh never posts twice.
///
/// The PAN copy, the photograph and the proof of address are asked of every holder,
/// the same way: of the investor here, and of each joint holder on Investor
/// Information, which hands its posts to this same model (see <see cref="DocHolder"/>).
///
/// A document's checks are outside services, each asked in turn: identification,
/// OCR - which for an Aadhaar has to read all 12 digits of its number - and whoever
/// answers for what was read - the issuer or the bank - or for a PAN, the
/// PAN-Aadhaar link. Only then is the copy filed with DMS (see <see cref="IDocumentApi"/>).
///
/// The address carries nothing: the application is the one the session is on,
/// opened by Investor Identification, and only ever the owner's own (see
/// <see cref="Controllers.UploadDocumentsController"/>), which reads it, hands it
/// here, and saves what this leaves in it.
/// </summary>
public class UploadDocumentsViewModel(
    UnoTP.Backend.Application app,
    ISession session,
    IDocumentApi documents,
    IDocumentIdentifier identifier,
    IOcrService ocr,
    INsdlService nsdl,
    UnoTP.Features.FeatureSet features,
    IVerificationService verification,
    IPanAadhaarLinkService panLink)
{
    /// <summary>The application the session is on, read afresh for every request.</summary>
    public UnoTP.Backend.Application App { get; } = app;

    private Holder Who => App.Holder;

    public string HolderName => Who.Name;

    // A PAN established at the step before has no folio yet: one opens with the
    // application, and the head of the page says so rather than leaving a blank.
    public bool HasFolio => Who.Folio.Length > 0;

    public string Folio => Who.Folio;

    public string HolderFolio => HasFolio ? Who.Folio : "Opens with this application";

    public string HolderPan => Mask(Who.Pan);

    public string HolderDob => MaskDate(Who.Dob);

    /// <summary>Set when the step before already put a PAN copy on the application.</summary>
    public bool PanFiled => Who.PanFiled;

    /// <summary>The number the application is filed under, minted when it was opened.</summary>
    public string AppNo => App.AppNo;

    /// <summary>
    /// Whose documents a slot holds, by the holder type DMS files them under: 01 the
    /// investor, 02 the second holder, 03 the third. The investor's keys are the
    /// slots' own ("pan"); a joint holder's carry their type in front ("h02-pan").
    /// </summary>
    public sealed record DocHolder(string Code, Holder Who)
    {
        public bool Joint => Code != HolderType.Investor;

        public string Key(string slot) => Joint ? $"h{Code}-{slot}" : slot;
    }

    public DocHolder Investor => new(HolderType.Investor, Who);

    /// <summary>The joint holders on the application: the second, then the third.</summary>
    public IEnumerable<DocHolder> JointHolders =>
        State.Joint.OrderBy(j => j.Key, StringComparer.Ordinal).Select(j => new DocHolder(j.Key, j.Value.Holder));

    public DocHolder? JointHolder(string? code) =>
        code is not null && State.Joint.TryGetValue(code, out var j) ? new DocHolder(code, j.Holder) : null;

    private IEnumerable<DocHolder> Holders => JointHolders.Prepend(Investor);

    /// <summary>
    /// Whether a proof's type is set from what the copy is identified as, after the
    /// upload (the document identification feature, on by default). Off, it is chosen
    /// from a drop-down first, and the box waits for it.
    /// </summary>
    public bool AutoProofType => features.Flags.DocIdentification;

    /// <summary>The proof of address chosen for a holder.</summary>
    public string PoaTypeOf(DocHolder h) => h.Joint ? State.Joint[h.Code].PoaType : State.PoaType;

    private void SetPoaType(DocHolder h, string type)
    {
        if (h.Joint) State.Joint[h.Code].PoaType = type;
        else State.PoaType = type;
    }

    /// <summary>
    /// Whether a holder's communication address is theirs to give here. Not where
    /// the system already holds the address - a folio with the proof of address or
    /// the address on it, whose address is the mailing address too - nor, for the
    /// investor, once CKYC is fetched, whose record brings it. Wherever a proof of
    /// address is asked for, the address is being set now and post may go elsewhere.
    /// </summary>
    public bool MailCanDiffer(DocHolder h) =>
        !(State.Ckyc && !h.Joint) && (h.Who.Folio.Length == 0 || View(PoaSlot, h).Used);

    // Why a holder has no communication address of their own to prove.
    private string MailWhy(DocHolder h) => h.Who.Folio.Length > 0
        ? $"The address is held against folio {h.Who.Folio}, so post goes there."
        : "The communication address comes with the CKYC record, once the investor consents. It is not uploaded here.";

    /// <summary>
    /// Whether NSDL is asked about a holder's PAN when their PAN copy is filed: a
    /// joint holder with no folio, who is added on their PAN and date of birth
    /// alone. OCR reads the name off the copy, and NSDL is asked for all three. The
    /// investor's PAN is established on Investor Identification instead.
    /// </summary>
    public static bool NsdlApplies(DocHolder h) => h.Joint && h.Who.Folio.Length == 0;

    /// <summary>Where NSDL stands on a joint holder: empty until asked, "verified", "name" or "failed".</summary>
    public string NsdlOf(DocHolder h) => h.Joint ? State.Joint[h.Code].Nsdl : "";

    /// <summary>
    /// Whether the PAN-Aadhaar link is asked for a holder: only one with no folio
    /// yet. A holder on a folio is past it.
    /// </summary>
    public static bool LinkApplies(DocHolder h) => h.Who.Folio.Length == 0;

    /// <summary>Whether a holder's post goes to an address other than the permanent one.</summary>
    public bool MailDifferentOf(DocHolder h) =>
        MailCanDiffer(h) && (h.Joint ? State.Joint[h.Code].MailDifferent : State.MailDifferent);

    /// <summary>What a holder's communication address is proved with.</summary>
    public string MailTypeOf(DocHolder h) => h.Joint ? State.Joint[h.Code].MailPoaType : State.MailPoaType;

    private void SetMail(DocHolder h, bool different, string type)
    {
        if (h.Joint) (State.Joint[h.Code].MailDifferent, State.Joint[h.Code].MailPoaType) = (different, type);
        else (State.MailDifferent, State.MailPoaType) = (different, type);
    }

    // What a proof is handed in as within its kind: the type chosen above its box.
    private string TypeOf(SlotDef def, DocHolder h) => def.Key switch
    {
        "poa" => PoaTypeOf(h),
        "mail" => MailTypeOf(h),
        "payment" => State.PayMode,
        _ => "",
    };

    // A KYC document is filed under the holder it belongs to; the rest belong to
    // the application and file as not holder-specific.
    private static string FiledUnder(SlotDef def, DocHolder h) => HolderSlots.Contains(def) ? h.Code : HolderType.None;

    /// <summary>Where DMS holds the copy behind a slot's key: its holder type and slot.</summary>
    public (string Holder, string Slot)? DmsOf(string key) =>
        Locate(key) is { } found ? (FiledUnder(found.Def, found.Holder), found.Def.Key) : null;

    // The same masking the register uses: a PAN keeps its first five and last
    // character, a date of birth only its year.
    public static string Mask(string pan) =>
        pan.Length == 10 ? pan[..5] + "••••" + pan[9..] : pan;

    private static string MaskDate(string dob) =>
        dob.Length == 10 ? "••-••-" + dob[6..] : dob;

    // ===== What the page offers ================================================

    /// <summary>What a drop zone takes, and what it says it takes.</summary>
    public record Accepts(string Mime, string Label, int MaxMb);

    public static readonly Accepts Form = new("application/pdf,image/jpeg", "PDF/JPG/JPEG", 4);
    public static readonly Accepts Image = new("image/jpeg", "JPG/JPEG", 2);
    public static readonly Accepts Proof = new("application/pdf,image/jpeg", "PDF/JPG/JPEG", 2);

    // An application made on paper carries a signed form; a digital one is
    // accepted through the investor's own link instead, so that slot is not used.
    public const string Digital = "DIGITAL";
    public const string Physical = "PHYSICAL";
    public static readonly string[] ApplicationTypes = { Digital, Physical };

    /// <summary>What a digital application carries where a paper one carries its form number.</summary>
    public const string DigitalFormNo = "0000";

    public static readonly string[] ProofsOfAddress =
    {
        "Aadhaar", "Passport", "Driving Licence", "Voter ID", "Utility bill",
    };

    // Only the modes settled by an instrument carry a document; the rest are
    // settled electronically and have nothing to file.
    public record PaymentMode(string Name, string? Document);

    public static readonly PaymentMode[] PaymentModes =
    {
        new("Online", null),
        new("RTGS", null),
        new("Cheque", "cheque"),
    };

    // The partner in the top bar, whose code fills the sourcing field.
    public const string PartnerCode = "100002225";

    // ----- What a deposit is booked as -------------------------------------------
    public const string General = "PUBLIC/GENERAL";
    public const string Women = "WOMEN";
    public const string Senior = "SR CITIZEN";
    public const string SeniorWomen = "SR CITIZEN WOMEN";

    // An employee deposit is booked against a staff record, which is what the
    // block of employee fields is for. Only a 1033 partner books one.
    public const string EmployeeCategory = "EMPLOYEE";
    public const string EmployeeWomen = "EMPLOYEE WOMEN";

    public static bool IsEmployee(string category) => category is EmployeeCategory or EmployeeWomen;

    /// <summary>A senior citizen is 60 or over on the day the deposit is booked.</summary>
    public const int SeniorAge = 60;

    /// <summary>
    /// The category the holder's date of birth and gender make them: a senior
    /// citizen from 60, and the women's category of either for a woman. With no
    /// gender known yet, the one the date of birth alone gives.
    /// </summary>
    public static string CategoryFor(string dob, string gender, DateTime today)
    {
        var senior = DateTime.TryParseExact(dob, "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var born) && born.AddYears(SeniorAge) <= today.Date;
        var woman = gender == Genders.Female;
        return (senior, woman) switch
        {
            (true, true) => SeniorWomen,
            (true, false) => Senior,
            (false, true) => Women,
            _ => General,
        };
    }

    // ----- How the application is sourced --------------------------------------
    // One answer the rest of Additional Details hangs off: what the two code
    // fields are called, which of them is typed and which the mode fills itself,
    // whose register the typed one is searched against, and what the deposit may
    // be booked as. The old screen keys the modes by number and posts them that
    // way, so they keep their numbers here.

    /// <summary>What the second code field does under a mode.</summary>
    public static class SubField
    {
        /// <summary>Shut and empty: the mode has no sub-broker.</summary>
        public const string Shut = "shut";

        /// <summary>Shut, carrying the same house code as the field above it.</summary>
        public const string House = "house";

        /// <summary>There if there is one, empty if there is not.</summary>
        public const string Free = "free";

        /// <summary>The partner's own code, and theirs to change.</summary>
        public const string Employee = "employee";

        /// <summary>The partner's own code, and not theirs to change.</summary>
        public const string EmployeeShut = "employeeShut";
    }

    /// <summary>Which register a mode searches its typed code against.</summary>
    public static class Register
    {
        public const string None = "";
        public const string Brokers = "brokers";
        public const string Employees = "employees";
    }

    /// <summary>
    /// A way an application is sourced. <c>House</c> is the code the mode stands
    /// in the first field itself, where it does; <c>Search</c> names the field
    /// that is searched - "source" for the first, "sub" for the second - and
    /// <c>Register</c> what it is searched against.
    /// </summary>
    public record SourcingMode(
        string Code, string Name, string CodeLabel, string NameLabel,
        string House, string Search, string Register, string Sub, string[] Categories);

    private static readonly string[] Public = { General, Women, Senior, SeniorWomen };

    /// <summary>
    /// The four modes the old screen offers a partner, in its own order. The
    /// screen posts them by number and hangs everything off that number: BROKER
    /// is the one where the code is typed, and each of the other three stands its
    /// own house code in the field and asks for nothing there.
    /// </summary>
    public static readonly SourcingMode[] SourcingModes =
    {
        new("2", "BROKER", "Broker Code", "Broker Name",
            "", "source", Register.Brokers, SubField.Free, Public),
        new("1", "MMFSS - BRANCH", "Sourcing Employee Code", "Sourcing Employee Name",
            "MFL", "", Register.None, SubField.House, Public),
        new("5", "MFL-EX", "Sourcing Employee Code", "Sourcing Employee Name",
            "MFL-EX", "sub", Register.Employees, SubField.Employee, new[] { EmployeeCategory, EmployeeWomen }),
        new("6", "MFIS/FD", "Sourcing Employee Code", "Sourcing Employee Name",
            "MIBS", "sub", Register.Employees, SubField.EmployeeShut, Public),
    };

    /// <summary>The broker mode, the only one a partner other than 1033 sources under.</summary>
    public static SourcingMode BrokerMode => SourcingModes[0];

    /// <summary>
    /// Whether the partner chooses how the application is sourced and what it is
    /// booked as: agency type 1033. Anyone else sources as a broker under their own
    /// business broker code, and the category follows the holder's date of birth
    /// and gender.
    /// </summary>
    public bool Chooses => UnoTP.Features.PartnerSession.IsSourcingAgency(session);

    /// <summary>The business broker code a partner other than 1033 files under.</summary>
    public string BusinessBroker => UnoTP.Features.PartnerSession.BrokerCode(session);

    /// <summary>The gender the category is set from: the folio's, else an Aadhaar's read here.</summary>
    public string HolderGender => GenderIn(State);

    // Read off the state handed in, not State, which settles through here.
    private string GenderIn(UploadState s) => Who.Gender.Length > 0 ? Who.Gender : s.Gender;

    /// <summary>Why the category stands as it does, for a partner who does not choose it.</summary>
    public string SetCategoryWhy =>
        (Who.Dob.Length == 0 ? "No date of birth is on record" : "Set from the date of birth")
        + (HolderGender.Length > 0 ? $" and gender ({HolderGender.ToLowerInvariant()})."
            : ". The gender is read off an Aadhaar filed as the proof of address; until then a women's category cannot be given.");

    // A partner other than 1033 has nothing to choose: broker mode, their own
    // code, and the category the holder's details set. Kept whenever the state is
    // read, so a saved state from before holds to it too.
    private void Settle(UploadState s)
    {
        if (Chooses) return;
        s.Sourcing = BrokerMode.Code;
        s.SourceCode = BusinessBroker;
        s.Category = CategoryFor(Who.Dob, GenderIn(s), DateTime.Today);
    }

    /// <summary>The brokers a broker-sourced application can be filed under, from the backend.</summary>
    public IReadOnlyList<Party> Brokers { get; set; } = [];

    /// <summary>The staff a sub-broker or employee code is searched against, the
    /// partner at the keyboard among them, from the backend.</summary>
    public IReadOnlyList<Party> Staff { get; set; } = [];

    /// <summary>The name the staff register holds against a code, or null.</summary>
    public string? StaffName(string code) =>
        Staff.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Name;

    public static readonly string[] EmployeeHolders = { "First holder", "Second holder", "Third holder" };

    public static readonly string[] EmployeeRelations = { "Self", "Spouse", "Parent", "Child", "Sibling" };

    public static readonly string[] EmployeeProofs =
        { "Employee ID card", "Appointment letter", "Latest salary slip" };

    // ----- The address the application carries ---------------------------------
    // A proof of address is not filed on its own: OCR reads the address off it and
    // the issuer is asked whether that is the address they hold. Only an answer
    // that comes back clean replaces what the application already carries, which
    // is why the step shows both addresses where the pickers can change them.

    /// <summary>The address on record, or empty for an investor with none yet.</summary>
    public string PermanentAddress => Who.Address;

    /// <summary>Who stands behind each proof, as the empty box names them before
    /// anything is uploaded. A bill has no register to ask, so it is Operations who
    /// settle it and the address is left as it stands.</summary>
    public static readonly Dictionary<string, string> Issuers = new()
    {
        ["Aadhaar"] = "UIDAI",
        ["Passport"] = "Passport Seva",
        ["Driving Licence"] = "Sarathi",
        ["Voter ID"] = "the Election Commission",
        ["Utility bill"] = "",
    };

    /// <summary>Who answers for a PAN.</summary>
    public const string PanAuthority = "the Income Tax Department";

    // The same, short enough for a card.
    private const string LinkWaitsShort = "Asked once an Aadhaar number is read on this application.";

    private const string LinkWaits =
        "Whether an Aadhaar is linked to it is asked once an Aadhaar is read on this application — file one as the proof of address.";

    // The holder's consent to their Aadhaar being read and checked, which IDfy asks
    // for on every Aadhaar call. The step does not ask the partner for it, so it is
    // never given: behind a real IDfy an Aadhaar is turned back, saying why. The
    // mock does not ask.
    private const bool AadhaarConsent = false;

    // The Aadhaar number OCR read for a holder on this application, for asking the
    // PAN-Aadhaar link with. An Aadhaar number is not to be stored, so it is never
    // saved with the application: it is held in the server's session, and goes with it.
    private string AadhaarKey(DocHolder h) => "aadhaar:" + AppNo + (h.Joint ? ":" + h.Code : "");

    private string AadhaarOf(DocHolder h) => session.GetString(AadhaarKey(h)) ?? "";

    /// <summary>What the register already holds against the folio the application
    /// was opened on. A document on the folio is not asked for again: the step
    /// shows it as not applicable and says which folio carries it.</summary>
    public DocsOnRecord? FolioDocs => FolioDocsOf(Investor);

    private static DocsOnRecord? FolioDocsOf(DocHolder h) => h.Who.Folio.Length > 0 ? h.Who.OnRecord : null;

    // ===== The documents =======================================================

    /// <summary>A document the step asks for, by the key its markup and posts carry.</summary>
    public record SlotDef(string Key, string Label, Accepts Accepts);

    public static readonly SlotDef FormSlot = new("form", "Application Form", Form);
    public static readonly SlotDef PanSlot = new("pan", "PAN copy", Proof);
    public static readonly SlotDef PhotoSlot = new("photo", "photograph", Image);
    public static readonly SlotDef PoaSlot = new("poa", "POA", Image);
    public static readonly SlotDef MailSlot = new("mail", "communication address proof", Image);
    public static readonly SlotDef PaymentSlot = new("payment", "the instrument", Image);
    public static readonly SlotDef EmpProofSlot = new("empproof", "Employee Proof", Proof);

    public static readonly SlotDef[] Slots = [FormSlot, PanSlot, PhotoSlot, PoaSlot, MailSlot, PaymentSlot, EmpProofSlot];

    /// <summary>What every holder files for themselves: the investor on this step, and
    /// each joint holder on Investor Information. The rest belong to the application.</summary>
    public static readonly SlotDef[] HolderSlots = [PanSlot, PhotoSlot, PoaSlot, MailSlot];

    /// <summary>Three refusals in a row and a document goes to Operations.</summary>
    public const int MaxAttempts = 3;

    /// <summary>What a slot shows: whether it is asked for at all, and what is in it.</summary>
    /// <param name="Key">The slot's key for its holder, which its markup and posts carry.</param>
    /// <param name="Optional">Asked for, but not needed to proceed.</param>
    public sealed record SlotView(
        SlotDef Def, string Key, bool Used, string? NotApplicable, string? Locked, StoredDoc? Doc,
        string? Must, string? With, int Attempts, string? Error, string? ErrorLog, bool Optional = false,
        ReadCard? Read = null)
    {
        /// <summary>Wanted before the step can go on: asked for, not optional, and not in yet.</summary>
        public bool Missing => Used && !Optional && Doc is null;

        public bool Spent => Attempts >= MaxAttempts;
        public string Title => Def.Key switch { "payment" => "the cheque", "mail" => "the proof", _ => Def.Label };
        public bool Unconfirmed => Doc?.CheckKind is "warn" or "bad";

        public string? Tries => Attempts == 0 ? null
            : Spent ? $"{MaxAttempts} refused one after another — this document now goes to Operations."
            : $"{Attempts} of {MaxAttempts} refused in a row this session — a copy the checks take clears it.";
    }

    private const string CkycWhy =
        "CKYC supplies this once the investor consents, which is asked for after the application is completed. It is not uploaded here.";

    // The three checks are named on the empty box too: a partner who knows UIDAI
    // will be asked reaches for the copy UIDAI would recognise.
    private const string Run = "Once uploaded: identified, read by OCR, then ";

    public SlotView View(SlotDef def) => View(def, Investor);

    public SlotView View(SlotDef def, DocHolder h)
    {
        var s = State;
        var key = h.Key(def.Key);
        var proofType = TypeOf(def, h);
        var held = FolioDocsOf(h);
        string heldWhy = $"Already held against folio {h.Who.Folio}, so it is not filed again.";
        var (used, na) = def.Key switch
        {
            "form" => (s.AppType == Physical, "A digital application is accepted through the investor’s own link, so there is no signed form to file."),
            "pan" => held?.Pan == true ? (false, heldWhy) : (true, null),
            // CKYC is fetched for the investor only: a joint holder files their own.
            "photo" => held?.Photo == true ? (false, heldWhy) : s.Ckyc && !h.Joint ? (false, CkycWhy) : (true, null),
            // A folio that holds the proof, or the address itself, needs no proof of it.
            "poa" => held?.Poa == true ? (false, heldWhy)
                : held is not null && h.Who.Address.Length > 0 ? (false, $"The address is held against folio {h.Who.Folio}, so no proof of it is asked for.")
                : s.Ckyc && !h.Joint ? (false, CkycWhy) : (true, null),
            "mail" => !MailCanDiffer(h) ? (false, MailWhy(h))
                : MailDifferentOf(h) ? (true, null) : (false, "Post goes to the permanent address, so there is no other address to prove."),
            "payment" => (DocumentOf(s.PayMode) is not null, $"{(s.PayMode.Length > 0 ? s.PayMode : "This mode")} is settled electronically, so there is no instrument to copy."),
            "empproof" => (IsEmployee(s.Category), "Only a deposit booked against a staff record carries an employee proof."),
            _ => (true, (string?)null),
        };

        // A proof is checked as whatever type was chosen above it, so there is
        // nothing to check it as until one is.
        string? locked = !used ? null : def.Key switch
        {
            "poa" when !AutoProofType && proofType.Length == 0 => "Choose the proof of address first",
            "mail" when !AutoProofType && proofType.Length == 0 => "Choose the communication address proof first",
            "empproof" when s.EmpProofType.Length == 0 => "Choose the employee proof first",
            _ => null,
        };

        string? with = def.Key switch
        {
            "pan" => Run + $"checked with {PanAuthority}.",
            "payment" => Run + "the account is confirmed with the bank it is drawn on.",
            "poa" or "mail" when Issuers.TryGetValue(proofType, out var issuer) => issuer.Length > 0
                ? Run + $"the address is confirmed with {issuer}."
                : $"Once uploaded: identified and read by OCR. A {proofType.ToLowerInvariant()} has no issuer to confirm the address with, so Operations settle it.",
            // The type is what the copy is identified as, so until one is filed it is not known.
            "poa" or "mail" => "Once uploaded: identified, which sets its type, read by OCR, then confirmed with its issuer.",
            _ => null,
        };

        // A holder on a folio has been through KYC: a PAN copy the folio does not
        // hold is taken if there is one, but not needed.
        var optional = used && def.Key == "pan" && h.Who.Folio.Length > 0;

        // What the copy was read to say stands in its own box once it is filed. An
        // address the folio holds is shown where its proof would be, as it stands:
        // nothing here checked it.
        var doc = used ? s.Docs.GetValueOrDefault(key) : null;
        var folioAddress = held is not null && h.Who.Address.Length > 0;
        ReadCard? read = def.Key switch
        {
            "poa" when doc is not null => s.Reads.GetValueOrDefault(key),
            "mail" when doc is not null => MailReadOf(h),
            "payment" when doc is not null => s.Reads.GetValueOrDefault("payment"),
            "poa" when !used && folioAddress => FolioAddress(h, "so no proof of it is asked for."),
            "mail" when !used && folioAddress && !MailCanDiffer(h) => FolioAddress(h, "and post goes there."),
            _ => null,
        };

        var flash = Shown;
        return new SlotView(def, key, used, used ? null : na, locked, doc,
            optional ? $"Not mandatory: the holder is on folio {h.Who.Folio}." : null,
            with, s.AttemptsOf(key),
            flash?.Errors.GetValueOrDefault(key), flash?.ErrorLog.GetValueOrDefault(key), optional, read);
    }

    /// <summary>
    /// What a holder's KYC copies were read to say, always the same cards in the
    /// same order: the permanent address, the communication address, for a joint
    /// holder NSDL, and the PAN's link with Aadhaar. A card with nothing to read says
    /// why rather than going missing.
    /// </summary>
    public IReadOnlyList<ReadItem> ReadsOf(DocHolder h)
    {
        List<ReadItem> cards =
        [
            ("Permanent address", State.Reads[h.Key("poa")]),
            ("Communication address", MailDifferentOf(h) ? MailReadOf(h)
                : !MailCanDiffer(h) && h.Who.Folio.Length > 0 && h.Who.Address.Length > 0 ? FolioAddress(h, "and post goes there.")
                : !MailCanDiffer(h) && h.Who.Folio.Length > 0 ? NotRead("Same as permanent", $"Post goes to the address held against folio {h.Who.Folio}.",
                    "The system holds the address, and it is the mailing address too.")
                : !MailCanDiffer(h) ? NotRead("From CKYC", "The communication address comes with the CKYC record.",
                    "It is fetched once the investor consents, with the permanent address and the photograph.")
                : NotRead("Same as permanent", "Post goes to the permanent address, so there is no other address to read.",
                    "Choose Different from Permanent to file a proof of another address.")),
        ];
        if (h.Joint) cards.Add(NsdlCard(h));
        cards.Add(("PAN–Aadhaar link", LinkApplies(h) ? State.Reads[h.Key("pan")]
            : NotRead("Not applicable", $"{Mask(h.Who.Pan)} · held against folio {h.Who.Folio}",
                "The PAN–Aadhaar link is asked only for a holder with no folio yet.")));
        return cards;
    }

    /// <summary>
    /// What a holder's PAN was checked with: NSDL, for a joint holder, and the
    /// PAN-Aadhaar link. These take two copies, or a name typed again, so they stand
    /// as cards; an address stands in the box of the proof it was read off.
    /// </summary>
    public IReadOnlyList<ReadItem> PanReadsOf(DocHolder h) =>
        [.. ReadsOf(h).Where(i => i.Kind is not ("Permanent address" or "Communication address"))];

    /// <summary>
    /// What the payment instrument was read to say, beside its box. A mode settled
    /// electronically carries no instrument, and the card says so.
    /// </summary>
    public ReadItem PaymentRead() => View(PaymentSlot).Used
        ? new("Account", State.Reads["payment"], "payment")
        : new("Account", NotRead("Not applicable", "No instrument is copied for this payment mode.",
            "An account is read only off a cheque or demand draft."));

    // What NSDL said about a joint holder's PAN, and - when it holds the PAN
    // against another name - where the name printed on the card is typed to ask again.
    private ReadItem NsdlCard(DocHolder h)
    {
        var pan = Mask(h.Who.Pan);
        if (!NsdlApplies(h))
            return new("PAN – NSDL", NotRead("Not applicable", $"{pan} · held against folio {h.Who.Folio}", "A holder on a folio is not asked about with NSDL again."));
        var j = State.Joint[h.Code];
        var key = h.Key("nsdl");
        return j.Nsdl switch
        {
            "verified" => new("PAN – NSDL", new ReadCard("Verified with NSDL", $"{pan} · {h.Who.Name}", "The PAN, date of birth and name read off the PAN copy all match.", "is-done"), key),
            "name" => new("PAN – NSDL", new ReadCard("Name not matched", $"Put to NSDL: {j.NsdlName}",
                "NSDL holds the PAN and date of birth, but not against that name. Type the name exactly as printed on the PAN card, and NSDL is asked again.", "is-failed"), key,
                new NameRetry(h.Key("nsdlName"), j.NsdlName, Shown?.Errors.GetValueOrDefault(key))),
            "failed" => new("PAN – NSDL", new ReadCard("Not verified", $"No record of {pan} against {MaskDate(h.Who.Dob)}",
                "NSDL holds no such PAN and date of birth, so this holder cannot go on. Remove them and search again.", "is-failed"), key),
            _ => new("PAN – NSDL", new ReadCard("Not yet checked", "Checked once the PAN copy is filed above.",
                "OCR reads the name off the PAN copy, and NSDL is asked whether it holds the PAN, the date of birth and that name."), key),
        };
    }

    // The address a folio holds, shown as it stands: nothing on this step checked it.
    private static ReadCard FolioAddress(DocHolder h, string then) =>
        new("Not verified", h.Who.Address, $"Held against folio {h.Who.Folio}, {then}", "is-unverified");

    // A card for something there is nothing to read off, saying why. Not kept.
    private static ReadCard NotRead(string state, string lines, string from) => new(state, lines, from, "is-na");

    /// <summary>What a holder's communication address proof was read to say.</summary>
    public ReadCard MailReadOf(DocHolder h)
    {
        var key = h.Key("mail");
        if (!State.Reads.TryGetValue(key, out var card)) State.Reads[key] = card = MailCard();
        return card;
    }

    private static ReadCard MailCard() => new("Not yet read", "Read off the communication address proof once one is filed.",
        "Confirmed with whoever issued the proof, as the permanent address is.");

    public static string? DocumentOf(string payMode) =>
        PaymentModes.FirstOrDefault(m => m.Name == payMode)?.Document;

    public static SourcingMode? ModeOf(string code) => SourcingModes.FirstOrDefault(m => m.Code == code);

    // ===== The state of this application =======================================

    /// <summary>What the step holds against this application, opened on first sight.</summary>
    public UploadState State
    {
        get
        {
            var s = App.Upload ??= Open();
            Settle(s);
            return s;
        }
    }

    // A new application opens with the reads as the step before left them, the
    // PAN copy that came over with it, and the attempts the backend has on record
    // from before this page - which are on the record without counting against
    // the three.
    private UploadState Open()
    {
        var s = new UploadState();
        OpenHolder(s, Investor);
        s.Reads["payment"] = new ReadCard("Not yet read", "Read off the instrument once a copy is filed.",
            "The account the deposit is paid from. Bank Details & Payment opens with whatever is confirmed here.");
        s.Log.AddRange(App.Prior);
        return s;
    }

    // A holder's cards open with what identifying them established: the PAN copy
    // it came with, and the address the folio holds.
    private static void OpenHolder(UploadState s, DocHolder h)
    {
        var pan = Mask(h.Who.Pan);
        s.Reads[h.Key("pan")] = h.Who.PanFiled
            ? new ReadCard("Link not checked", pan + " · link with Aadhaar not checked yet",
                $"PAN confirmed with NSDL. {LinkWaitsShort}")
            : new ReadCard("Not yet read", "Read off the PAN copy once one is filed here.", LinkWaitsShort);
        // The folio's address is shown as it stands, until a proof filed here is confirmed.
        s.Reads[h.Key("poa")] = h.Who.Address.Length > 0
            ? FolioAddress(h, "and not checked on this step.")
            : new ReadCard("Not on record", $"No address is held for this {(h.Joint ? "holder" : "investor")} yet.",
                "Read off the proof of address once one is filed here.");

        if (h.Who.PanFiled)
        {
            s.Docs[h.Key("pan")] = new StoredDoc("PAN copy on the application", 0, "",
                $"Identified, read and confirmed with NSDL when the PAN was established {(h.Joint ? "as this holder was identified" : "on the step before")}.", "ok", Before: true);
        }
    }

    /// <summary>What a log entry's holder becomes once that joint holder is taken off.</summary>
    public const string RemovedHolder = "removed";

    /// <summary>Puts a joint holder on the application under their type, with their documents yet to come.</summary>
    public void AddJoint(string code, Holder holder)
    {
        if (State.Joint.ContainsKey(code)) return;
        State.Joint[code] = new JointHolder { Holder = holder };
        OpenHolder(State, new DocHolder(code, holder));
    }

    /// <summary>
    /// Takes a joint holder off the application, and every document of theirs with
    /// them. Only the last one opened can go - the second only once there is no
    /// third - so nobody ever changes holder type.
    /// </summary>
    public void RemoveJoint(string code)
    {
        if (JointHolder(code) is not { } h) return;
        foreach (var def in HolderSlots)
        {
            var key = h.Key(def.Key);
            Drop(key);
            State.Attempts.Remove(key);
            State.Reads.Remove(key);
        }
        State.Joint.Remove(code);
        session.Remove(AadhaarKey(h));
        // Their attempts stay on the application's history, but no longer as any
        // holder's: whoever takes their type next does not inherit them.
        foreach (var entry in State.Log.Where(e => e.Holder == code)) entry.Holder = RemovedHolder;

    }

    /// <summary>What the last post left to say, taken out of the session as the page is drawn.</summary>
    public Flash? Shown { get; set; }

    /// <summary>What this post has to say, kept for the page it redirects to.</summary>
    public Flash? Said { get; set; }

    /// <summary>Set once Proceed finds everything in: the post moves on to Investor Information.</summary>
    public bool Complete { get; private set; }

    // The copies this post took off the application, as DMS holds them. DMS follows
    // once the save that takes them off has gone through (see SettleAsync).
    private readonly HashSet<(string Holder, string Slot)> dropped = [];

    private void Drop(string key)
    {
        if (DmsOf(key) is { } at && State.Docs.Remove(key, out var doc) && !doc.Before) dropped.Add(at);
    }

    /// <summary>Brings DMS in line with what was just saved: the copies taken off are deleted.</summary>
    public async Task SettleAsync()
    {
        foreach (var (holder, slot) in dropped) await documents.DeleteAsync(AppNo, holder, slot);
    }

    // ===== What the form posts ================================================

    /// <summary>What this post carried; every post carries the whole form.</summary>
    public UploadForm Posted { get; set; } = new();

    // ===== What a post does ===================================================

    /// <summary>Upload: the file posted for the slot whose button was pressed.
    /// Returns where on the page to come back to.</summary>
    public Task<string?> UploadAsync(IFormFileCollection files) => UploadAsync(Posted.Slot, files);

    /// <summary>Upload for a slot by its key, the investor's or a joint holder's.</summary>
    public async Task<string?> UploadAsync(string? key, IFormFileCollection files)
    {
        if (Locate(key) is not { } found) return null;
        Keep();
        await TakeAsync(found.Def, found.Holder, files.GetFile("file_" + key));
        return "slot-" + key;
    }

    // The slot a key is for, and whose it is.
    private (SlotDef Def, DocHolder Holder)? Locate(string? key)
    {
        foreach (var h in Holders)
        {
            foreach (var def in h.Joint ? HolderSlots : Slots)
            {
                if (h.Key(def.Key) == key) return (def, h);
            }
        }
        return null;
    }

    // Nothing is requested of the investor from this step: choosing CKYC only says
    // how the KYC will arrive. The addresses - permanent and communication - and
    // the photograph come with that record, so those stop being asked for; the PAN
    // copy does not, and
    // nor does anything of a joint holder's, whose KYC is not fetched. It
    // cannot be taken back, and it closes the paper route - consent is given
    // online, through the link the investor is sent.
    public string Ckyc()
    {
        Keep();
        var s = State;
        if (!HasFolio && !s.Ckyc && s.AppType != Physical)
        {
            s.Ckyc = true;
            s.PoaType = "";
            Drop("poa");
            Drop("photo");
            s.Reads["poa"].Reset();
            ForgetMail(Investor);
            SetMail(Investor, false, "");
            Say().Banner = "The addresses and the photograph will come from CKYC once the investor consents.";
        }
        return "cudRoute";
    }

    // Proceed says what is missing where it is missing, and the page comes back
    // to the first of it.
    public string? Proceed()
    {
        Keep();
        var s = State;
        var flash = Say();
        void Need(bool ok, string key, string message)
        {
            if (ok) return;
            flash.Errors[key] = message;
            flash.Focus ??= key;
        }

        var mode = ModeOf(s.Sourcing);
        // A type chosen from the drop-down, when it is not set from the upload.
        if (!AutoProofType && View(PoaSlot).Used) Need(s.PoaType.Length > 0, "cudPoaType", "Choose the proof of address");
        if (!AutoProofType && View(MailSlot).Used) Need(s.MailPoaType.Length > 0, "cudMailType", "Choose the communication address proof");
        Need(s.PayMode.Length > 0, "cudPayMode", "Choose the payment mode");
        Need(s.Sourcing.Length > 0, "cudSourcing", "Choose the sourcing mode");
        if (mode is not null)
        {
            Need(s.SourceCode.Length > 0, "cudSourceCode", $"Enter the {mode.CodeLabel.ToLowerInvariant()}");
            if (SubRequired(mode)) Need(s.SubBroker.Length > 0, "cudSubBroker", "Enter the sub broker code");
        }
        Need(s.Category.Length > 0, "cudCategory", "Choose the deposit category");
        if (IsEmployee(s.Category))
        {
            // A code this screen cannot put a name to is not a reason to stop:
            // the staff register is Operations' to check.
            Need(s.EmpCode.Length > 0, "cudEmpCode", "Enter the employee code");
            Need(s.EmpCompany.Length > 0, "cudEmpCompany", "Enter the employee company name");
            Need(s.EmpHolder.Length > 0, "cudEmpHolder", "Choose which holder is the employee");
            Need(s.EmpRelation.Length > 0, "cudEmpRelation", "Choose the relation with the holder");
            Need(s.EmpProofType.Length > 0, "cudEmpProofType", "Choose the employee proof");
        }
        if (s.AppType == Physical) Need(s.TypedFormNo.Length > 0, "cudFormNo", "Enter the physical form number");

        foreach (var slot in Slots.Select(View).Where(v => v.Missing))
        {
            flash.Errors[slot.Key] = "This document is required";
            flash.Focus ??= "slot-" + slot.Key;
        }

        if (flash.Errors.Count == 0)
        {
            Said = null;
            Complete = true;
        }
        return flash.Focus;
    }

    private Flash Say() => Said ??= new Flash();

    /// <summary>
    /// What Proceed on Investor Information asks of each joint holder, the same way
    /// this step asks the investor: every document that is theirs to file. Returns
    /// where on the page to come back to; nothing missing, it returns null and says nothing.
    /// </summary>
    public string? ProceedJoint()
    {
        var flash = Say();
        void Need(string key, string message, string? focus = null)
        {
            flash.Errors[key] = message;
            flash.Focus ??= focus ?? key;
        }

        foreach (var h in JointHolders)
        {
            if (!AutoProofType && View(PoaSlot, h).Used && PoaTypeOf(h).Length == 0) Need(h.Key("poaType"), "Choose the proof of address");
            if (!AutoProofType && View(MailSlot, h).Used && MailTypeOf(h).Length == 0) Need(h.Key("mailType"), "Choose the communication address proof");
            foreach (var v in HolderSlots.Select(d => View(d, h)).Where(v => v.Missing))
                Need(v.Key, "This document is required", "slot-" + v.Key);
            if (NsdlApplies(h) && State.Docs.ContainsKey(h.Key("pan")) && NsdlOf(h) != "verified")
                Need(h.Key("nsdl"), NsdlOf(h) == "failed" ? "NSDL holds no such PAN and date of birth: remove this holder and search again" : "Type the name as printed on the PAN, and ask NSDL again", "read-" + h.Key("nsdl"));
        }
        if (flash.Errors.Count == 0 && flash.Banner is null) Said = null;
        return flash.Focus;
    }

    /// <summary>
    /// What each joint holder's card on Investor Information posted: where their post
    /// goes. Post going back to the permanent address takes the communication address
    /// proof off. A proof's type is not posted: it is what the copy is identified as.
    /// </summary>
    public void KeepJoint(IFormCollection form)
    {
        string Proof(string? type) => ProofsOfAddress.Contains(type) ? type! : "";
        foreach (var h in JointHolders)
        {
            if (!AutoProofType && form.TryGetValue(h.Key("poaType"), out var poa)) SetPoaType(h, Proof(poa));
            if (!MailCanDiffer(h) || !form.TryGetValue(h.Key("mailing"), out var mailing)) continue;
            var different = mailing == "different";
            if (!different) ForgetMail(h);
            var type = !different ? "" : !AutoProofType && form.TryGetValue(h.Key("mailType"), out var mail) ? Proof(mail) : MailTypeOf(h);
            SetMail(h, different, type);
        }
    }

    // Asked before post goes back to the permanent address, when a copy is filed.
    public const string MailDropAsk =
        "Post will go to the permanent address, and the communication address proof uploaded will be removed. Switch to Same as Permanent?";

    // The proof of another address, and what it was read to say, taken off.
    private void ForgetMail(DocHolder h)
    {
        Drop(h.Key("mail"));
        State.Reads[h.Key("mail")] = MailCard();
    }

    // ===== Keeping what was typed ===============================================

    // Every post carries the whole form, so every post keeps it, and applies what
    // each answer settles for the rest of the step. A field shut by the page is not
    // posted, so what it holds is the page's to say, not the post's.
    public void Keep()
    {
        var s = State;

        if (Posted.AppType is Digital or Physical) s.AppType = s.Ckyc ? Digital : Posted.AppType;
        // A digital application has no paper form, and the register will not take an
        // empty field for one: it is filed as 0000. A number typed for a paper
        // application is kept through a change of mind and put back.
        var typed = (Posted.FormNo ?? "").Trim();
        if (typed.Length > 0 && typed != DigitalFormNo) s.TypedFormNo = typed;
        else if (typed.Length == 0 && s.AppType == Physical && Posted.FormNo is not null) s.TypedFormNo = "";
        s.FormNo = s.AppType == Physical ? s.TypedFormNo : DigitalFormNo;
        // A slot the application has no use for keeps nothing.
        if (s.AppType != Physical) Drop("form");

        // A type chosen from the drop-down, when it is not set from the upload.
        if (!AutoProofType && Posted.PoaType is not null) s.PoaType = ProofsOfAddress.Contains(Posted.PoaType) ? Posted.PoaType : "";

        // Post going to the permanent address has nothing else to prove: the proof
        // of another address goes with the answer.
        if (Posted.Mailing is "same" or "different" && MailCanDiffer(Investor))
        {
            var different = Posted.Mailing == "different";
            if (!different) ForgetMail(Investor);
            var type = !different ? "" : !AutoProofType && Posted.MailType is not null ? (ProofsOfAddress.Contains(Posted.MailType) ? Posted.MailType : "") : s.MailPoaType;
            SetMail(Investor, different, type);
        }

        if (Posted.PayMode is not null)
        {
            s.PayMode = PaymentModes.Any(m => m.Name == Posted.PayMode) ? Posted.PayMode : "";
            if (DocumentOf(s.PayMode) is null)
            {
                Drop("payment");
                s.Reads["payment"].Reset();
            }
        }

        KeepSourcing(s);

        if (IsEmployee(s.Category))
        {
            if (Posted.EmpCode is not null) s.EmpCode = Posted.EmpCode.Trim().ToUpperInvariant();
            if (Posted.EmpCompany is not null) s.EmpCompany = Posted.EmpCompany.Trim();
            if (Posted.EmpHolder is not null) s.EmpHolder = EmployeeHolders.Contains(Posted.EmpHolder) ? Posted.EmpHolder : "";
            if (Posted.EmpRelation is not null) s.EmpRelation = EmployeeRelations.Contains(Posted.EmpRelation) ? Posted.EmpRelation : "";
            if (Posted.EmpProofType is not null) s.EmpProofType = EmployeeProofs.Contains(Posted.EmpProofType) ? Posted.EmpProofType : "";
        }
        else
        {
            // Every other category asks none of it, and anything typed goes with the block.
            (s.EmpCode, s.EmpCompany, s.EmpHolder, s.EmpRelation, s.EmpProofType) = ("", "", "", "", "");
            Drop("empproof");
        }
    }

    // The old screen hangs the whole of Additional Details off the sourcing mode.
    // A change of mode is a fresh answer: whatever was typed under the last one
    // goes, the way the old screen empties both fields before it fills them.
    private void KeepSourcing(UploadState s)
    {
        // Nothing here is a partner's other than 1033 to choose; the sub broker is.
        if (!Chooses)
        {
            if (Posted.SubBroker is not null) s.SubBroker = Posted.SubBroker.Trim().ToUpperInvariant();
            Settle(s);
            return;
        }
        if (Posted.Sourcing is null) return;
        var mode = ModeOf(Posted.Sourcing);
        var fresh = Posted.Sourcing != s.Sourcing;
        s.Sourcing = mode?.Code ?? "";

        if (mode is null)
        {
            (s.SourceCode, s.SubBroker, s.Category) = ("", "", "");
            return;
        }

        var postedSource = fresh ? "" : (Posted.SourceCode ?? s.SourceCode).Trim().ToUpperInvariant();
        var postedSub = fresh ? "" : (Posted.SubBroker ?? s.SubBroker).Trim().ToUpperInvariant();

        s.SourceCode = mode.House.Length > 0 ? mode.House : postedSource;
        s.SubBroker = mode.Sub switch
        {
            SubField.House => mode.House,
            SubField.Shut => "",
            SubField.Free => postedSub,
            // Sourced by an employee: the application opens with the code of whoever
            // is at the keyboard, theirs to change under MFL-EX and not under MIBS.
            SubField.EmployeeShut => PartnerCode,
            _ => postedSub.Length > 0 ? postedSub : PartnerCode,
        };

        // What a deposit may be booked as belongs to the mode. A mode with one
        // category settles it; otherwise what was chosen stands if the mode allows it.
        var chosen = Posted.Category ?? s.Category;
        s.Category = mode.Categories.Length == 1 ? mode.Categories[0]
            : mode.Categories.Contains(chosen) ? chosen : "";
    }

    public static bool SubRequired(SourcingMode mode) => mode.Sub is SubField.Employee or SubField.EmployeeShut;

    // ===== Taking a document ===================================================

    private const long Mb = 1024 * 1024;

    // Everything that has to be true of the file itself is checked first: a file
    // turned away for its type or its size never reached the document, so it costs
    // no attempt.
    private async Task TakeAsync(SlotDef def, DocHolder h, IFormFile? file)
    {
        var view = View(def, h);
        if (!view.Used) return;
        string? refuse =
            file is null || file.Length == 0 ? "Choose a file to upload"
            : view.Locked is not null ? view.Locked
            : view.Spent ? $"{MaxAttempts} copies of this document were refused one after another this session, so it is no longer filed here — book a service call to file it."
            : !Accepted(def, file) ? "That file type is not accepted here"
            : file.Length > def.Accepts.MaxMb * Mb ? $"The file is over {def.Accepts.MaxMb} MB — {SizeOf(file.Length)}"
            : null;

        byte[] bytes = [];
        if (refuse is null)
        {
            bytes = await ReadAllAsync(file!);
            // The name and the type the browser gives are only claims: the first
            // bytes say what the file is. Costs no attempt, like any other file problem.
            if (!LooksLike(bytes, file!)) refuse = "That file is not a readable PDF or JPEG";
            else if (def.Key == "photo") refuse = PhotoProblem(bytes);
        }
        if (refuse is not null)
        {
            Say().Errors[view.Key] = refuse;
            return;
        }

        await PutAsync(def, h, file!, bytes);
    }

    private static bool Accepted(SlotDef def, IFormFile file)
    {
        var accepted = def.Accepts.Mime.Split(',');
        if (accepted.Contains(file.ContentType)) return true;
        // Some browsers send a PDF or a JPEG as a bare stream: its name says what it is.
        var byName = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "",
        };
        return accepted.Contains(byName);
    }

    /// <summary>
    /// A posted file, read straight into one array of its own length. The request
    /// size limit has already capped how long that can be.
    /// </summary>
    public static async Task<byte[]> ReadAllAsync(IFormFile file)
    {
        var bytes = new byte[file.Length];
        await using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(bytes);
        return bytes;
    }

    /// <summary>Whether a file's first bytes are those of the type it claims to be.</summary>
    public static bool LooksLike(byte[] bytes, IFormFile file)
    {
        var pdf = bytes.Length >= 5 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F' && bytes[4] == '-';
        var jpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return file.ContentType == "application/pdf" || ext == ".pdf" ? pdf : jpeg;
    }

    public static string SizeOf(long bytes) =>
        bytes < Mb
            ? $"{Math.Max(1, (int)Math.Round(bytes / 1024.0))} KB"
            : (bytes / (double)Mb).ToString(bytes < 10 * Mb ? "0.0" : "0") + " MB";

    // What a photograph has to be to be worth comparing a face against. The
    // dimensions are read off the image itself rather than assumed from its weight.
    private const int MinKb = 15, MaxKb = 2048, MinPx = 150, MaxPx = 4096;

    private static string? PhotoProblem(byte[] bytes)
    {
        var kb = bytes.Length / 1024.0;
        if (kb < MinKb) return $"That photograph is {Math.Round(kb)} KB. A face cannot be compared against anything under {MinKb} KB.";
        if (kb > MaxKb) return $"That photograph is {Math.Round(kb)} KB, over the {MaxKb} KB a photograph may be.";
        if (JpegSize(bytes) is not var (w, h)) return "That file could not be read as a photograph.";
        if (w < MinPx || h < MinPx) return $"That photograph is {w}×{h} pixels. It must be at least {MinPx} pixels on both sides.";
        if (w > MaxPx || h > MaxPx) return $"That photograph is {w}×{h} pixels, over the {MaxPx} a side that can be handled.";
        return null;
    }

    // A JPEG says its size in its start-of-frame marker; nothing else is needed to find it.
    private static (int Width, int Height)? JpegSize(byte[] b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; }
            var length = (b[i + 2] << 8) | b[i + 3];
            // SOF0-SOF15, bar DHT (C4), JPG (C8) and DAC (CC).
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + length;
        }
        return null;
    }

    // What a slot is checked as, for the documents somebody outside answers for,
    // and what a refusal calls it.
    private static readonly Dictionary<string, (DocumentKind Kind, string What)> Checked = new()
    {
        ["pan"] = (DocumentKind.PanCard, "a PAN card"),
        ["poa"] = (DocumentKind.ProofOfAddress, "a proof of address"),
        ["mail"] = (DocumentKind.ProofOfAddress, "a proof of address"),
        ["payment"] = (DocumentKind.Cheque, "a cheque"),
    };

    // Not every document has somebody behind it to ask: the card says who does
    // look at the copy instead, and when.
    private static readonly Dictionary<string, string> NoCheck = new()
    {
        ["form"] = "Filed as handed over. No register outside answers for an application form — Operations check it against the application before the deposit is booked.",
        ["photo"] = "Filed as handed over. No register outside answers for a photograph — Operations compare it with the KYC record before the deposit is booked.",
        ["empproof"] = "Filed as handed over. No register outside answers for an employee proof — Operations check it against the staff register.",
    };

    // From here it is the document that is being checked, and that is what an
    // attempt is spent on. Each outside service is asked in turn, and each attempt
    // is kept in the history as the checks it went through. A copy that gets
    // through them is filed with DMS; one that does not is kept aside there.
    private async Task PutAsync(SlotDef def, DocHolder h, IFormFile file, byte[] bytes)
    {
        var s = State;
        var key = h.Key(def.Key);
        var entry = new LogEntry(Guid.NewGuid().ToString("n")[..8], h.Joint ? $"{def.Label} · {h.Who.Name}" : def.Label,
            s.AttemptsOf(key) + 1, DateTime.Now.ToString("HH:mm:ss"), $"{file.FileName} · {SizeOf(file.Length)}") { Holder = h.Joint ? h.Code : "" };
        s.Log.Insert(0, entry);
        var copy = new UploadFile(Path.GetFileName(file.FileName), file.ContentType, bytes);

        StoredDoc Filed(string check, string kind) =>
            new(copy.FileName, file.Length, file.ContentType, check, kind);

        if (!Checked.TryGetValue(def.Key, out var rule))
        {
            await FileAsync(def, h, copy);
            s.Attempts[key] = 0;
            entry.Add("Filed as handed over — nothing outside answers for this document.");
            entry.End("Filed", "ok");
            s.Docs[key] = Filed(NoCheck.GetValueOrDefault(def.Key, "Filed as handed over."), "");
            return;
        }

        try
        {
            await CheckAsync(def, h, rule, copy, entry, Filed);
        }
        catch (ExternalServiceException e)
        {
            // Nothing was checked, so nothing is filed and no attempt is spent.
            entry.Add($"{e.Service} could not answer: {e.Message}", "bad");
            if (e.TraceId is not null) entry.Add("Trace " + e.TraceId);
            entry.End("Not checked", "bad");
            Say().Errors[key] = $"{e.Message} Nothing was filed, and it does not count as a refusal.";
            Say().ErrorLog[key] = entry.Id;
        }
    }

    private async Task CheckAsync(SlotDef def, DocHolder h, (DocumentKind Kind, string What) rule, UploadFile copy, LogEntry entry, Func<string, string, StoredDoc> filed)
    {
        var s = State;
        var key = h.Key(def.Key);

        // 1. Identification: is it the document it is handed in as? A proof of
        // address is handed in as nothing in particular: which proof it is is what
        // identification says, and that is its type from here on. A payment is
        // handed in as the mode chosen for it.
        var proof = def.Key is "poa" or "mail";
        var detect = proof && AutoProofType;
        var identified = await identifier.IdentifyAsync(rule.Kind, detect ? "" : TypeOf(def, h), copy);
        if (detect && identified.Matches && !ProofsOfAddress.Contains(identified.Type))
            identified = new Identification(false, "It could not be told which proof of address it is. Upload a clearer copy of an Aadhaar, passport, driving licence, voter ID or utility bill.");
        var type = detect ? identified.Type ?? "" : TypeOf(def, h);

        async Task RefuseAsync(string why, string whatNext, params (string Text, string Kind)[] stages)
        {
            var attempts = s.Attempts[key] = s.AttemptsOf(key) + 1;
            var kept = await documents.KeepRefusedAsync(AppNo, FiledUnder(def, h), def.Key, copy);
            // Telling a partner to upload it again when there is nothing left to
            // upload with is worse than saying nothing.
            var message = attempts >= MaxAttempts
                ? $"{why}, and that is {MaxAttempts} refused one after another. It now goes to Operations — book a service call to file it, quoting {kept.Ref}."
                : $"{why}. {whatNext} The copy is kept for a week as {kept.Ref}.";
            foreach (var (text, kind) in stages) entry.Add(text, kind);
            entry.Add($"Copy kept for analysis as {kept.Ref} until {kept.KeptUntil:dd MMM yyyy}, and deleted after.", "warn");
            entry.End("Refused", "bad");
            Say().Errors[key] = message;
            Say().ErrorLog[key] = entry.Id;
        }

        if (!identified.Matches)
        {
            await RefuseAsync($"That does not read as {rule.What}", identified.Hint ?? "Upload a clearer copy.", ($"Not identified as {rule.What}.", "bad"));
            return;
        }
        entry.Add($"Identified as {(proof ? "a proof of address: " + type : rule.What)}.", "ok");

        // 2. OCR. An Aadhaar is filed unmasked: one OCR cannot read all 12 digits of
        // the number off - masked, or too unclear - is not taken.
        var reading = await ocr.ReadAsync(rule.Kind, type, copy, new OcrSubject(h.Who.Pan, h.Who.Dob, h.Who.Name), AadhaarConsent);
        if (proof && type == "Aadhaar" && !AadhaarNumbers.IsWhole(reading.IdNumber))
        {
            await RefuseAsync("OCR could not read all 12 digits of the Aadhaar number, so the copy may be masked",
                "The application needs the unmasked Aadhaar, with all 12 digits readable — upload that.",
                ("OCR read no whole 12-digit Aadhaar number off it: masked, or not clear enough.", "bad"),
                ("An Aadhaar is filed unmasked, so this copy was not taken.", "bad"));
            return;
        }

        // A joint holder with no folio is put to NSDL with the name the copy reads.
        if (def.Key == "pan" && NsdlApplies(h))
        {
            await NsdlAsync(h, InvestorIdentificationViewModel.NormaliseName(reading.Name), entry, typed: false);
            h = JointHolder(h.Code)!;
        }

        if (detect)
        {
            // Taken: the proof's type is what it was identified as, whatever it was before.
            if (def.Key == "poa") SetPoaType(h, type);
            else SetMail(h, true, type);
            entry.Add($"Its type is set to {type} from the copy.", "ok");
        }

        // 3. Whoever answers for what was read.
        var (check, kind) = def.Key switch
        {
            "pan" => await ReadPanAsync(h, reading, entry),
            "poa" or "mail" => await ReadAddressAsync(def, h, reading, entry),
            _ => await ReadInstrumentAsync(reading, entry),
        };
        await FileAsync(def, h, copy);
        s.Docs[key] = filed(check, kind);
        // Taken, so whatever was refused before it is behind the partner.
        s.Attempts[key] = 0;
    }

    // A slot holds one copy in DMS: a copy filed before is deleted, then the new
    // one filed. One that came over from the step before has no copy here.
    private async Task FileAsync(SlotDef def, DocHolder h, UploadFile copy)
    {
        var under = FiledUnder(def, h);
        if (State.Docs.GetValueOrDefault(h.Key(def.Key)) is { Before: false }) await documents.DeleteAsync(AppNo, under, def.Key);
        await documents.FileAsync(AppNo, under, def.Key, copy);
    }

    private static string Cap(string words) => char.ToUpperInvariant(words[0]) + words[1..];

    // The issuer behind that proof is asked whether the address OCR read is the
    // one they hold. Only a clean answer replaces the address the application carries.
    private async Task<(string, string)> ReadAddressAsync(SlotDef def, DocHolder h, OcrReading reading, LogEntry entry)
    {
        // The permanent address, or the communication address where post goes elsewhere.
        var mailing = def.Key == "mail";
        var what = mailing ? "communication address" : "address";
        var card = mailing ? MailReadOf(h) : State.Reads[h.Key("poa")];
        var type = TypeOf(def, h);
        var named = type.Length > 0 ? type.ToLowerInvariant() : "proof";
        entry.Add("OCR read: " + reading.Address);
        // The investor's gender, where the folio gives none, sets the category.
        if (!h.Joint && Who.Gender.Length == 0 && reading.Gender.Length > 0 && State.Gender != reading.Gender)
        {
            State.Gender = reading.Gender;
            entry.Add($"Gender read: {reading.Gender}.");
        }
        var answer = await verification.ConfirmProofAsync(type, reading, h.Who.Dob);
        var issuer = answer.Verifier;

        // An Aadhaar carries the number the PAN-Aadhaar link is asked with, so a PAN
        // already on the application can be asked about now.
        if (type == "Aadhaar" && AadhaarNumbers.IsWhole(reading.IdNumber) && LinkApplies(h))
        {
            session.SetString(AadhaarKey(h), reading.IdNumber);
            if (PanOnApplication(h) && (!NsdlApplies(h) || NsdlOf(h) == "verified")) await RelinkPanAsync(h, entry);
            else if (PanOnApplication(h))
            {
                // The Aadhaar number is kept, and asked with once NSDL verifies the PAN.
                LinkWaitsOnNsdl(h);
                entry.Add("The PAN-Aadhaar link waits until NSDL verifies the PAN; this Aadhaar is asked with then.", "warn");
            }
        }

        if (answer.NotAsked is { } why)
        {
            (card.State, card.Kind) = ("With Operations", "is-failed");
            card.From = $"{Cap(issuer)} could not be asked: {why}. The {what} is left as it stands for Operations to settle.";
            entry.Add($"{Cap(issuer)} not asked: {why}.", "warn");
            entry.End($"Filed, {what} unchanged", "warn");
            return ($"Read, but {issuer} could not be asked: {why}. The copy is filed and Operations settle the {what}; the application keeps the one it carries until they do.", "warn");
        }

        if (issuer.Length == 0)
        {
            (card.State, card.Kind) = ("With Operations", "is-failed");
            card.From = $"A {named} has no issuer to check with, so the {what} is left as it stands for Operations to settle.";
            entry.Add($"A {named} has no register behind it to put that address to.", "warn");
            entry.End($"Filed, {what} unchanged", "warn");
            return ($"Read, but nothing outside answers for a {named}. The copy is filed and Operations settle the {what}; the application keeps the one it carries until they do.", "warn");
        }
        if (!answer.Confirmed)
        {
            (card.State, card.Kind) = ("Not confirmed", "is-failed");
            card.From = $"{issuer} did not confirm the address on this proof, so the application keeps the {what} it carries. Upload a clearer copy, or a different proof.";
            entry.Add($"{issuer} did not confirm that address.", "bad");
            entry.End($"Filed, {what} unchanged", "warn");
            return ($"Read, but {issuer} did not confirm what it says. The copy is filed and the application keeps the {what} it carries — upload a clearer copy, or another proof.", "bad");
        }
        // A communication address read for the first time replaces nothing.
        var before = mailing && card.Kind != "is-done" ? "" : card.Lines;
        (card.Lines, card.Was) = (reading.Address, before);
        (card.State, card.Kind) = ($"Verified with {issuer}", "is-done");
        card.From = $"Read off the {named} filed above and confirmed with {issuer}.";
        entry.Add($"{issuer} confirmed that address.", "ok");
        entry.Add(before.Length > 0 ? $"{Cap(what)} on the application replaced. Was: " + before : $"{Cap(what)} on the application set.", "ok");
        entry.End("Filed", "ok");
        return ($"Identified, read and confirmed with {issuer}. The {what} on the application now comes from this proof.", "ok");
    }

    // A cheque carries an account rather than an address, and it is the bank it is
    // drawn on that is asked to stand behind it.
    private async Task<(string, string)> ReadInstrumentAsync(OcrReading reading, LogEntry entry)
    {
        var card = State.Reads["payment"];
        var mode = State.PayMode.Length > 0 ? State.PayMode.ToLowerInvariant() : "cheque";
        entry.Add("OCR read: " + reading.Account);
        var answer = await verification.ConfirmAccountAsync(reading.Account, reading.Bank);
        var bank = answer.Verifier.Length > 0 ? answer.Verifier : "The bank";
        if (!answer.Confirmed)
        {
            (card.State, card.Kind) = ("Not confirmed", "is-failed");
            card.From = $"{bank} did not confirm that account against this {mode}, so nothing is carried forward. Upload a clearer copy of the instrument.";
            entry.Add($"{bank} did not confirm that account.", "bad");
            entry.End("Filed, account not carried", "warn");
            return ($"Read, but {bank} did not confirm the account on this {mode}. The copy is filed and no account is carried to Bank Details & Payment.", "bad");
        }
        card.Lines = reading.Account;
        (card.State, card.Kind) = ($"Confirmed with {bank}", "is-done");
        card.From = $"Read off the {mode} filed above and confirmed with {bank}. Bank Details & Payment opens with this account.";
        entry.Add($"{bank} confirmed that account.", "ok");
        entry.Add("Account carried to Bank Details & Payment.", "ok");
        entry.End("Filed", "ok");
        return ($"Identified, read and confirmed with {bank}. Bank Details & Payment opens with this account.", "ok");
    }

    // A PAN copy is read for the number on it, and - for a holder with no folio -
    // the PAN-Aadhaar link is asked whether an Aadhaar is held against their PAN,
    // once there is an Aadhaar number on the application to ask with. An unlinked PAN does not stop
    // the application: TDS runs at the higher rate until the investor links it.
    private async Task<(string, string)> ReadPanAsync(DocHolder h, OcrReading reading, LogEntry entry)
    {
        entry.Add("OCR read: PAN " + Mask(reading.Pan.Length > 0 ? reading.Pan : h.Who.Pan));
        if (NsdlApplies(h) && NsdlOf(h) != "verified")
        {
            // Not verified: the link waits for NSDL, and the copy and its card say why.
            LinkWaitsOnNsdl(h);
            entry.Add("The PAN-Aadhaar link waits until NSDL verifies the PAN.", "warn");
            entry.End("Filed, not verified", NsdlOf(h) == "failed" ? "bad" : "warn");
            return NsdlOf(h) == "failed"
                ? ("Filed, but NSDL holds no record of this PAN against the date of birth searched. Remove this holder and search again.", "bad")
                : ("Filed, but NSDL does not hold this PAN against the name read off it. Type the name as printed on the card, in the NSDL card below.", "warn");
        }
        if (!LinkApplies(h))
        {
            // On a folio: the link is not asked.
            entry.End("Filed", "ok");
            return ($"Identified and read as PAN {Mask(h.Who.Pan)}.", "ok");
        }
        var link = await LinkAsync(h, entry);
        entry.End(link switch
        {
            PanAadhaarLink.Linked => "Filed",
            PanAadhaarLink.NotLinked => "Filed, PAN not linked",
            _ => "Filed, link not checked",
        }, link == PanAadhaarLink.Linked ? "ok" : "warn");
        return PanCheck(h, link);
    }

    // A joint holder's PAN on the application but not verified with NSDL: the link
    // card says what it waits on, rather than still asking for the PAN copy.
    private void LinkWaitsOnNsdl(DocHolder h)
    {
        var card = State.Reads[h.Key("pan")];
        card.Lines = Mask(h.Who.Pan) + " · link with Aadhaar not asked yet";
        (card.State, card.Kind, card.From) = NsdlOf(h) == "failed"
            ? ("Not asked", "is-failed", "NSDL did not verify the PAN, so the link is not asked.")
            : ("Waiting on NSDL", "", $"Asked once NSDL verifies the PAN{(AadhaarOf(h).Length > 0 ? ", with the Aadhaar already read" : "")}.");
    }

    // NSDL asked about a joint holder's PAN, date of birth and a name; verified,
    // the name is theirs from here on.
    private async Task NsdlAsync(DocHolder h, string name, LogEntry entry, bool typed)
    {
        var j = State.Joint[h.Code];
        var answer = await nsdl.VerifyAsync(h.Who.Pan, h.Who.Dob, name);
        j.NsdlName = name;
        j.Nsdl = !answer.PairOk ? "failed" : answer.NameOk ? "verified" : "name";
        entry.Add(answer.PairOk ? "NSDL holds the PAN against the date of birth." : "NSDL holds no such PAN and date of birth.", answer.PairOk ? "ok" : "bad");
        if (answer.PairOk)
            entry.Add(answer.NameOk ? $"NSDL holds it against {name}{(typed ? ", as typed" : "")}." : $"NSDL does not hold it against {name}{(typed ? ", as typed" : "")}.", answer.NameOk ? "ok" : "warn");
        if (j.Nsdl == "verified") j.Holder = j.Holder with { Name = name };
    }

    /// <summary>
    /// The name printed on a joint holder's PAN card, typed where NSDL did not hold
    /// the PAN against the name OCR read, and put to NSDL again. Returns where on
    /// the page to come back to.
    /// </summary>
    public async Task<string?> RetryNsdlAsync(DocHolder h, string? typed)
    {
        var key = h.Key("nsdl");
        if (!NsdlApplies(h) || NsdlOf(h) != "name" || State.Docs.GetValueOrDefault(h.Key("pan")) is not { } doc) return "read-" + key;
        var name = InvestorIdentificationViewModel.NormaliseName(typed);
        if (name.Length < 3)
        {
            Say().Errors[key] = "Enter the name as printed on the PAN";
            return "read-" + key;
        }
        var entry = new LogEntry(Guid.NewGuid().ToString("n")[..8], $"{PanSlot.Label} · NSDL again", 1,
            DateTime.Now.ToString("HH:mm:ss"), $"Name typed from the PAN card: {name}") { Holder = h.Code };
        State.Log.Insert(0, entry);
        try
        {
            await NsdlAsync(h, name, entry, typed: true);
        }
        catch (ExternalServiceException e)
        {
            entry.Add($"{e.Service} could not answer: {e.Message}", "bad");
            entry.End("Not checked", "bad");
            Say().Errors[key] = e.Message;
            return "read-" + key;
        }
        h = JointHolder(h.Code)!;
        if (NsdlOf(h) != "verified")
        {
            entry.End("Not verified", "warn");
            Say().Errors[key] = $"NSDL does not hold PAN {Mask(h.Who.Pan)} against that name";
            return "read-" + key;
        }
        // Verified: the link can be asked now, and the copy says what came of it.
        var link = await LinkAsync(h, entry);
        var (check, kind) = PanCheck(h, link);
        State.Docs[h.Key("pan")] = doc with { Check = check, CheckKind = kind };
        entry.End("Verified", "ok");
        return "read-" + key;
    }

    private bool PanOnApplication(DocHolder h) => h.Who.PanFiled || State.Docs.ContainsKey(h.Key("pan"));

    // The Aadhaar arrived after the PAN: the link is asked now, and the PAN's card
    // and what its copy says both follow the answer.
    private async Task RelinkPanAsync(DocHolder h, LogEntry entry)
    {
        var link = await LinkAsync(h, entry);
        if (State.Docs.GetValueOrDefault(h.Key("pan")) is { Before: false } doc)
        {
            var (check, kind) = PanCheck(h, link);
            State.Docs[h.Key("pan")] = doc with { Check = check, CheckKind = kind };
        }
    }

    // Null when the link check could not answer. The PAN or the Aadhaar that
    // prompted it is filed all the same: the link is only ever a note on the PAN.
    private async Task<PanAadhaarLink?> LinkAsync(DocHolder h, LogEntry entry)
    {
        var card = State.Reads[h.Key("pan")];
        var pan = Mask(h.Who.Pan);
        PanAadhaarLink link;
        try
        {
            link = await panLink.CheckAsync(h.Who.Pan, AadhaarOf(h));
        }
        catch (ExternalServiceException e)
        {
            card.Lines = pan + " · link with Aadhaar not checked";
            (card.State, card.Kind) = ("Link not checked", "");
            card.From = $"The PAN-Aadhaar link could not be asked just now: {e.Message}";
            entry.Add($"{e.Service} could not answer the PAN-Aadhaar link: {e.Message}", "warn");
            return null;
        }
        switch (link)
        {
            case PanAadhaarLink.Linked:
                card.Lines = pan + " · linked with Aadhaar";
                (card.State, card.Kind) = ("Linked with Aadhaar", "is-done");
                card.From = $"Confirmed with {PanAuthority} against the Aadhaar read on this application.";
                entry.Add($"{Cap(PanAuthority)} holds an Aadhaar against this PAN.", "ok");
                break;
            case PanAadhaarLink.NotLinked:
                card.Lines = pan + " · not linked with Aadhaar";
                (card.State, card.Kind) = ("Not linked", "is-failed");
                card.From = $"{Cap(PanAuthority)} holds no Aadhaar against this PAN. The deposit can still be booked, but TDS runs at the higher rate until the investor links it.";
                entry.Add("No Aadhaar against this PAN.", "warn");
                break;
            default:
                card.Lines = pan + " · link with Aadhaar not checked yet";
                (card.State, card.Kind) = ("Link not checked", "");
                card.From = LinkWaitsShort;
                entry.Add("No Aadhaar number read on this application yet, so the PAN-Aadhaar link is not asked.", "warn");
                break;
        }
        return link;
    }

    private static (string, string) PanCheck(DocHolder h, PanAadhaarLink? link) => (Mask(h.Who.Pan), link) switch
    {
        (var pan, PanAadhaarLink.Linked) => ($"Identified, read as PAN {pan} and confirmed with {PanAuthority}: an Aadhaar is held against it.", "ok"),
        (var pan, PanAadhaarLink.NotLinked) => ($"Read as PAN {pan}, but {PanAuthority} holds no Aadhaar against it. The copy is filed and the deposit can be booked — TDS runs at the higher rate until the {(h.Joint ? "holder" : "investor")} links it.", "warn"),
        (var pan, PanAadhaarLink.NeedsAadhaar) => ($"Identified and read as PAN {pan}. {LinkWaits}", "warn"),
        (var pan, _) => ($"Identified and read as PAN {pan}. The PAN-Aadhaar link could not be asked just now; it is asked again when an Aadhaar is next filed.", "warn"),
    };

    // ===== What the page says about the rest ===================================

    /// <summary>The name a register holds against a code field, or why there is none.</summary>
    public (string Text, bool Found)? Resolved(SourcingMode? mode, bool sub)
    {
        var value = sub ? State.SubBroker : State.SourceCode;
        if (mode is null || value.Length == 0) return null;
        var house = sub ? (mode.Sub == SubField.House ? mode.House : "") : mode.House;
        if (house.Length > 0 && value.Equals(house, StringComparison.OrdinalIgnoreCase))
            return ("Stands for the sourcing mode itself — filled here, not typed.", false);
        var register = RegisterOf(mode, sub);
        var found = register.FirstOrDefault(p => p.Code.Equals(value, StringComparison.OrdinalIgnoreCase));
        return found is not null
            ? ($"{(sub ? "Sub Broker Name" : mode.NameLabel)} — {found.Name}", true)
            : ("No name against this code here. You can still proceed — Operations check it before the deposit is booked.", false);
    }

    /// <summary>What a code field is searched against under the mode, if it is searched at all.</summary>
    public IReadOnlyList<Party> RegisterOf(SourcingMode? mode, bool sub)
    {
        if (mode is null || mode.Search != (sub ? "sub" : "source")) return [];
        return mode.Register switch
        {
            Register.Brokers => Brokers,
            Register.Employees => Staff,
            _ => [],
        };
    }

    /// <summary>What Proceed would ask for, in the order it asks, so the footer can say what is next.</summary>
    public List<string> Outstanding()
    {
        var s = State;
        var left = new List<string>();
        var mode = ModeOf(s.Sourcing);
        bool Missing(SlotDef d) => View(d).Missing;

        if (Missing(FormSlot)) left.Add("the application form");
        if (Missing(PanSlot)) left.Add("the PAN copy");
        if (!AutoProofType && View(PoaSlot).Used && s.PoaType.Length == 0) left.Add("the proof of address type");
        if (Missing(PoaSlot)) left.Add("the proof of address");
        if (Missing(PhotoSlot)) left.Add("the photograph");
        if (!AutoProofType && View(MailSlot).Used && s.MailPoaType.Length == 0) left.Add("the communication address proof type");
        if (Missing(MailSlot)) left.Add("the communication address proof");
        if (s.PayMode.Length == 0) left.Add("the payment mode");
        if (Missing(PaymentSlot)) left.Add("the instrument copy");
        if (mode is null) left.Add("the sourcing mode");
        else
        {
            if (s.SourceCode.Length == 0) left.Add("the " + mode.CodeLabel.ToLowerInvariant());
            if (SubRequired(mode) && s.SubBroker.Length == 0) left.Add("the sub broker code");
        }
        if (s.Category.Length == 0) left.Add("the deposit category");
        if (IsEmployee(s.Category))
        {
            if (s.EmpCode.Length == 0) left.Add("the employee code");
            if (s.EmpCompany.Length == 0) left.Add("the employee company");
            if (s.EmpHolder.Length == 0) left.Add("the employee holder");
            if (s.EmpRelation.Length == 0) left.Add("the relation with the holder");
            if (s.EmpProofType.Length == 0) left.Add("the employee proof type");
            if (Missing(EmpProofSlot)) left.Add("the employee proof");
        }
        if (s.AppType == Physical && s.TypedFormNo.Length == 0) left.Add("the form number");
        return left;
    }
}

/// <summary>What the upload step's form posts. Every post carries all of it; a
/// field the page shut is not posted, and stays null.</summary>
public sealed class UploadForm
{
    public string? AppType { get; set; }
    /// <summary>The proof of address type, posted only while it is chosen rather than set from the upload.</summary>
    public string? PoaType { get; set; }

    /// <summary>Where post goes: "same" as the permanent address, or "different".</summary>
    public string? Mailing { get; set; }

    /// <summary>The communication address proof type, posted only while it is chosen.</summary>
    public string? MailType { get; set; }
    public string? PayMode { get; set; }
    public string? Sourcing { get; set; }
    public string? SourceCode { get; set; }
    public string? SubBroker { get; set; }
    public string? Category { get; set; }
    public string? EmpCode { get; set; }
    public string? EmpCompany { get; set; }
    public string? EmpHolder { get; set; }
    public string? EmpRelation { get; set; }
    public string? EmpProofType { get; set; }
    public string? FormNo { get; set; }

    /// <summary>The slot whose Upload was pressed.</summary>
    public string? Slot { get; set; }

    /// <summary>The control whose change redrew the page, so the page comes back to it.</summary>
    public string? Refresh { get; set; }
}
