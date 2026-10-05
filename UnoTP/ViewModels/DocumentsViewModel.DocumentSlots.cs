using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

// The documents the step asks for, and how each one's box is drawn.
public partial class DocumentsViewModel
{
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

    // ----- The address the application carries ---------------------------------
    // A proof of address is not filed on its own: OCR reads the address off it and
    // the issuer is asked whether that is the address they hold. Only an answer
    // that comes back clean replaces what the application already carries, which
    // is why the step shows both addresses where the pickers can change them.

    /// <summary>Who stands behind each proof, as the empty box names them before
    /// anything is uploaded.</summary>
    public IReadOnlyDictionary<string, string> Issuers => Ref.ProofsOfAddress.ToDictionary(p => p.Type, p => p.Issuer);

    /// <summary>Who answers for a PAN.</summary>
    public const string PanAuthority = "the Income Tax Department";

    // The same, short enough for a card.
    private const string LinkWaitsShort = "Asked once an Aadhaar is filed as the proof of address, with its number.";

    private const string LinkWaits =
        "Whether an Aadhaar is linked to it is asked once an Aadhaar is read on this application — file one as the proof of address.";

    // The holder's consent to their Aadhaar being read, masked and checked, which
    // IDfy asks for on every Aadhaar call. It is taken as given: the investor consents
    // on the application form the partner holds, so every Aadhaar call carries it.
    private const bool AadhaarConsent = true;

    // The copy with its Aadhaar number masked, as it is filed and as it is kept
    // aside. The masking API is told which application and holder it is for.
    private async Task<UploadFile> MaskedAsync(DocHolder h, UploadFile copy)
    {
        var aadhaar = new AadhaarToMask(AppNo, h.Code, copy, AadhaarConsent);
        Doing("Masking the Aadhaar number\u2026");
        return (await masking.MaskAsync(aadhaar)).Copy;
    }

    // The Aadhaar number OCR read for a holder on this application, for asking the
    // PAN-Aadhaar link with. An Aadhaar number is not to be stored, so it is never
    // saved with the application: it is held in the server's session, and goes with it.
    private string AadhaarKey(DocHolder h) => "aadhaar:" + AppNo + (h.Joint ? ":" + h.Code : "");

    private string AadhaarOf(DocHolder h) => session.GetString(AadhaarKey(h)) ?? "";

    // What is on record for the folio the application was opened on, where the
    // holder's latest KYC is kept: a verified PAN copy, a photograph, a verified
    // proof of address. A document on record is not required again; one that is not
    // there, or is there unverified, is asked for like anyone's.
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

    /// <summary>Form 121, the TDS declaration, filed on FD Configuration when no TDS is to be deducted.</summary>
    public static readonly SlotDef TdsFormSlot = new("tdsform", "Form 121", Form);

    public static readonly SlotDef[] Slots = [FormSlot, PanSlot, PhotoSlot, PoaSlot, MailSlot, PaymentSlot, EmpProofSlot, TdsFormSlot];

    /// <summary>
    /// Set while FD Configuration is drawn: its Form 121 box is asked for by the
    /// switch on the page, before the deposit is saved with it. Elsewhere the box is
    /// asked for by the deposit as saved.
    /// </summary>
    public bool TdsFormWanted { get; set; }

    /// <summary>What every holder files for themselves: the investor on this step, and
    /// each joint holder on Investor Information. The rest belong to the application.</summary>
    public static readonly SlotDef[] HolderSlots = [PanSlot, PhotoSlot, PoaSlot, MailSlot];

    /// <summary>Refusals in a row before a document has to wait to be tried again, from the backend's rules.</summary>
    public int MaxAttempts => Config.MaxAttempts;

    /// <summary>When a document refused that many times in a row may be tried again; null while it has tries left.</summary>
    private DateTime? RetryAt(string key) => State.RetryAt(key, MaxAttempts, limiter.RefusedWait);

    /// <summary>The refusals in a row that still count: once the wait is over, the count starts again.</summary>
    private int AttemptsNow(string key) => State.AttemptsNow(key, MaxAttempts, limiter.RefusedWait, DateTime.Now);

    /// <summary>How long is left of the wait, as the page says it: "try again in about 12 minutes".</summary>
    public static string TryAgainIn(DateTime? retryAt)
    {
        var minutes = retryAt is null ? 0 : (int)Math.Ceiling((retryAt.Value - DateTime.Now).TotalMinutes);
        if (minutes <= 1) return "try again in a minute";
        return $"try again in about {minutes} minutes";
    }

