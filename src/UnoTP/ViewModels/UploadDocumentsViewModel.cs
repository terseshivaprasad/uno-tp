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
/// A document's checks are outside services, each asked in turn: identification,
/// then masking for an Aadhaar, OCR, and whoever answers for what was read - the
/// issuer or the bank - or for a PAN, the PAN-Aadhaar link. Only then is the copy
/// filed with DMS (see <see cref="IDocumentApi"/>).
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
    IMaskingService masking,
    IOcrService ocr,
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

    // The same masking the register uses: a PAN keeps its first five and last
    // character, a date of birth only its year.
    private static string Mask(string pan) =>
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

    // An employee deposit is booked against a staff record, which is what the
    // block of employee fields is for.
    public const string EmployeeCategory = "EMPLOYEE";

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

    private static readonly string[] RetailAndTrust = { "INDIVIDUAL", "SENIOR CITIZEN", "TRUST" };

    /// <summary>
    /// The four modes the old screen offers a partner, in its own order. The
    /// screen posts them by number and hangs everything off that number: BROKER
    /// is the one where the code is typed, and each of the other three stands its
    /// own house code in the field and asks for nothing there.
    /// </summary>
    public static readonly SourcingMode[] SourcingModes =
    {
        new("2", "BROKER", "Broker Code", "Broker Name",
            "", "source", Register.Brokers, SubField.Free, RetailAndTrust),
        new("1", "MMFSS - BRANCH", "Sourcing Employee Code", "Sourcing Employee Name",
            "MFL", "", Register.None, SubField.House, RetailAndTrust),
        new("5", "MFL-EX", "Sourcing Employee Code", "Sourcing Employee Name",
            "MFL-EX", "sub", Register.Employees, SubField.Employee, new[] { EmployeeCategory }),
        new("6", "MFIS/FD", "Sourcing Employee Code", "Sourcing Employee Name",
            "MIBS", "sub", Register.Employees, SubField.EmployeeShut, RetailAndTrust),
    };

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

    private const string LinkWaits =
        "Whether an Aadhaar is linked to it is asked once an Aadhaar is read on this application — file one as the proof of address.";

    // The holder's consent to their Aadhaar being read and checked, which IDfy asks
    // for on every Aadhaar call. The step does not ask the partner for it, so it is
    // never given: behind a real IDfy an Aadhaar is turned back, saying why. The
    // mock does not ask.
    private const bool AadhaarConsent = false;

    // The Aadhaar number OCR read on this application, for asking the PAN-Aadhaar
    // link with. An Aadhaar number is not to be stored, so it is never saved with
    // the application: it is held in the server's session, and goes with it.
    private string AadhaarNumber
    {
        get => session.GetString("aadhaar:" + AppNo) ?? "";
        set => session.SetString("aadhaar:" + AppNo, value);
    }

    /// <summary>What the register already holds against the folio the application
    /// was opened on. A document on the folio is not asked for again: the step
    /// shows it as not applicable and says which folio carries it.</summary>
    public DocsOnRecord? FolioDocs => HasFolio ? Who.OnRecord : null;

    // ===== The documents =======================================================

    /// <summary>A document the step asks for, by the key its markup and posts carry.</summary>
    public record SlotDef(string Key, string Label, Accepts Accepts);

    public static readonly SlotDef FormSlot = new("form", "Application Form", Form);
    public static readonly SlotDef PanSlot = new("pan", "PAN copy", Proof);
    public static readonly SlotDef PhotoSlot = new("photo", "photograph", Image);
    public static readonly SlotDef PoaSlot = new("poa", "POA", Image);
    public static readonly SlotDef PaymentSlot = new("payment", "the instrument", Image);
    public static readonly SlotDef EmpProofSlot = new("empproof", "Employee Proof", Proof);

    public static readonly SlotDef[] Slots = [FormSlot, PanSlot, PhotoSlot, PoaSlot, PaymentSlot, EmpProofSlot];

    /// <summary>Three refusals in a row and a document goes to Operations.</summary>
    public const int MaxAttempts = 3;

    /// <summary>What a slot shows: whether it is asked for at all, and what is in it.</summary>
    public sealed record SlotView(
        SlotDef Def, bool Used, string? NotApplicable, string? Locked, StoredDoc? Doc,
        string? Must, string? With, int Attempts, string? Error, string? ErrorLog)
    {
        public string Key => Def.Key;
        public bool Spent => Attempts >= MaxAttempts;
        public string Title => Def.Key == "payment" ? "the cheque" : Def.Label;
        public bool Unconfirmed => Doc?.CheckKind is "warn" or "bad";

        public string? Tries => Attempts == 0 ? null
            : Spent ? $"{MaxAttempts} refused one after another — this document now goes to Operations."
            : $"{Attempts} of {MaxAttempts} refused in a row this session — a copy the checks take clears it.";
    }

    private const string CkycWhy =
        "CKYC supplies this once the investor consents, which is asked for after the application is completed. It is not uploaded here.";

    private const string Unmasked = "Unmasked copy only — all 12 digits must be readable.";

    // The three checks are named on the empty box too: a partner who knows UIDAI
    // will be asked reaches for the copy UIDAI would recognise.
    private const string Run = "Once uploaded: identified, read by OCR, then ";

    public SlotView View(SlotDef def)
    {
        var s = State;
        var held = FolioDocs;
        string heldWhy = $"Already held against folio {Folio}, so it is not filed again.";
        var (used, na) = def.Key switch
        {
            "form" => (s.AppType == Physical, "A digital application is accepted through the investor’s own link, so there is no signed form to file."),
            "pan" => held?.Pan == true ? (false, heldWhy) : (true, null),
            "photo" => held?.Photo == true ? (false, heldWhy) : s.Ckyc ? (false, CkycWhy) : (true, null),
            "poa" => held?.Poa == true ? (false, heldWhy) : s.Ckyc ? (false, CkycWhy) : (true, null),
            "payment" => (DocumentOf(s.PayMode) is not null, $"{(s.PayMode.Length > 0 ? s.PayMode : "This mode")} is settled electronically, so there is no instrument to copy."),
            "empproof" => (s.Category == EmployeeCategory, "Only a deposit booked against a staff record carries an employee proof."),
            _ => (true, (string?)null),
        };

        // A proof is checked as whatever type was chosen above it, so there is
        // nothing to check it as until one is.
        string? locked = !used ? null : def.Key switch
        {
            "poa" when s.PoaType.Length == 0 => "Choose the proof of address first",
            "empproof" when s.EmpProofType.Length == 0 => "Choose the employee proof first",
            _ => null,
        };

        string? with = def.Key switch
        {
            "pan" => Run + $"checked with {PanAuthority}.",
            "payment" => Run + "the account is confirmed with the bank it is drawn on.",
            "poa" when Issuers.TryGetValue(s.PoaType, out var issuer) => issuer.Length > 0
                ? Run + $"the address is confirmed with {issuer}."
                : $"Once uploaded: identified and read by OCR. A {s.PoaType.ToLowerInvariant()} has no issuer to confirm the address with, so Operations settle it.",
            _ => null,
        };

        var flash = Shown;
        return new SlotView(def, used, used ? null : na, locked,
            used ? s.Docs.GetValueOrDefault(def.Key) : null,
            def.Key == "poa" && s.PoaType == "Aadhaar" ? Unmasked : null,
            with, s.AttemptsOf(def.Key),
            flash?.Errors.GetValueOrDefault(def.Key), flash?.ErrorLog.GetValueOrDefault(def.Key));
    }

    public static string? DocumentOf(string payMode) =>
        PaymentModes.FirstOrDefault(m => m.Name == payMode)?.Document;

    public static SourcingMode? ModeOf(string code) => SourcingModes.FirstOrDefault(m => m.Code == code);

    // ===== The state of this application =======================================

    /// <summary>What the step holds against this application, opened on first sight.</summary>
    public UploadState State => App.Upload ??= Open();

    // A new application opens with the reads as the step before left them, the
    // PAN copy that came over with it, and the attempts the backend has on record
    // from before this page - which are on the record without counting against
    // the three.
    private UploadState Open()
    {
        var s = new UploadState();
        s.Reads["pan"] = PanFiled
            ? new ReadCard("Link not checked", HolderPan + " · link with Aadhaar not checked yet",
                $"Confirmed with NSDL when the PAN was identified. {LinkWaits}")
            : new ReadCard("Not yet read", "Read off the PAN copy once one is filed here.",
                $"{Cap(PanAuthority)} is asked whether it holds an Aadhaar against this PAN once an Aadhaar number is read on this application.");
        s.Reads["poa"] = PermanentAddress.Length > 0
            ? new ReadCard("On the application", PermanentAddress,
                "From the folio the application was opened against.")
            : new ReadCard("Not on record", "No address is held for this investor yet.",
                "Read off the proof of address once one is filed here.");
        s.Reads["payment"] = new ReadCard("Not yet read", "Read off the instrument once a copy is filed.",
            "The account the deposit is paid from. Bank Details & Payment opens with whatever is confirmed here.");

        if (PanFiled)
        {
            s.Docs["pan"] = new StoredDoc("PAN copy on the application", 0, "",
                "Identified, read and confirmed with NSDL when the PAN was established on the step before.", "ok", Before: true);
        }

        s.Log.AddRange(App.Prior);
        return s;
    }

    /// <summary>What the last post left to say, taken out of the session as the page is drawn.</summary>
    public Flash? Shown { get; set; }

    /// <summary>What this post has to say, kept for the page it redirects to.</summary>
    public Flash? Said { get; set; }

    /// <summary>Set once Proceed finds everything in: the post moves on to Investor Information.</summary>
    public bool Complete { get; private set; }

    /// <summary>The documents this post took off the application. Their copies are
    /// deleted from DMS once the save that takes them off has gone through.</summary>
    public IReadOnlyCollection<string> Dropped => dropped;

    private readonly HashSet<string> dropped = [];

    private void Drop(string slot)
    {
        if (State.Docs.Remove(slot, out var doc) && !doc.Before) dropped.Add(slot);
    }

    // ===== What the form posts ================================================

    /// <summary>What this post carried; every post carries the whole form.</summary>
    public UploadForm Posted { get; set; } = new();

    // ===== What a post does ===================================================

    /// <summary>Upload: the file posted for the slot whose button was pressed.
    /// Returns where on the page to come back to.</summary>
    public async Task<string?> UploadAsync(IFormFileCollection files)
    {
        var def = Slots.FirstOrDefault(d => d.Key == Posted.Slot);
        if (def is null) return null;
        Keep();
        await TakeAsync(def, files.GetFile("file_" + def.Key));
        return "slot-" + def.Key;
    }

    // Nothing is requested of the investor from this step: choosing CKYC only says
    // how the KYC will arrive. The address and the photograph come with that
    // record, so those pickers stop being asked for; the PAN copy does not. It
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
            Say().Banner = "The address and photograph will come from CKYC once the investor consents.";
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
        if (View(PoaSlot).Used) Need(s.PoaType.Length > 0, "cudPoaType", "Choose the proof of address");
        Need(s.PayMode.Length > 0, "cudPayMode", "Choose the payment mode");
        Need(s.Sourcing.Length > 0, "cudSourcing", "Choose the sourcing mode");
        if (mode is not null)
        {
            Need(s.SourceCode.Length > 0, "cudSourceCode", $"Enter the {mode.CodeLabel.ToLowerInvariant()}");
            if (SubRequired(mode)) Need(s.SubBroker.Length > 0, "cudSubBroker", "Enter the sub broker code");
        }
        Need(s.Category.Length > 0, "cudCategory", "Choose the deposit category");
        if (s.Category == EmployeeCategory)
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

        foreach (var slot in Slots.Select(View).Where(v => v.Used && v.Doc is null))
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

        if (Posted.PoaType is not null) s.PoaType = ProofsOfAddress.Contains(Posted.PoaType) ? Posted.PoaType : "";

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

        if (s.Category == EmployeeCategory)
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
    private async Task TakeAsync(SlotDef def, IFormFile? file)
    {
        var view = View(def);
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
            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms);
            bytes = ms.ToArray();
            // The name and the type the browser gives are only claims: the first
            // bytes say what the file is. Costs no attempt, like any other file problem.
            if (!LooksLike(bytes, file!)) refuse = "That file is not a readable PDF or JPEG";
            else if (def.Key == "photo") refuse = PhotoProblem(bytes);
        }
        if (refuse is not null)
        {
            Say().Errors[def.Key] = refuse;
            return;
        }

        await PutAsync(def, file!, bytes);
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
    private async Task PutAsync(SlotDef def, IFormFile file, byte[] bytes)
    {
        var s = State;
        var entry = new LogEntry(Guid.NewGuid().ToString("n")[..8], def.Label, s.AttemptsOf(def.Key) + 1,
            DateTime.Now.ToString("HH:mm:ss"), $"{file.FileName} · {SizeOf(file.Length)}");
        s.Log.Insert(0, entry);
        var copy = new UploadFile(Path.GetFileName(file.FileName), file.ContentType, bytes);

        StoredDoc Filed(string check, string kind) =>
            new(copy.FileName, file.Length, file.ContentType, check, kind);

        if (!Checked.TryGetValue(def.Key, out var rule))
        {
            await FileAsync(def.Key, copy);
            s.Attempts[def.Key] = 0;
            entry.Add("Filed as handed over — nothing outside answers for this document.");
            entry.End("Filed", "ok");
            s.Docs[def.Key] = Filed(NoCheck.GetValueOrDefault(def.Key, "Filed as handed over."), "");
            return;
        }

        try
        {
            await CheckAsync(def, rule, copy, entry, Filed);
        }
        catch (ExternalServiceException e)
        {
            // Nothing was checked, so nothing is filed and no attempt is spent.
            entry.Add($"{e.Service} could not answer: {e.Message}", "bad");
            if (e.TraceId is not null) entry.Add("Trace " + e.TraceId);
            entry.End("Not checked", "bad");
            Say().Errors[def.Key] = $"{e.Message} Nothing was filed, and it does not count as a refusal.";
            Say().ErrorLog[def.Key] = entry.Id;
        }
    }

    private async Task CheckAsync(SlotDef def, (DocumentKind Kind, string What) rule, UploadFile copy, LogEntry entry, Func<string, string, StoredDoc> filed)
    {
        var s = State;

        // 1. Identification: is it the document it is handed in as? The type
        // chosen above the box is what it is handed in as within its kind.
        var type = def.Key == "poa" ? s.PoaType : def.Key == "payment" ? s.PayMode : "";
        var identified = await identifier.IdentifyAsync(rule.Kind, type, copy);

        // 2. Masking: an Aadhaar is filed unmasked.
        var masked = identified.Matches && def.Key == "poa" && s.PoaType == "Aadhaar" && await masking.IsMaskedAsync(copy, AadhaarConsent);

        if (!identified.Matches || masked)
        {
            var attempts = s.Attempts[def.Key] = s.AttemptsOf(def.Key) + 1;
            var spent = attempts >= MaxAttempts;
            var kept = await documents.KeepRefusedAsync(AppNo, def.Key, copy);
            string message;
            if (masked)
            {
                message = "That Aadhaar is masked. The application needs the unmasked copy, with all 12 digits readable — "
                    + (spent ? $"and that is {MaxAttempts} refused one after another, so it now goes to Operations. Book a service call to file it, quoting {kept.Ref}."
                             : $"upload it again. The copy is kept for a week as {kept.Ref}.");
                entry.Add("Identified as an Aadhaar, masked.", "bad");
                entry.Add("An Aadhaar is filed unmasked, so this copy was not taken.", "bad");
            }
            else
            {
                // Telling a partner to upload it again when there is nothing left to
                // upload with is worse than saying nothing.
                message = spent
                    ? $"That does not read as {rule.What}, and that is {MaxAttempts} refused one after another. It now goes to Operations — book a service call to file it, quoting {kept.Ref}."
                    : $"That does not read as {rule.What}. {identified.Hint ?? "Upload a clearer copy."} The copy is kept for a week as {kept.Ref}.";
                entry.Add($"Not identified as {rule.What}.", "bad");
            }
            entry.Add($"Copy kept for analysis as {kept.Ref} until {kept.KeptUntil:dd MMM yyyy}, and deleted after.", "warn");
            entry.End("Refused", "bad");
            Say().Errors[def.Key] = message;
            Say().ErrorLog[def.Key] = entry.Id;
            return;
        }

        entry.Add($"Identified as {rule.What}.", "ok");

        // 3. OCR, then 4. whoever answers for what was read.
        var reading = await ocr.ReadAsync(rule.Kind, type, copy, new OcrSubject(Who.Pan, Who.Dob, Who.Name), AadhaarConsent);
        var (check, kind) = def.Key switch
        {
            "pan" => await ReadPanAsync(reading, entry),
            "poa" => await ReadAddressAsync(reading, entry),
            _ => await ReadInstrumentAsync(reading, entry),
        };
        await FileAsync(def.Key, copy);
        s.Docs[def.Key] = filed(check, kind);
        // Taken, so whatever was refused before it is behind the partner.
        s.Attempts[def.Key] = 0;
    }

    // A slot holds one copy in DMS: a copy filed before is deleted, then the new
    // one filed. One that came over from the step before has no copy here.
    private async Task FileAsync(string slot, UploadFile copy)
    {
        if (State.Docs.GetValueOrDefault(slot) is { Before: false }) await documents.DeleteAsync(AppNo, slot);
        await documents.FileAsync(AppNo, slot, copy);
    }

    private static string Cap(string words) => char.ToUpperInvariant(words[0]) + words[1..];

    // The issuer behind that proof is asked whether the address OCR read is the
    // one they hold. Only a clean answer replaces the address the application carries.
    private async Task<(string, string)> ReadAddressAsync(OcrReading reading, LogEntry entry)
    {
        var card = State.Reads["poa"];
        var type = State.PoaType;
        var named = type.Length > 0 ? type.ToLowerInvariant() : "proof";
        entry.Add("OCR read: " + reading.Address);
        var answer = await verification.ConfirmProofAsync(type, reading, Who.Dob);
        var issuer = answer.Verifier;

        // An Aadhaar carries the number the PAN-Aadhaar link is asked with, so a PAN
        // already on the application can be asked about now.
        if (type == "Aadhaar" && AadhaarNumbers.IsWhole(reading.IdNumber))
        {
            AadhaarNumber = reading.IdNumber;
            if (PanOnApplication) await RelinkPanAsync(entry);
        }

        if (answer.NotAsked is { } why)
        {
            (card.State, card.Kind) = ("With Operations", "is-failed");
            card.From = $"{Cap(issuer)} could not be asked: {why}. The address is left as it stands for Operations to settle.";
            entry.Add($"{Cap(issuer)} not asked: {why}.", "warn");
            entry.End("Filed, address unchanged", "warn");
            return ($"Read, but {issuer} could not be asked: {why}. The copy is filed and Operations settle the address; the application keeps the one it carries until they do.", "warn");
        }

        if (issuer.Length == 0)
        {
            (card.State, card.Kind) = ("With Operations", "is-failed");
            card.From = $"A {named} has no issuer to check with, so the address is left as it stands for Operations to settle.";
            entry.Add($"A {named} has no register behind it to put that address to.", "warn");
            entry.End("Filed, address unchanged", "warn");
            return ($"Read, but nothing outside answers for a {named}. The copy is filed and Operations settle the address; the application keeps the one it carries until they do.", "warn");
        }
        if (!answer.Confirmed)
        {
            (card.State, card.Kind) = ("Not confirmed", "is-failed");
            card.From = $"{issuer} did not confirm the address on this proof, so the application keeps the address it carries. Upload a clearer copy, or a different proof.";
            entry.Add($"{issuer} did not confirm that address.", "bad");
            entry.End("Filed, address unchanged", "warn");
            return ($"Read, but {issuer} did not confirm what it says. The copy is filed and the application keeps the address it carries — upload a clearer copy, or another proof.", "bad");
        }
        var before = card.Lines;
        (card.Lines, card.Was) = (reading.Address, before);
        (card.State, card.Kind) = ($"Verified with {issuer}", "is-done");
        card.From = $"Read off the {named} filed above and confirmed with {issuer}.";
        entry.Add($"{issuer} confirmed that address.", "ok");
        entry.Add("Address on the application replaced. Was: " + before, "ok");
        entry.End("Filed", "ok");
        return ($"Identified, read and confirmed with {issuer}. The address on the application now comes from this proof.", "ok");
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

    // A PAN copy is read for the number on it, and the PAN-Aadhaar link is asked
    // whether an Aadhaar is held against the holder's PAN - once there is an
    // Aadhaar number on the application to ask with. An unlinked PAN does not stop
    // the application: TDS runs at the higher rate until the investor links it.
    private async Task<(string, string)> ReadPanAsync(OcrReading reading, LogEntry entry)
    {
        entry.Add("OCR read: PAN " + Mask(reading.Pan.Length > 0 ? reading.Pan : Who.Pan));
        var link = await LinkAsync(entry);
        entry.End(link switch
        {
            PanAadhaarLink.Linked => "Filed",
            PanAadhaarLink.NotLinked => "Filed, PAN not linked",
            _ => "Filed, link not checked",
        }, link == PanAadhaarLink.Linked ? "ok" : "warn");
        return PanCheck(link);
    }

    private bool PanOnApplication => PanFiled || State.Docs.ContainsKey("pan");

    // The Aadhaar arrived after the PAN: the link is asked now, and the PAN's card
    // and what its copy says both follow the answer.
    private async Task RelinkPanAsync(LogEntry entry)
    {
        var link = await LinkAsync(entry);
        if (State.Docs.GetValueOrDefault("pan") is { Before: false } doc)
        {
            var (check, kind) = PanCheck(link);
            State.Docs["pan"] = doc with { Check = check, CheckKind = kind };
        }
    }

    // Null when the link check could not answer. The PAN or the Aadhaar that
    // prompted it is filed all the same: the link is only ever a note on the PAN.
    private async Task<PanAadhaarLink?> LinkAsync(LogEntry entry)
    {
        var card = State.Reads["pan"];
        PanAadhaarLink link;
        try
        {
            link = await panLink.CheckAsync(Who.Pan, AadhaarNumber);
        }
        catch (ExternalServiceException e)
        {
            card.Lines = HolderPan + " · link with Aadhaar not checked";
            (card.State, card.Kind) = ("Link not checked", "");
            card.From = $"The PAN-Aadhaar link could not be asked just now: {e.Message}";
            entry.Add($"{e.Service} could not answer the PAN-Aadhaar link: {e.Message}", "warn");
            return null;
        }
        switch (link)
        {
            case PanAadhaarLink.Linked:
                card.Lines = HolderPan + " · linked with Aadhaar";
                (card.State, card.Kind) = ("Linked with Aadhaar", "is-done");
                card.From = $"Confirmed with {PanAuthority} against the Aadhaar read on this application.";
                entry.Add($"{Cap(PanAuthority)} holds an Aadhaar against this PAN.", "ok");
                break;
            case PanAadhaarLink.NotLinked:
                card.Lines = HolderPan + " · not linked with Aadhaar";
                (card.State, card.Kind) = ("Not linked", "is-failed");
                card.From = $"{Cap(PanAuthority)} holds no Aadhaar against this PAN. The deposit can still be booked, but TDS runs at the higher rate until the investor links it.";
                entry.Add("No Aadhaar against this PAN.", "warn");
                break;
            default:
                card.Lines = HolderPan + " · link with Aadhaar not checked yet";
                (card.State, card.Kind) = ("Link not checked", "");
                card.From = LinkWaits;
                entry.Add("No Aadhaar number read on this application yet, so the PAN-Aadhaar link is not asked.", "warn");
                break;
        }
        return link;
    }

    private (string, string) PanCheck(PanAadhaarLink? link) => link switch
    {
        PanAadhaarLink.Linked => ($"Identified, read as PAN {HolderPan} and confirmed with {PanAuthority}: an Aadhaar is held against it.", "ok"),
        PanAadhaarLink.NotLinked => ($"Read as PAN {HolderPan}, but {PanAuthority} holds no Aadhaar against it. The copy is filed and the deposit can be booked — TDS runs at the higher rate until the investor links it.", "warn"),
        PanAadhaarLink.NeedsAadhaar => ($"Identified and read as PAN {HolderPan}. {LinkWaits}", "warn"),
        _ => ($"Identified and read as PAN {HolderPan}. The PAN-Aadhaar link could not be asked just now; it is asked again when an Aadhaar is next filed.", "warn"),
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
        bool Missing(SlotDef d) { var v = View(d); return v.Used && v.Doc is null; }

        if (Missing(FormSlot)) left.Add("the application form");
        if (Missing(PanSlot)) left.Add("the PAN copy");
        if (View(PoaSlot).Used && s.PoaType.Length == 0) left.Add("the proof of address type");
        if (Missing(PoaSlot)) left.Add("the proof of address");
        if (Missing(PhotoSlot)) left.Add("the photograph");
        if (s.PayMode.Length == 0) left.Add("the payment mode");
        if (Missing(PaymentSlot)) left.Add("the instrument copy");
        if (mode is null) left.Add("the sourcing mode");
        else
        {
            if (s.SourceCode.Length == 0) left.Add("the " + mode.CodeLabel.ToLowerInvariant());
            if (SubRequired(mode) && s.SubBroker.Length == 0) left.Add("the sub broker code");
        }
        if (s.Category.Length == 0) left.Add("the deposit category");
        if (s.Category == EmployeeCategory)
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
    public string? PoaType { get; set; }
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
