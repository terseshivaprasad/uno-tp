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

    /// <summary>PUT applications/{appNo}/details: the holders' and the nominee's details, from Investor Information. New version, or null on a conflict.</summary>
    Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default);

    /// <summary>PUT applications/{appNo}/payment: the accounts and the cheque, from Bank Details &amp; Payment. New version, or null on a conflict.</summary>
    Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default);

    /// <summary>PUT applications/{appNo}/deposit: the deposit as configured. New version, or null on a conflict.</summary>
    Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default);

    /// <summary>
    /// POST applications/{appNo}/submit: the application is submitted and the
    /// investor sent the link to pay and consent. Null on a conflict; the
    /// application, as submitted, otherwise.
    /// </summary>
    Task<Application?> SubmitAsync(string appNo, int version, CancellationToken ct = default);

    /// <summary>
    /// POST applications/{appNo}/resend-link: sends a submitted application's payment
    /// link again, while a resend is left. The submission as it now stands, or null
    /// when there is none to resend.
    /// </summary>
    Task<Submission?> ResendLinkAsync(string appNo, CancellationToken ct = default);

    /// <summary>The partner's saved applications that are theirs to finish, newest first.</summary>
    Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default);

    /// <summary>The partner's applications, for looking one up.</summary>
    Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default);
}

/// <summary>Who an application is for, as Investor Identification established it.</summary>
/// <param name="PanFiled">Set when a PAN copy is on the application already.</param>
/// <param name="Address">The address on record, or empty for an investor with none yet.</param>
/// <param name="OnRecord">What the folio already holds, for an application opened on one.</param>
/// <param name="Gender">As the folio holds it, or empty for an investor with no folio.</param>
public sealed record Holder(
    string Pan, string Dob, string Name, string Folio, bool PanFiled,
    string Address = "", DocsOnRecord? OnRecord = null, string Gender = "");

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
    string PepRelated = "");

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

/// <summary>FD Configuration, as saved. Codes are the reference lists'.</summary>
/// <param name="NoTds">Form 15G or 15H is submitted, so no TDS is deducted.</param>
public sealed record DepositDetails(
    long Amount,
    int TenureMonths,
    string Payout,
    bool AutoRenewal,
    string RenewInstruction,
    bool NoTds,
    string DeliveryType);

/// <param name="Status">Where the application stands once submitted: "payment-pending", then the backend's own.</param>
/// <param name="LinkSentTo">The mobile number the link went to by SMS, masked.</param>
/// <param name="LinkEmailedTo">The e-mail address it went to as well, masked; empty when there is none.</param>
/// <param name="LinkValidUntil">When the payment link stops working.</param>
/// <param name="ResendsLeft">Times the link can still be sent again.</param>
public sealed record Submission(DateTime At, string Status, string LinkSentTo, DateTime LinkValidUntil, int ResendsLeft, string LinkEmailedTo = "");

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

    /// <summary>Set once the KYC is to come from CERSAI; it cannot be taken back.</summary>
    public bool Ckyc { get; set; }

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

    /// <summary>What each copy was read to say, by the slot it came from.</summary>
    public Dictionary<string, ReadCard> Reads { get; init; } = [];

    /// <summary>Every attempt, newest first.</summary>
    public List<LogEntry> Log { get; init; } = [];

    /// <summary>
    /// The joint holders added on Investor Information, by holder type: 02 the second
    /// holder, 03 the third. Their documents are in <see cref="Docs"/>,
    /// <see cref="Attempts"/> and <see cref="Reads"/> under keys that start with it
    /// ("h02-pan"), and filed with DMS under it.
    /// </summary>
    public Dictionary<string, JointHolder> Joint { get; init; } = [];

    public int AttemptsOf(string key) => Attempts.GetValueOrDefault(key);
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
    string Step,
    string Scheme = "",
    IReadOnlyList<MilestoneRecord>? Milestones = null);

/// <summary>A step in an application's history, and when it was reached; <c>At</c> is null for one not reached yet.</summary>
public sealed record MilestoneRecord(string Step, DateTime? At);