    /// <summary>What a slot shows: whether it is asked for at all, and what is in it.</summary>
    /// <param name="Key">The slot's key for its holder, which its markup and posts carry.</param>
    /// <param name="Optional">Asked for, but not needed to proceed.</param>
    public sealed record SlotView(
        SlotDef Def, string Key, bool Used, string? NotApplicable, string? Locked, StoredDoc? Doc,
        string? Must, string? With, int Attempts, string? Error, string? ErrorLog, bool Optional = false,
        ReadCard? Read = null, string? LockedHint = null, int MaxAttempts = int.MaxValue, string? Final = null,
        DateTime? RetryAt = null)
    {
        /// <summary>Wanted before the step can go on: asked for, not optional, and not in yet.</summary>
        public bool Missing => Used && !Optional && Doc is null;

        /// <summary>Refused the most times in a row the rules allow: no more tries until the wait is over.</summary>
        public bool Spent => Attempts >= MaxAttempts;
        public string Title => Def.Key switch { "payment" => "the cheque", "mail" => "the proof", _ => Def.Label };
        public bool Unconfirmed => Doc?.CheckKind is "warn" or "bad";

        public string? Tries => Attempts == 0 ? null
            : Spent ? $"{MaxAttempts} refused one after another — {TryAgainIn(RetryAt)}."
            : $"{Attempts} of {MaxAttempts} refused in a row this session — a copy the checks take clears it.";
    }

    private const string CkycWhy =
        "The investor\u2019s CKYC record stands as their KYC, so this is not uploaded here.";

    // The three checks are named on the empty box too: a partner who knows UIDAI
    // will be asked reaches for the copy UIDAI would recognise.
    private const string Run = "Once uploaded: identified, read by OCR, then ";

    /// <summary>A document card as one value of a choice would leave it. Now: this value is the one chosen.</summary>
    public sealed record SlotAlternative(string Value, bool Now, SlotView Slot);

    /// <summary>
    /// One document card per value a choice can take, for the choices that reshape a card:
    /// the application type (the form's card), the communication address (the proof's
    /// card) and the payment mode (the instrument's card). The page draws every card and
    /// hides all but the chosen one, so it can swap them in the moment the choice changes.
    /// </summary>
    /// <param name="field">The choice's form field: appType, mailing or payMode.</param>
    public IReadOnlyList<SlotAlternative> AlternativesFor(string field)
    {
        var alternatives = new List<SlotAlternative>();

        if (field == "appType")
        {
            foreach (var type in ApplicationTypes)
            {
                alternatives.Add(new SlotAlternative(type.Code, type.Code == State.AppType, ViewWithAppType(type.Code)));
            }
        }
        else if (field == "mailing")
        {
            var different = MailDifferentOf(Investor);
            alternatives.Add(new SlotAlternative("same", !different, ViewWithMailDifferent(false)));
            alternatives.Add(new SlotAlternative("different", different, ViewWithMailDifferent(true)));
        }
        else if (field == "payMode")
        {
            alternatives.Add(new SlotAlternative("", State.PayMode == "", ViewWithPayMode("")));
            foreach (var mode in PaymentModes)
            {
                alternatives.Add(new SlotAlternative(mode.Name, mode.Name == State.PayMode, ViewWithPayMode(mode.Name)));
            }
        }

        return alternatives;
    }

    // The three helpers below draw a card as it would stand with the choice set to a
    // value: the choice is set on the state for the moment the card is worked out, then put back.
    private SlotView ViewWithAppType(string type)
    {
        var was = State.AppType;
        State.AppType = type;
        try { return View(FormSlot); }
        finally { State.AppType = was; }
    }

    private SlotView ViewWithMailDifferent(bool different)
    {
        var was = State.MailDifferent;
        State.MailDifferent = different;
        try { return View(MailSlot); }
        finally { State.MailDifferent = was; }
    }

    private SlotView ViewWithPayMode(string mode)
    {
        var was = State.PayMode;
        State.PayMode = mode;
        try { return View(PaymentSlot); }
        finally { State.PayMode = was; }
    }

    /// <summary>How a document slot stands for the investor: its copy, its checks and what can be done.</summary>
    public SlotView View(SlotDef def) => View(def, Investor);

