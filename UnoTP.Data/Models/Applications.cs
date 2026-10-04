namespace UnoTP.Models;

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

    /// <summary>
    /// PUT applications/{appNo}/pages/{page}: a page's working state, kept with the
    /// application as it stands - it moves no version and meets no conflict. False
    /// when the application is not the partner's.
    /// </summary>
    Task<bool> SavePageAsync(string appNo, string page, string state, CancellationToken ct = default);

    /// <summary>PUT applications/{appNo}/details: the holders' and the nominee's details, from Investor Information. New version, or null on a conflict.</summary>
    Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default);

    /// <summary>PUT applications/{appNo}/payment: the accounts and the cheque, from Bank Details &amp; Payment. New version, or null on a conflict.</summary>
    Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default);

    /// <summary>PUT applications/{appNo}/deposit: the deposit as configured. New version, or null on a conflict.</summary>
    Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default);

    /// <summary>
    /// POST applications/{appNo}/submit: the application is submitted and the
    /// investor sent the link to pay and consent. Null on a conflict; the
    /// application, as submitted, otherwise. <paramref name="link"/> is the payment
    /// link the backend sends the investor; null leaves the backend to make its own.
    /// </summary>
    Task<Application?> SubmitAsync(string appNo, int version, PaymentLink? link = null, CancellationToken ct = default);

    /// <summary>
    /// POST applications/{appNo}/resend-link: sends a submitted application's payment
    /// link again, while a resend is left. The submission as it now stands, or null
    /// when there is none to resend.
    /// </summary>
    Task<Submission?> ResendLinkAsync(string appNo, CancellationToken ct = default);

    /// <summary>The partner's applications that are theirs to finish - opened, not yet submitted - newest first.</summary>
    Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default);

    /// <summary>The partner's applications, for looking one up.</summary>
    Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default);
}

/// <summary>Who an application is for, as Investor Identification established it.</summary>
/// <param name="PanFiled">Set when a PAN copy is on the application already.</param>
/// <param name="Address">The address on record, or empty for an investor with none yet.</param>
/// <param name="OnRecord">What the folio already holds, for an application opened on one.</param>
/// <param name="Gender">As the folio holds it, or empty for an investor with no folio.</param>
/// <param name="Source">Where the folio's record says the investor's KYC came from; empty for an investor with no folio, or a folio that does not say.</param>
public sealed record Holder(
    string Pan, string Dob, string Name, string Folio, bool PanFiled,
    string Address = "", DocsOnRecord? OnRecord = null, string Gender = "", string Source = "");

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

    /// <summary>The holders' and the nominee's details, null until Investor Information is first saved.</summary>
    public ApplicationDetails? Details { get; set; }

    /// <summary>The accounts and the cheque, null until Bank Details &amp; Payment is first saved.</summary>
    public PaymentDetails? Payment { get; set; }

    /// <summary>The deposit, null until FD Configuration is first saved.</summary>
    public DepositDetails? Deposit { get; set; }

    /// <summary>Set once the application is submitted.</summary>
    public Submission? Submitted { get; set; }

    /// <summary>
    /// The deposit this application renews; null for a new deposit. Set by the
    /// backend when the application is opened from Renew FD, with the deposit's
    /// joint holders, repayment account and maturity amount already on it.
    /// </summary>
    public RenewalOf? Renewal { get; init; }

    /// <summary>
    /// Each wizard page's working state as the page last left it, by page: what is
    /// typed but not yet a part of its own, a joint holder still being searched
    /// for. Kept with the application, so a new application starts clean and one
    /// picked up again opens where it was left. Opaque to the backend.
    /// </summary>
    public Dictionary<string, string> Pages { get; set; } = [];

    /// <summary>
    /// Whose rate card the deposit is quoted from: the deposit's category, and whether
    /// this is a purchase or a renewal. A renewal starts on its maturity date,
    /// and is quoted off today's card like any other. The category is passed in: a page has it settled before the upload step is
    /// saved (Upload Documents sets it from the holder for a broker partner).
    /// </summary>
    public RatesRequest RateCardRequest(string category)
    {
        var applicationType = RateCard.Purchase;
        if (Renewal is not null) applicationType = RateCard.Renew;

        return new RatesRequest(category, applicationType, Renewal?.MaturesOn);
    }
}

