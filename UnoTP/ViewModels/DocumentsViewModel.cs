using UnoTP.Infrastructure;
using UnoTP.Models;
using UnoTP.Services;

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
/// OCR - which for an Aadhaar has to read the name and date of birth on the PAN - and
/// whoever answers for what was read - the issuer or the bank - or for a PAN, the
/// PAN-Aadhaar link. Only then is the copy filed with DMS (see <see cref="IDocumentApi"/>).
///
/// The address carries nothing: the application is the one the session is on,
/// opened by Investor Identification, and only ever the owner's own (see
/// <see cref="Controllers.DocumentsController"/>), which reads it, hands it
/// here, and saves what this leaves in it.
/// </summary>
public partial class DocumentsViewModel(
    UnoTP.Models.Application app,
    ISession session,
    IDocumentApi documents,
    IDocumentIdentifier identifier,
    IOcrService ocr,
    IPanVerificationService pan,
    ICkycService ckyc,
    UnoTP.Infrastructure.FeatureSet features,
    IVerificationService verification,
    IPanAadhaarLinkService panLink,
    IFaceMatchService faces,
    IMaskingService masking,
    INameScreeningService screening,
    INameMatchService names,
    OutsideSwitches switches,
    UnoTP.Infrastructure.Lookups lookups,
    UnoTP.Infrastructure.CurrentPartner currentPartner,
    ISourcingApi sourcing,
    DocumentLimiter limiter,
    UploadProgress progress)
{
    /// <summary>The application the session is on, read afresh for every request.</summary>
    public UnoTP.Models.Application App { get; } = app;

    // The investor as the application was opened on them, under the name NSDL has
    // since verified where they came with no folio. Read off the saved state, not
    // State, which is built from this.
    private Holder Who => App.Upload is { Name.Length: > 0 } u ? App.Holder with { Name = u.Name } : App.Holder;

    public string HolderName => Who.Name.Length > 0 ? Who.Name : "Name read off the PAN copy";

    // A PAN established at the step before has no folio yet: one opens with the
    // application, and the head of the page says so rather than leaving a blank.
    public bool HasFolio => Who.Folio.Length > 0;

    /// <summary>
    /// Whether the investor's KYC can be fetched from CKYC now: only for an investor
    /// with no folio, on the PAN and date of birth they were identified with - so
    /// before any PAN copy is uploaded too. Once a copy is uploaded it has to hold:
    /// while NSDL has not verified it (not asked yet, the name not agreeing, no such
    /// PAN), the record is not fetched on it. A PAN established before the
    /// application was opened needs no copy.
    /// </summary>
    public bool CkycPanVerified => !HasFolio && (PanFiled || View(PanSlot).Doc is null || NsdlOf(Investor) == "verified");

    public const string CkycWaitsOnPan = "Available once NSDL verifies the uploaded PAN copy.";

    /// <summary>The deposit this application renews, when it was opened from Renew FD; null for a new deposit.</summary>
    public RenewalOf? Renewal => App.Renewal;

    /// <summary>A renewal: no payment is made - the maturing deposit pays for the new one - and the amount is its maturity amount.</summary>
    public bool IsRenewal => App.Renewal is not null;

    public string Folio => Who.Folio;

    public string HolderFolio => HasFolio ? Who.Folio : "Opens with this application";

    public string HolderPan => MaskPan(Who.Pan);

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

        /// <summary>The key a document slot is stored under: the slot itself for the investor, prefixed h{n}- for a joint holder.</summary>
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
    /// <summary>Whether a proof of address's type is detected off the copy: the feature on, and identification not switched off.</summary>
    public bool AutoProofType => features.Flags.DocIdentification && switches.IsOn(OutsideSwitches.Identify);

    /// <summary>
    /// Whether a communication address other than the permanent one is proved with an
    /// upload (the CommProofUpload feature, off for this release). Off, the box is not
    /// shown, and the address is typed on Investor Information.
    /// </summary>
    public bool CommProofUpload => features.Flags.CommProofUpload;

    /// <summary>Whether a holder's communication address is typed on Investor Information: it differs, and no proof of it is uploaded.</summary>
    public bool MailTyped(DocHolder h) => MailDifferentOf(h) && !CommProofUpload;

    /// <summary>The communication address typed for a holder, as last saved; null until one is.</summary>
    public TypedAddress? TypedMailOf(DocHolder h) =>
        App.Details?.Holders.FirstOrDefault(d => d.Holder == h.Code)?.Communication is { Line1.Length: > 0 } typed ? typed : null;

    public const string MailTypedWhy = "The communication address is typed on Investor Information, so no proof of it is uploaded.";

    /// <summary>A typed address on lines of its own, as a read card and the review show it.</summary>
    public static string Lines(TypedAddress a) => string.Join(", ",
        new[] { a.Line1, a.Line2, a.Line3, a.City, a.District, a.State }.Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        + (a.PinCode.Length > 0 ? " - " + a.PinCode : "");

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
        ? "The address is on the folio, so post goes there."
        : "The communication address comes with the CKYC record, once the investor consents. It is not uploaded here.";

    /// <summary>
    /// Whether NSDL is asked about a holder's PAN when their PAN copy is filed: any
    /// holder with no folio - the investor or a joint holder - who comes to this
    /// step on their PAN and date of birth alone. OCR reads the name off the copy,
    /// and NSDL is asked for all three. An application whose PAN was established
    /// before it was opened is past it.
    /// </summary>
    public static bool NsdlApplies(DocHolder h) => h.Who.Folio.Length == 0 && !h.Who.PanFiled;

    /// <summary>Where NSDL stands on a holder: empty until asked, "verified", "name" or "failed".</summary>
    public string NsdlOf(DocHolder h) => h.Joint ? State.Joint[h.Code].Nsdl : State.Nsdl;

    private string NsdlNameOf(DocHolder h) => h.Joint ? State.Joint[h.Code].NsdlName : State.NsdlName;

    // The holder as the state now has them, after NSDL may have named them.
    private DocHolder Again(DocHolder h) => h.Joint ? JointHolder(h.Code)! : Investor;

    // What NSDL failing means for the holder: a joint holder is removed, and an
    // investor the application was opened on is searched for again.
    private static string NsdlFailedNext(DocHolder h) => h.Joint
        ? "Remove this holder and search again."
        : "Start again from Investor Identification with the right PAN and date of birth.";

    /// <summary>
    /// Whether the PAN-Aadhaar link is asked for a holder: only one with no folio
    /// yet. A holder on a folio is past it.
    /// </summary>
    public static bool LinkApplies(DocHolder h) => h.Who.Folio.Length == 0;

    /// <summary>
    /// Whether "What the PAN was checked with" is shown for a holder: only one with no
    /// folio yet, and only once their PAN copy is filed, since every check starts
    /// from it. A holder on a folio was checked when it opened - NSDL and the
    /// PAN-Aadhaar link are not asked again - so the section is left off.
    /// </summary>
    public bool PanChecksShown(DocHolder h) => h.Who.Folio.Length == 0 && State.Docs.ContainsKey(h.Key("pan"));

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
    /// <summary>
    /// Why a PAN copy is not the holder's, or null when it is. The PAN and date of
    /// birth were settled before any copy was asked for and cannot be changed, so
    /// the copy has to read as both: another PAN or date of birth, or one OCR could
    /// not read, and it is refused.
    /// </summary>
    public static string? PanCopyMismatch(OcrReading reading, string pan, string dob)
    {
        var read = reading.Pan.Replace(" ", "").ToUpperInvariant();
        if (read.Length == 0) return "OCR could not read the PAN number on it";
        if (read != pan.ToUpperInvariant()) return $"The PAN on it reads as {MaskPan(read)}, not {MaskPan(pan)}";
        if (dob.Length == 0) return null;
        if (reading.Dob.Length == 0) return "OCR could not read the date of birth on it";
        if (reading.Dob != dob) return $"The date of birth on it does not match the one entered ({MaskDate(dob)})";
        return null;
    }

    /// <summary>A PAN with its middle hidden, as the pages show it.</summary>
    public static string MaskPan(string pan) =>
        pan.Length == 10 ? pan[..5] + "••••" + pan[9..] : pan;

    public static string MaskDate(string dob) =>
        dob.Length == 10 ? "••/••/" + dob[6..] : dob;

    // ===== The state of this application =======================================

    /// <summary>What the step holds against this application, opened on first sight.</summary>
    public UploadState State
    {
        get
        {
            var s = App.Upload ??= new UploadState();
            Ready(s);
            Settle(s);
            return s;
        }
    }

    // A holder's cards are opened the first time the step sees them: the reads as
    // the step before left them, the PAN copy that came over, and the attempts the
    // backend has on record from before this page - on the record without counting
    // against the three. A state the backend opened with holders already on it - a
    // renewal's, with the deposit's joint holders - has theirs opened here too.
    private void Ready(UploadState s)
    {
        if (!s.Reads.ContainsKey(Investor.Key("poa")))
        {
            OpenHolder(s, Investor);
            s.Reads.TryAdd("payment", new ReadCard("Not yet read", "Read off the instrument once a copy is filed.",
                "The account the deposit is paid from. Bank Details & Payment opens with whatever is confirmed here."));
            if (s.Log.Count == 0) s.Log.AddRange(App.Prior);
        }
        foreach (var (code, joint) in s.Joint)
        {
            var h = new DocHolder(code, joint.Holder);
            if (!s.Reads.ContainsKey(h.Key("poa"))) OpenHolder(s, h);
        }
    }

    // A holder's cards open with what identifying them established: the PAN copy
    // it came with, and the address the folio holds.
    private static void OpenHolder(UploadState s, DocHolder h)
    {
        var pan = MaskPan(h.Who.Pan);
        s.Reads[h.Key("pan")] = h.Who.PanFiled
            ? new ReadCard("Waiting on an Aadhaar", pan + " · link with Aadhaar not asked yet",
                $"PAN confirmed with NSDL. {LinkWaitsShort}")
            : new ReadCard("Not yet read", "Read off the PAN copy once one is filed here.", LinkWaitsShort);
        // The folio's address is shown as it stands, until a proof filed here is confirmed.
        s.Reads[h.Key("poa")] = h.Who.Address.Length > 0
            ? FolioAddress(h, "not checked here.")
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
            TakeOffApplication(key);
            State.Attempts.Remove(key);
            State.RefusedAt.Remove(key);
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

    /// <summary>Takes a document off the application; a copy that was already filed is deleted from DMS once the save goes through.</summary>
    private void TakeOffApplication(string key)
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
}