    /// <summary>How a document slot stands for a holder: its copy, its checks and what can be done.</summary>
    public SlotView View(SlotDef def, DocHolder h)
    {
        var s = State;
        var key = h.Key(def.Key);
        var proofType = TypeOf(def, h);
        var held = FolioDocsOf(h);
        string heldWhy = "Already on record and verified, so it is not filed again.";
        string heldPhotoWhy = "Already on record, so it is not filed again.";
        var (used, na) = def.Key switch
        {
            "form" => (s.AppType == Physical, "A digital application is accepted through the investor’s own link, so there is no signed form to file."),
            "pan" => held?.Pan == true ? (false, heldWhy) : (true, null),
            // CKYC is fetched for the investor only: a joint holder files their own.
            "photo" => held?.Photo == true ? (false, heldPhotoWhy) : s.Ckyc && !h.Joint ? (false, CkycWhy) : (true, null),
            // A verified proof on record need not be filed again - but a newer one is
            // taken, should the address have changed (optional, below).
            "poa" => s.Ckyc && !h.Joint ? (false, CkycWhy) : (true, null),
            "mail" => !MailCanDiffer(h) ? (false, MailWhy(h))
                : MailTyped(h) ? (false, MailTypedWhy)
                : MailDifferentOf(h) ? (true, null) : (false, "Post goes to the permanent address, so there is no other address to prove."),
            // A renewal is paid by the maturing deposit. Otherwise, until a mode is
            // chosen the box waits on the choice (locked below).
            "payment" => IsRenewal ? (false, $"The maturing deposit {Renewal!.DepositNumber} pays for the new one, so there is no instrument to copy.")
                : (s.PayMode.Length == 0 || DocumentOf(s.PayMode) is not null, $"{s.PayMode} is settled electronically, so there is no instrument to copy."),
            "empproof" => (IsEmployee(s.Category), "Only a deposit booked against a staff record carries an employee proof."),
            "tdsform" => (TdsFormWanted || App.Deposit?.NoTds == true, "Asked only when no TDS is to be deducted: the switch on FD Configuration."),
            _ => (true, (string?)null),
        };

        // A proof of address is read and checked against the holder the PAN copy
        // establishes, so it waits for that copy wherever one is needed. And a
        // proof is checked as whatever type was chosen above it, so there is
        // nothing to check it as until one is.
        var waitsOnPan = used && def.Key is "poa" or "mail" && PanWanted(h);
        string? locked = !used ? null : waitsOnPan ? "Upload the PAN copy first" : def.Key switch
        {
            "poa" when !AutoProofType && proofType.Length == 0 => "Choose the proof of address first",
            "mail" when !AutoProofType && proofType.Length == 0 => "Choose the communication address proof first",
            "empproof" when s.EmpProofType.Length == 0 => "Choose the employee proof first",
            "payment" when s.PayMode.Length == 0 => "Choose the payment mode first",
            _ => null,
        };

        string? with = def.Key switch
        {
            "pan" when NsdlApplies(h) && !switches.IsOn(OutsideSwitches.PanCheck) => $"Once uploaded: identified and read by OCR. The PAN check is {OutsideSwitches.Off}, so NSDL is not asked.",
            "pan" when NsdlApplies(h) => Run + "the PAN, date of birth and name are checked with NSDL.",
            "pan" => Run + $"checked with {PanAuthority}.",
            "payment" when !switches.IsOn(OutsideSwitches.Cheque) => $"Once uploaded: filed as handed over. The cheque check is {OutsideSwitches.Off}, so the account is entered on Bank Details & Payment.",
            "payment" => Run + "the account is confirmed with the bank it is drawn on.",
            "poa" or "mail" when Issuers.TryGetValue(proofType, out var issuer) => issuer.Length > 0
                ? Run + $"the address is confirmed with {issuer}."
                : $"Once uploaded: identified and read by OCR. A {proofType.ToLowerInvariant()} has no issuer to confirm the address with, so Operations settle it.",
            // The type is what the copy is identified as, so until one is filed it is
            // not known: the box names the proofs it takes.
            "poa" or "mail" => $"{OneOf(ProofsFor(def.Key))}: identified on upload, then confirmed with its issuer.",
            _ => null,
        };

        // Only a document already verified on record is not required. A PAN copy is
        // then not asked for at all (above); a proof of address is still taken, but
        // not needed. Anything not verified on record is required, folio or no folio.
        var optional = used && def.Key == "poa" && held?.Poa == true;

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
            "pan" when doc is not null => PanReadOf(h, doc),
            "poa" when doc is null && folioAddress => FolioAddress(h, PoaOnRecordNext(used, optional)),
            "mail" when !used && folioAddress && !MailCanDiffer(h) => FolioAddress(h, "post goes there."),
            _ => null,
        };