/// <summary>Investor Information, as saved: each holder's details, and the nominee's.</summary>
public sealed class ApplicationDetails
{
    /// <summary>By holder type: 01 the investor, 02 and 03 the joint holders.</summary>
    public List<HolderDetails> Holders { get; set; } = [];

    /// <summary>Null when no nominee is named.</summary>
    public NomineeDetails? Nominee { get; set; }
}

/// <param name="Pep">"yes", "no", or "" unanswered; the same for <paramref name="PepRelated"/>. Asked only of a holder with no folio.</param>
/// <param name="FatcaTaxResident">A tax resident of another country: such a holder invests offline.</param>
/// <param name="Communication">Where post goes, typed by hand, when it is not the permanent address and no proof of it is uploaded; null otherwise.</param>
public sealed record HolderDetails(
    string Holder,
    string Gender = "",
    string NameType = "",
    string ParentName = "",
    string AnnualIncome = "",
    string Occupation = "",
    string SubOccupation = "",
    string MaritalStatus = "",
    string Mobile = "",
    string Email = "",
    bool FatcaTaxResident = false,
    bool FatcaPermanentResident = false,
    string Pep = "",
    string PepRelated = "",
    TypedAddress? Communication = null);

/// <summary>An address typed by hand. The district and state are the backend's for the PIN code, not typed.</summary>
public sealed record TypedAddress(
    string Line1 = "",
    string Line2 = "",
    string Line3 = "",
    string City = "",
    string PinCode = "",
    string District = "",
    string State = "");

/// <param name="Dob">dd-MM-yyyy. A guardian is named for a nominee under the minimum age.</param>
public sealed record NomineeDetails(
    string Name = "",
    string Dob = "",
    string Relation = "",
    string GuardianName = "",
    string GuardianLine1 = "",
    string GuardianLine2 = "",
    string GuardianLine3 = "",
    string GuardianPinCode = "",
    string GuardianCity = "");

/// <summary>Bank Details &amp; Payment, as saved.</summary>
/// <param name="Payment">The account the deposit is paid from.</param>
/// <param name="Repayment">The account interest and the maturity amount are paid into; the payment account when <paramref name="RepaymentSameAsPayment"/>.</param>
/// <param name="Cheque">For a deposit paid by cheque; null otherwise.</param>
public sealed record PaymentDetails(BankAccount? Payment, BankAccount? Repayment, bool RepaymentSameAsPayment, ChequeDetails? Cheque);

/// <param name="AccountNumber">The whole number; masked wherever it is shown.</param>
public sealed record BankAccount(string Ifsc, string AccountNumber);

/// <param name="Date">dd-MM-yyyy.</param>
/// <param name="CmsLocation">The Axis CMS location it is presented at.</param>
public sealed record ChequeDetails(string Number, string Date, string CmsLocation);

/// <summary>The deposit an application renews: what runs on into the new one.</summary>
/// <param name="Amount">The deposit's maturity amount: what is renewed when principal and interest are.</param>
/// <param name="Principal">The deposit's own amount: what is renewed when the principal only is; 0 when not known.</param>
public sealed record RenewalOf(string DepositNumber, long Amount, DateOnly MaturesOn, decimal Rate, int TenureMonths, string Payout, long Principal = 0)
{
    /// <summary>Whether the principal is known, so the principal alone can be renewed.</summary>
    public bool PrincipalKnown => Principal > 0;

    /// <summary>The new deposit's amount, for what of this deposit is renewed (a <see cref="RenewalChoice"/> code).</summary>
    public long AmountFor(string renewalFor)
    {
        if (renewalFor == RenewalChoice.Principal && PrincipalKnown) return Principal;
        return Amount;
    }
}

/// <summary>
/// What a deposit is renewed for, as the renewInstructions list codes it: on a
/// renewal, what of the old deposit runs on into the new one; under auto renewal,
/// what the new deposit will renew for. The FD system keeps them as P and F.
/// </summary>
public static class RenewalChoice
{
    public const string Principal = "principal";

