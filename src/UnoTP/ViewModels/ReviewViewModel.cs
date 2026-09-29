using System.Globalization;
using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>One holder on the review, from what every step saved.</summary>
public sealed record ReviewHolder(
    DocumentsViewModel.DocHolder Holder,
    HolderDetails Details,
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
public sealed class ReviewViewModel(DocumentsViewModel docs, DepositQuote? quote, BankBranch? paymentBranch, BankBranch? repaymentBranch)
{
    public DocumentsViewModel Docs { get; } = docs;
    public DepositQuote? Quote { get; } = quote;
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

    public HolderDetails DetailsOf(DocumentsViewModel.DocHolder h) =>
        App.Details?.Holders.FirstOrDefault(d => d.Holder == h.Code) ?? new HolderDetails(h.Code);

    public IReadOnlyList<ReviewHolder> Holders =>
    [
        .. Docs.JointHolders.Prepend(Docs.Investor).Select(h => new ReviewHolder(
            h, DetailsOf(h), Docs.ReadsOf(h)[0].Card, Docs.PoaTypeOf(h),
            Docs.MailTyped(h) ? (Docs.TypedMailOf(h) is { } typed ? DocumentsViewModel.Lines(typed) : "Not typed yet")
                : Docs.MailDifferentOf(h) ? Docs.MailReadOf(h).Lines : "Same as permanent",
            Docs.MailTyped(h) ? (Docs.TypedMailOf(h) is null ? "" : "Typed")
                : Docs.MailDifferentOf(h) ? Docs.MailReadOf(h).State : "")),
    ];

    /// <summary>Every document each holder files, and the payment's and the staff proof's.</summary>
    public IReadOnlyList<ReviewDocument> Documents
    {
        get
        {
            var list = new List<ReviewDocument>();
            foreach (var h in Docs.JointHolders.Prepend(Docs.Investor))
            {
                var who = h.Joint ? $"holder {int.Parse(h.Code)}" : "investor";
                foreach (var def in DocumentsViewModel.HolderSlots)
                {
                    // No proof of a communication address while its upload is off.
                    if (def.Key == DocumentsViewModel.MailSlot.Key && !Docs.CommProofUpload) continue;
                    var v = Docs.View(def, h);
                    list.Add(new ReviewDocument($"{Cap(def.Key == "poa" ? "proof of address" : def.Label)} · {who}",
                        !v.Used ? v.NotApplicable ?? "" : v.Doc is { } d ? (d.Check.Length > 0 ? d.Check : "Filed")
                            : v.Optional ? "Not filed: not needed for a holder on a folio" : "Not filed yet",
                        v.Doc is not null || v.Optional, v.Used));
                }
            }
            foreach (var def in new[] { DocumentsViewModel.FormSlot, DocumentsViewModel.PaymentSlot, DocumentsViewModel.EmpProofSlot, DocumentsViewModel.TdsFormSlot })
            {
                var v = Docs.View(def);
                list.Add(new ReviewDocument(Cap(def.Key == "payment" ? "payment instrument" : def.Label),
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
                var who = h.Holder.Joint ? $"holder {int.Parse(h.Holder.Code)}" : "the investor";
                if (h.Details.Mobile.Length == 0) list.Add(("Investor Information", $"the mobile number of {who}"));
                if (h.Details.Email.Length == 0) list.Add(("Investor Information", $"the e-mail of {who}"));
                if (h.Details.ParentName.Length == 0) list.Add(("Investor Information", $"the father, mother or spouse name of {who}"));
                if (Docs.MailTyped(h.Holder) && Docs.TypedMailOf(h.Holder) is null) list.Add(("Investor Information", $"the communication address of {who}"));
            }
            if (Payment is null) list.Add(("Bank Details & Payment", "the payment and repayment accounts"));
            if (Deposit?.NoTds == true && Docs.View(DocumentsViewModel.TdsFormSlot).Doc is null) list.Add(("FD Configuration", "the Form 121"));
            if (Deposit is null || Deposit.Amount == 0) list.Add(("FD Configuration", "the deposit"));
            return list;
        }
    }

    public bool Ready => Blockers.Count == 0;

    public string Option(IEnumerable<Option> list, string code) => list.FirstOrDefault(o => o.Code == code)?.Name ?? code;

    public PayoutOption? Payout => Deposit is null ? null : Ref.Payouts.FirstOrDefault(p => p.Code == Deposit.Payout);

    public int PaymentLinkHours => Config.LinkValidityHours.GetValueOrDefault("payment");

    /// <summary>The investor's mobile, where the payment link goes by SMS.</summary>
    public string InvestorMobile => DetailsOf(Docs.Investor).Mobile;

    /// <summary>The investor's e-mail, where the payment link goes as well.</summary>
    public string InvestorEmail => DetailsOf(Docs.Investor).Email;

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
