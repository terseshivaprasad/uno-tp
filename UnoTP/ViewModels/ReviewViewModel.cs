using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>One holder on the review, from what every step saved.</summary>
public sealed record ReviewHolder(
    DocumentsViewModel.DocHolder Holder,
    HolderDetails Details,
    string Gender,
    ReadCard Permanent,
    string PoaType,
    string Mailing,
    string MailingState);

/// <summary>A document on the review: filed, not needed, or still to file.</summary>
public sealed record ReviewDocument(string Title, string Detail, bool Done, bool Applies);

/// <summary>
/// Review Summary: everything the application holds, as the backend has it, and what
/// still stands between it and submitting.
/// </summary>
public sealed class ReviewViewModel(DocumentsViewModel docs, DepositQuote? quote, BankBranch? paymentBranch, BankBranch? repaymentBranch,
    SourceOfFundsCheck? sourceOfFunds = null)
{
    public DocumentsViewModel Docs { get; } = docs;
    public DepositQuote? Quote { get; } = quote;

    /// <summary>Whether FD Configuration asks the source of funds for this deposit; null where the page does not check it.</summary>
    public SourceOfFundsCheck? SourceOfFunds { get; } = sourceOfFunds;
    public BankBranch? PaymentBranch { get; } = paymentBranch;
    public BankBranch? RepaymentBranch { get; } = repaymentBranch;

    public Application App => Docs.App;
    public ReferenceData Ref => Docs.Ref;
    public AppConfig Config => Docs.Config;
    public DepositDetails? Deposit => App.Deposit;
    public PaymentDetails? Payment => App.Payment;
    public NomineeDetails? Nominee => App.Details?.Nominee;

    /// <summary>Set when a submit was refused, with why.</summary>
    public string? Refused { get; init; }

    /// <summary>What Bank Details &amp; Payment still lacks as saved, by field; empty when nothing, or where the page does not check it.</summary>
    public IReadOnlyDictionary<string, string> BankProblems { get; init; } = new Dictionary<string, string>();

    public HolderDetails DetailsOf(DocumentsViewModel.DocHolder h) =>
        App.Details?.Holders.FirstOrDefault(d => d.Holder == h.Code) ?? new HolderDetails(h.Code);

    public IReadOnlyList<ReviewHolder> Holders =>
    [
        .. Docs.JointHolders.Prepend(Docs.Investor).Select(h => new ReviewHolder(
            h, DetailsOf(h), GenderOf(h), Docs.ReadsOf(h)[0].Card, Docs.PoaTypeOf(h),
            Docs.MailTyped(h) ? (Docs.TypedMailOf(h) is { } typed ? DocumentsViewModel.Lines(typed) : "Not typed yet")
                : Docs.MailDifferentOf(h) ? Docs.MailReadOf(h).Lines : "Same as permanent",
            Docs.MailTyped(h) ? (Docs.TypedMailOf(h) is null ? "" : "Typed")
                : Docs.MailDifferentOf(h) ? Docs.MailReadOf(h).State : "")),
    ];

    // A holder's gender as Investor Information shows it: the one on the folio or
    // read off a proof, else the one chosen on that page.
    private string GenderOf(DocumentsViewModel.DocHolder h)
    {
        if (!h.Joint) return Docs.HolderGender;
        if (h.Who.Gender.Length > 0) return h.Who.Gender;
        return DetailsOf(h).Gender;
    }

    /// <summary>Every document each holder files, and the payment's and the staff proof's.</summary>
    public IReadOnlyList<ReviewDocument> Documents
    {
        get
        {
            var list = new List<ReviewDocument>();
            foreach (var h in Docs.JointHolders.Prepend(Docs.Investor))
            {
                var who = h.Joint ? Messages.ReviewSummary.HolderNumber(int.Parse(h.Code)) : "investor";
                foreach (var def in DocumentsViewModel.HolderSlots)
                {
                    // No proof of a communication address while its upload is off.
                    if (def.Key == DocumentsViewModel.MailSlot.Key && !Docs.CommProofUpload) continue;
                    var v = Docs.View(def, h);
                    list.Add(new ReviewDocument($"{Capitalize(def.Key == "poa" ? "proof of address" : def.Label)} · {who}",
                        !v.Used ? v.NotApplicable ?? "" : v.Doc is { } d ? (d.Check.Length > 0 ? d.Check : "Filed") : "Not filed yet",
                        v.Doc is not null, v.Used));
                }
            }
            foreach (var def in new[] { DocumentsViewModel.FormSlot, DocumentsViewModel.PaymentSlot, DocumentsViewModel.EmpProofSlot, DocumentsViewModel.TdsFormSlot })
            {
                var v = Docs.View(def);
                list.Add(new ReviewDocument(Capitalize(def.Key == "payment" ? "payment instrument" : def.Label),
                    !v.Used ? v.NotApplicable ?? "" : v.Doc is { } d ? (d.Check.Length > 0 ? d.Check : "Filed") : "Not filed yet",
                    v.Doc is not null, v.Used));
            }
            return list;
        }
    }

    /// <summary>What stops the application being submitted, by the step that has it.</summary>
    public IReadOnlyList<(string Step, string What)> Blockers
    {
        get
        {
            var list = new List<(string, string)>();
            foreach (var left in Docs.Outstanding()) list.Add(("Upload Documents", left));
            foreach (var h in Holders)
            {
                var who = h.Holder.Joint ? Messages.ReviewSummary.HolderNumber(int.Parse(h.Holder.Code)) : Messages.ReviewSummary.TheInvestor;
                foreach (var (step, what) in MandatoryMissing(h)) list.Add((step, Messages.ReviewSummary.MissingOf(what, who)));
                if (Docs.MailTyped(h.Holder) && Docs.TypedMailOf(h.Holder) is null) list.Add(("Investor Information", Messages.ReviewSummary.MissingMailAddress(who)));
            }
            // Saved is not enough: a draft kept half-filled has no bank picked.
            if (Payment is null) list.Add(("Bank Details & Payment", Messages.ReviewSummary.MissingAccounts));
            else
            {
                if (BankProblems.Keys.Any(k => k.StartsWith("Payment."))) list.Add(("Bank Details & Payment", Messages.ReviewSummary.MissingPaymentBank));
                if (BankProblems.Keys.Any(k => k.StartsWith("Repayment."))) list.Add(("Bank Details & Payment", Messages.ReviewSummary.MissingRepaymentBank));
                if (BankProblems.Keys.Any(k => k.StartsWith("Cheque."))) list.Add(("Bank Details & Payment", Messages.ReviewSummary.MissingCheque));
            }
            if (Deposit?.NoTds == true && Docs.View(DocumentsViewModel.TdsFormSlot).Doc is null) list.Add(("FD Configuration", Messages.ReviewSummary.MissingTdsForm));
            if (Deposit is null || Deposit.Amount == 0) list.Add(("FD Configuration", Messages.ReviewSummary.MissingDeposit));
            if (SourceOfFunds is { Asked: true } && (Deposit?.SourceOfFunds ?? "").Length == 0) list.Add(("FD Configuration", Messages.ReviewSummary.MissingSourceOfFunds));
            return list;
        }
    }

    public bool Ready => Blockers.Count == 0;

    // What every holder must have before the application goes, fresh or on a folio
    // alike: name, PAN, date of birth, gender, marital status, the father's, mother's
    // or spouse's name, mobile, e-mail, the first line, the city and the PIN code of
    // the permanent address, the photograph and the proof of address. Each comes with the
    // step that sets it. A document on the holder's record counts as there.
    private IEnumerable<(string Step, string What)> MandatoryMissing(ReviewHolder h)
    {
        const string information = "Investor Information";
        var joint = h.Holder.Joint;
        var who = h.Holder.Who;
        // The investor is identified on the first step and files on Upload Documents;
        // a joint holder is added, and files, on Investor Information.
        var identifiedOn = joint ? information : "Investor Identification";
        var filedOn = joint ? information : "Upload Documents";

        if (who.Name.Length == 0) yield return (filedOn, Messages.ReviewSummary.MissingName);
        if (who.Pan.Length == 0) yield return (identifiedOn, Messages.ReviewSummary.MissingPan);
        if (who.Dob.Length == 0) yield return (identifiedOn, Messages.ReviewSummary.MissingDob);
        if (h.Gender.Length == 0) yield return (information, Messages.ReviewSummary.MissingGender);
        if (h.Details.MaritalStatus.Length == 0) yield return (information, Messages.ReviewSummary.MissingMaritalStatus);
        if (h.Details.ParentName.Length == 0) yield return (information, Messages.ReviewSummary.MissingParentName);
        if (h.Details.Mobile.Length == 0) yield return (information, Messages.ReviewSummary.MissingMobile);
        if (h.Details.Email.Length == 0) yield return (information, Messages.ReviewSummary.MissingEmail);

        // An investor whose KYC was fetched from CKYC files no proof and no photograph:
        // the address and both documents are the CKYC record's.
        if (Docs.State.Ckyc && !joint) yield break;

        var (lines, pinCode) = Docs.PermanentAddressTextOf(h.Holder);
        if (lines.Length == 0) yield return (filedOn, Messages.ReviewSummary.MissingAddressLine1);
        // A proof filed here sets the city where it is filed; a holder who files none
        // has their record's, which Investor Information saves with their details.
        var citySetOn = Docs.AddressReadHere(h.Holder) ? filedOn : information;
        if (Docs.PermanentCityOf(h.Holder, h.Details).Length == 0) yield return (citySetOn, Messages.ReviewSummary.MissingCity);
        if (pinCode.Length == 0) yield return (filedOn, Messages.ReviewSummary.MissingPinCode);

        // The investor's two documents are among what Upload Documents still lacks
        // (Docs.Outstanding), listed above; a joint holder's are checked here.
        if (!joint) yield break;
        if (Docs.View(DocumentsViewModel.PhotoSlot, h.Holder).Missing) yield return (filedOn, Messages.ReviewSummary.MissingPhoto);
        if (Docs.View(DocumentsViewModel.PoaSlot, h.Holder).Missing) yield return (filedOn, Messages.ReviewSummary.MissingPoa);
    }

    public string Option(IEnumerable<Option> list, string code) => list.FirstOrDefault(o => o.Code == code)?.Name ?? code;

    public PayoutOption? Payout => Deposit is null ? null : Ref.Payouts.FirstOrDefault(p => p.Code == Deposit.Payout);

    public int PaymentLinkHours => Config.LinkValidityHours.GetValueOrDefault("payment");

    /// <summary>How long a payment link stays open, as the pages say it: in days when it is whole days, else in hours.</summary>
    public string PaymentLinkValidFor
    {
        get
        {
            var hours = PaymentLinkHours;
            if (hours >= 24 && hours % 24 == 0) return hours == 24 ? "1 day" : $"{hours / 24} days";
            return $"{hours} hours";
        }
    }

    /// <summary>Days an unpaid application stands from when it was started; a link can be sent, or sent again, until then.</summary>
    public int ApplicationDays => Config.CancellationDays;

    /// <summary>The investor's mobile, where the payment link goes by SMS.</summary>
    public string InvestorMobile => DetailsOf(Docs.Investor).Mobile;

    /// <summary>The investor's e-mail, where the payment link goes as well.</summary>
    public string InvestorEmail => DetailsOf(Docs.Investor).Email;

    /// <summary>The words with the first letter in capitals.</summary>
    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