    public const string PrincipalInterest = "principal-interest";
}

/// <summary>FD Configuration, as saved. Codes are the reference lists'.</summary>
/// <param name="NoTds">Form 121, the TDS declaration, is submitted, so no TDS is deducted.</param>
/// <param name="SourceOfFunds">A sourcesOfFunds code, where the deposit asks for one (AppConfig.SourceOfFundsFrom); empty otherwise.</param>
/// <param name="SourceOfFundsRemark">What the source is, typed, when the source is the one that takes a remark (AppConfig.SourceOfFundsOther).</param>
/// <param name="SourceOfFundsReason">Why the source of funds was asked: "Occupation" or "Annual Income"; empty when it was not.</param>
/// <param name="RenewalFor">On a renewal, what of the old deposit is renewed (a <see cref="RenewalChoice"/> code); empty otherwise.</param>
public sealed record DepositDetails(
    long Amount,
    int TenureMonths,
    string Payout,
    bool AutoRenewal,
    string RenewInstruction,
    bool NoTds,
    string DeliveryType,
    string SourceOfFunds = "",
    string SourceOfFundsRemark = "",
    string SourceOfFundsReason = "",
    string RenewalFor = "");

/// <param name="Status">Where the application stands once submitted: "payment-pending", then the backend's own.</param>
/// <param name="LinkSentTo">The mobile number the link went to by SMS, masked.</param>
/// <param name="LinkEmailedTo">The e-mail address it went to as well, masked; empty when there is none.</param>
/// <param name="LinkValidUntil">When the payment link stops working (linkValidityHours.payment from when it was last sent).</param>
/// <param name="ResendsLeft">Kept for the row; the rule is <paramref name="RegenerateUntil"/>.</param>
/// <param name="RegenerateUntil">A new link can be sent until then: cancellationDays after the application was created, when an unpaid application cancels itself.</param>
public sealed record Submission(DateTime At, string Status, string LinkSentTo, DateTime LinkValidUntil, int ResendsLeft, string LinkEmailedTo = "", string ShortUrl = "",
    DateTime? RegenerateUntil = null)
{
    /// <summary>Whether a new link can still be sent.</summary>
    public bool CanRegenerate(DateTime now) => RegenerateUntil is { } until && now <= until;
}