        var flash = Shown;
        return new SlotView(def, key, used, used ? null : na, locked, doc,
            optional ? "Not mandatory: a verified proof is on record, and its address stands unless a newer proof is filed." : null,
            with, AttemptsNow(key),
            flash?.Errors.GetValueOrDefault(key), flash?.ErrorLog.GetValueOrDefault(key), optional, read,
            // Waiting on the PAN, the box still names the proofs it will take.
            waitsOnPan ? $"Accepted: {OneOf(ProofsFor(def.Key))}. Checked against the holder the PAN copy establishes." : null, MaxAttempts,
            FinalWhy(def, h, doc), RetryAt(key));
    }

    // What the address on record says of its proof: not asked for, taken but not
    // needed, or needed because no proof on record is verified.
    private static string PoaOnRecordNext(bool used, bool optional)
    {
        if (!used) return "not checked here.";
        if (optional) return "file a newer proof only if the address has changed.";
        return "a proof of it is needed: none on record is verified.";
    }

    // A PAN copy NSDL has verified - the PAN, the date of birth and the name read off
    // it all held together - is final on the application: it is not replaced, as a
    // new copy could only undo what NSDL confirmed. Null for any other copy.
    private string? FinalWhy(SlotDef def, DocHolder h, StoredDoc? doc) =>
        def.Key == PanSlot.Key && doc is not null && NsdlApplies(h) && NsdlOf(h) == "verified"
            ? "Verified with NSDL — the PAN, date of birth and name all match — so this PAN copy is final and cannot be replaced."
            : null;

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
            ("Communication address", MailTyped(h) ? TypedMailOf(h) is { } typed
                    ? new ReadCard("Typed", Lines(typed), "Typed on Investor Information; the district and state are the PIN code's.")
                    : NotRead("To be typed", "Different from permanent: typed on Investor Information.",
                        "No proof of it is uploaded in this release; it is entered with the holder's details.")
                : MailDifferentOf(h) ? MailReadOf(h)
                : !MailCanDiffer(h) && h.Who.Folio.Length > 0 && h.Who.Address.Length > 0 ? FolioAddress(h, "post goes there.")
                : !MailCanDiffer(h) && h.Who.Folio.Length > 0 ? NotRead("Same as permanent", "Post goes to the address on the folio.",
                    "The system holds the address, and it is the mailing address too.")
                : !MailCanDiffer(h) ? NotRead("From CKYC", "The communication address comes with the CKYC record.",
                    "It is fetched once the investor consents, with the permanent address and the photograph.")
                : NotRead("Same as permanent", "Post goes to the permanent address, so there is no other address to read.",
                    CommProofUpload ? "Choose Different from Permanent to file a proof of another address."
                        : "Choose Different from Permanent to type another address on Investor Information.")),
        ];
        if (h.Joint || NsdlApplies(h)) cards.Add(NsdlCard(h));
        // The link is asked with the number an Aadhaar carries. Its card stands once
        // the PAN is on the application - waiting on an Aadhaar until one is filed -
        // and, for a holder on a folio, once an Aadhaar is filed, to say it is not asked.
        if (AadhaarFiled(h) || LinkApplies(h) && PanOnApplication(h))
            cards.Add(new ReadItem("PAN–Aadhaar link", LinkApplies(h) ? State.Reads[h.Key("pan")]
                : NotRead("Not applicable", $"{MaskPan(h.Who.Pan)} · on the folio",
                    "The PAN–Aadhaar link is asked only for a holder with no folio yet."), h.Key("link"),
                Error: Shown?.Errors.GetValueOrDefault(h.Key("link"))));
        if (View(PoaSlot, h).Used)
        {
            cards.Add(new ReadItem("PAN–POA name & DOB", DetailsOf(h), h.Key("details"), Error: Shown?.Errors.GetValueOrDefault(h.Key("details"))));
            cards.Add(new ReadItem("PAN–POA face match", FaceOf(h), h.Key("face")));
        }
        return cards;
    }

    // Whether an Aadhaar is filed as a holder's proof of address, or of the address post goes to.
    private bool AadhaarFiled(DocHolder h) =>
        PoaTypeOf(h) == "Aadhaar" && State.Docs.ContainsKey(h.Key("poa"))
        || MailDifferentOf(h) && MailTypeOf(h) == "Aadhaar" && State.Docs.ContainsKey(h.Key("mail"));
}