/// <summary>
/// The payment link an application is submitted with: the page the investor pays
/// on, and its short form when the shortener answered. The backend sends whichever
/// it has - the short one when there is one - by SMS and e-mail.
/// </summary>
public sealed record PaymentLink(string Url, string? ShortUrl);

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

    /// <summary>The investor's gender as read off an Aadhaar on this step, for a
    /// holder the folio gives none for. It sets the deposit category where the
    /// partner does not choose it.</summary>
    public string Gender { get; set; } = "";

    /// <summary>Where NSDL stands on an investor with no folio: empty until their PAN
    /// copy is filed, then "verified", "name" or "failed".</summary>
    public string Nsdl { get; set; } = "";

    /// <summary>The name last put to NSDL: read off the PAN copy, or typed from it.</summary>
    public string NsdlName { get; set; } = "";

    /// <summary>The investor's name as NSDL verified it, for one who came with no folio.</summary>
    public string Name { get; set; } = "";
    public string EmpCode { get; set; } = "";
    public string EmpCompany { get; set; } = "";
    public string EmpHolder { get; set; } = "";
    public string EmpRelation { get; set; } = "";
    public string EmpProofType { get; set; } = "";
    public string FormNo { get; set; } = "0000";

    /// <summary>A paper form's number, kept through a switch to digital and back.</summary>
    public string TypedFormNo { get; set; } = "";

    /// <summary>Set once CERSAI's record stands as the investor's KYC; it cannot be taken back.</summary>
    public bool Ckyc { get; set; }

    /// <summary>The CKYC reference number of the record CERSAI's search found: kept as the investor's CKYC number (f_Kyc_Number).</summary>
    public string CkycReference { get; set; } = "";

    /// <summary>Set when the investor's post goes to an address other than the
    /// permanent one, which is then proved with a copy of its own.</summary>
    public bool MailDifferent { get; set; }

    /// <summary>What the investor's mailing address is proved with.</summary>
    public string MailPoaType { get; set; } = "";

    public DateTime? SavedAt { get; set; }

    // ----- Documents
    public Dictionary<string, StoredDoc> Docs { get; init; } = [];

    /// <summary>Refusals in a row per document; a copy the checks take clears it.</summary>
    public Dictionary<string, int> Attempts { get; init; } = [];

    /// <summary>When each document was last refused: the wait before it may be tried again runs from then.</summary>
    public Dictionary<string, DateTime> RefusedAt { get; init; } = [];

    /// <summary>What each copy was read to say, by the slot it came from.</summary>
    public Dictionary<string, ReadCard> Reads { get; init; } = [];

    /// <summary>
    /// The cheque's fields, kept once its bank confirmed the account on it, so Bank
    /// Details opens filled in. Null until then, and again once a copy is not confirmed.
    /// </summary>
    public ChequeFields? ChequeRead { get; set; }

    /// <summary>Every attempt, newest first.</summary>
    public List<LogEntry> Log { get; init; } = [];

    /// <summary>What name screening said of each holder, by holder type (01, 02, 03), once Investor Information asked.</summary>
    public Dictionary<string, ScreeningOutcome> Screening { get; init; } = [];

    /// <summary>
    /// The joint holders added on Investor Information, by holder type: 02 the second
    /// holder, 03 the third. Their documents are in <see cref="Docs"/>,
    /// <see cref="Attempts"/> and <see cref="Reads"/> under keys that start with it
    /// ("h02-pan"), and filed with DMS under it.
    /// </summary>
    public Dictionary<string, JointHolder> Joint { get; init; } = [];

    public int AttemptsOf(string key) => Attempts.GetValueOrDefault(key);

    /// <summary>
    /// When a document refused <paramref name="max"/> times in a row may be tried
    /// again: <paramref name="wait"/> after its last refusal. Null while it has
    /// tries left. A document refused before refusals were dated has waited long enough.
    /// </summary>
    public DateTime? RetryAt(string key, int max, TimeSpan wait)
    {
        if (AttemptsOf(key) < max) return null;
        return RefusedAt.TryGetValue(key, out var at) ? at + wait : DateTime.MinValue;
    }

    /// <summary>
    /// The refusals in a row that still count against a document. Once the wait
    /// after <paramref name="max"/> of them is over, the count starts again from nothing.
    /// </summary>
    public int AttemptsNow(string key, int max, TimeSpan wait, DateTime now)
    {
        if (RetryAt(key, max, wait) is { } retry && retry <= now) return 0;
        return AttemptsOf(key);
    }
}

/// <summary>A joint holder on the application: who they are, and the proofs of address chosen for them.</summary>
public sealed class JointHolder
{
    /// <summary>Who they are; a holder with no folio takes their name once NSDL verifies it.</summary>
    public required Holder Holder { get; set; }

    /// <summary>
    /// Where NSDL stands on a holder with no folio, asked once their PAN copy is
    /// filed: empty until then, "verified", "name" when it holds the PAN and date of
    /// birth against another name, or "failed" when it holds no such pair.
    /// </summary>
    public string Nsdl { get; set; } = "";

    /// <summary>The name last put to NSDL: OCR's reading, or the one typed after it.</summary>
    public string NsdlName { get; set; } = "";

    public string PoaType { get; set; } = "";

    /// <summary>Set when their post goes to an address other than the permanent one.</summary>
    public bool MailDifferent { get; set; }

    public string MailPoaType { get; set; } = "";
}

/// <summary>
/// A document on the application. The copy itself is filed with the backend
/// (see <see cref="IDocumentApi"/>); this is what the step says about it.
/// <see cref="Before"/> is one that came over from the step before or the folio,
/// which has a name but no copy here to show.
/// </summary>
/// <param name="Checks">What the outside checks made of it as it was filed; null for a document none was run on.</param>
public sealed record StoredDoc(string FileName, long Size, string ContentType, string Check, string CheckKind, bool Before = false, DocChecks? Checks = null);

/// <summary>
/// What the outside checks made of a document as it was filed, for the flags
/// t_FD_BT_KYC_document keeps of it. Whether what was read was then confirmed is
/// the document's <see cref="StoredDoc.CheckKind"/>, which a later check can change.
/// </summary>
/// <param name="IdentifiedAs">What identification took it for; empty when identification was not asked.</param>
/// <param name="OcrAsked">Whether OCR was asked to read it.</param>
/// <param name="Read">Whether OCR read anything off it.</param>
/// <param name="Number">The number read off it: a PAN, a passport, licence or voter ID number, a cheque number. An Aadhaar's last four digits only.</param>
/// <param name="Expiry">When it runs out, dd-MM-yyyy; empty for one that does not, or whose date was not read.</param>
/// <param name="Masked">Whether it is an Aadhaar filed with its number masked.</param>
/// <param name="Face">The comparison of the faces on the PAN copy and the proof of address; null when none was made.</param>
public sealed record DocChecks(
    string IdentifiedAs = "", bool OcrAsked = false, bool Read = false, string Number = "", string Expiry = "",
    bool Masked = false, FaceCheck? Face = null);

/// <param name="Found">Whether a face was found on this copy.</param>
/// <param name="Score">How alike the two faces are, 0 to 100.</param>
public sealed record FaceCheck(bool Found, int Score);

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

    /// <summary>The number read off a proof, as shown - an Aadhaar's last four digits only.</summary>
    public string Number { get; set; } = "";

    /// <summary>When the proof runs out, as dd-MM-yyyy, for a passport or a licence.</summary>
    public string Expiry { get; set; } = "";

    /// <summary>The date of birth read off a PAN copy, as dd-MM-yyyy.</summary>
    public string Dob { get; set; } = "";

    /// <summary>How the card opened.</summary>
    public ReadFace Opened { get; set; } = new("", "", "", "");

    public void Reset()
    {
        // What a read replaced is on the card's own line: that goes back first.
        Lines = Was.Length > 0 ? Was : Opened.Lines;
        (State, From, Kind, Was, Number, Expiry) = (Opened.State, Opened.From, Opened.Kind, "", "", "");
    }
}

public sealed record ReadFace(string State, string Lines, string From, string Kind);

public sealed record LogStage(string Text, string Kind = "");

/// <summary>Name screening's answer for one holder: whether they may invest online, the service's reference, the name it was asked for, and when.</summary>
public sealed record ScreeningOutcome(bool Allowed, string Reference, string Name, DateTime At);

public sealed class LogEntry(string id, string document, int attempt, string at, string file)
{
    public string Id { get; } = id;
    public string Document { get; } = document;
    public int Attempt { get; } = attempt;
    public string At { get; } = at;
    public string File { get; } = file;
    /// <summary>Whose document it was: a joint holder's type (02, 03), "removed" once
    /// that holder was taken off, or empty for the investor's and the application's own.</summary>
    public string Holder { get; set; } = "";

    public string Mark { get; set; } = "Checking";
    public string Kind { get; set; } = "";
    public List<LogStage> Stages { get; init; } = [];

    /// <summary>Adds a stage to this entry of the document's history.</summary>
    public void Add(string text, string kind = "") => Stages.Add(new LogStage(text, kind));

    /// <summary>Closes this history entry with its final mark and kind.</summary>
    public void End(string mark, string kind) => (Mark, Kind) = (mark, kind);
}

/// <summary>A saved application the partner can pick up again, its holder masked.</summary>
/// <param name="Name">As NSDL verified it, else as read off the PAN copy, else as the folio has it; empty until one of them is known.</param>
/// <param name="StepsDone">How many of the four steps - Upload Documents, Investor Information, Bank Details &amp; Payment, FD Configuration - are saved.</param>
/// <param name="NextStep">The first of them not saved yet, or Review Summary once all four are.</param>
/// <param name="TouchedAt">When it was last saved, on the app's clock.</param>
/// <param name="Renews">The deposit a renewal's draft renews; empty for a new deposit's.</param>
public sealed record DraftSummary(string AppNo, string Name, string Pan, string Dob, long Amount,
    int StepsDone = 0, string NextStep = "", DateTime? TouchedAt = null, string Renews = "")
{
    /// <summary>The steps an application goes through before its review, in order.</summary>
    public static readonly string[] Steps = ["Upload Documents", "Investor Information", "Bank Details & Payment", "FD Configuration"];

    /// <summary>How far an application has got, from which of the steps are saved.</summary>
    public static (int Done, string Next) Progress(params bool[] saved) =>
        (saved.Count(s => s), Steps.Where((_, i) => !saved[i]).FirstOrDefault() ?? "Review Summary");
}

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
    string Step,
    string Scheme = "",
    IReadOnlyList<MilestoneRecord>? Milestones = null);

/// <summary>
/// A stage of an application's life, and when it was reached: <c>At</c> is null for one not reached
/// yet. A stage this application does not go through (a pay-in slip for one paid online) stays in the
/// list, <c>Applies</c> false with the reason in <c>Note</c>, so every application reads the same
/// stages in the same order. <c>Failed</c> marks one that came back against it (a penny drop that did
/// not verify, KYC rejected, the application cancelled).
/// </summary>
public sealed record MilestoneRecord(string Step, DateTime? At, string? Note = null, bool Applies = true, bool Failed = false);

/// <summary>
/// An application's stages from entry to the FDR, in the order they happen, for View Application.
/// </summary>
public static class ApplicationStages
{
    /// <summary>What an application has been through, as its dates say.</summary>
    /// <param name="payMode">Online, RTGS or Cheque (Net banking and DD, as older rows have them, read as Online and Cheque).</param>
    /// <param name="pennyDrop">The repayment account's penny drop as Operations' feed wrote it: "", OK or FAILED.</param>
    /// <param name="kyc">Operations' KYC verification: "", OK or REJECTED.</param>
    public static List<MilestoneRecord> Of(
        bool digital, string payMode, DateTime createdOn, DateTime? submittedOn, DateTime? linkSentOn, DateTime? acceptedOn,
        DateTime? slipOn, DateTime? pennyDropOn, string pennyDrop, DateTime? paidOn, DateTime? kycOn, string kyc,
        DateTime? bookedOn, string? fdr, DateTime? cancelledOn)
    {
        var online = payMode is "Online" or "Net banking";
        var cheque = payMode is "Cheque" or "DD";
        // Until the payment mode is chosen, whether a stage applies is not known yet: it waits.
        const string Undecided = "decided by the payment mode, not chosen yet";
        var chosen = payMode.Length > 0;
        List<MilestoneRecord> stages =
        [
            new("Application entry", submittedOn, submittedOn is null ? $"started {createdOn:dd/MM/yyyy} · not submitted yet" : null),
            // The link that takes the investor to accept, pay, or both.
            digital || online
                ? new("Short link sent", linkSentOn, linkSentOn is null ? null : "to the investor's mobile and e-mail")
                : !chosen ? new("Short link sent", null, Undecided)
                : new("Short link sent", null, $"physical, paid by {payMode}: nothing to send", Applies: false),
            // A digital application paid by RTGS or cheque is accepted on its own link;
            // one paid online is accepted as it is paid, and a physical one is signed on paper.
            !digital ? new("Investor acceptance", null, "signed on the physical form", Applies: false)
                : !chosen ? new("Investor acceptance", null, Undecided)
                : online ? new("Investor acceptance", null, "accepted with the online payment", Applies: false)
                : new("Investor acceptance", acceptedOn),
            cheque ? new("Pay-in slip generated", slipOn)
                : !chosen ? new("Pay-in slip generated", null, Undecided)
                : new("Pay-in slip generated", null, $"paid by {payMode}: no instrument to pay in", Applies: false),
            new("Penny drop · repayment account", pennyDropOn, pennyDrop == "FAILED" ? "the account did not verify" : null, Failed: pennyDrop == "FAILED"),
            new("Payment received", paidOn),
            new("KYC verification", kycOn, kyc == "REJECTED" ? "rejected by Operations" : null, Failed: kyc == "REJECTED"),
            new("FDR created", bookedOn, fdr is null ? null : $"FDR {fdr}"),
        ];
        if (cancelledOn is { } cancelled) stages.Add(new("Cancelled", cancelled, "unpaid within the window", Failed: true));
        return stages;
    }
}
