using System.Globalization;
using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>Rupees as the old screens write them: Indian grouping, and in words.</summary>
/// <summary>Where a link went: by SMS and by e-mail, as the backend masked them.</summary>
public static class SentTo
{
    /// <summary>"98••••1000 and ab••••@gmail.com", or the one there is.</summary>
    public static string Both(string mobile, string email) =>
        string.Join(" and ", new[] { mobile, email }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public static class Money
{
    /// <summary>"₹ 5,00,000".</summary>
    public static string Rupees(decimal amount) => "₹ " + Group((long)Math.Round(amount, MidpointRounding.AwayFromZero));

    /// <summary>Lakh and crore grouping: 20000000 as "2,00,00,000".</summary>
    public static string Group(long n)
    {
        var digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= 3) return (n < 0 ? "-" : "") + digits;
        var head = digits[..^3];
        var groups = new List<string>();
        while (head.Length > 2) { groups.Insert(0, head[^2..]); head = head[..^2]; }
        if (head.Length > 0) groups.Insert(0, head);
        return (n < 0 ? "-" : "") + string.Join(",", groups) + "," + digits[^3..];
    }

    private static readonly string[] Ones =
    [
        "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    private static readonly string[] Tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private static string Below100(long n) => n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 > 0 ? "-" + Ones[n % 10] : "");

    /// <summary>"Five lakh rupees".</summary>
    public static string InWords(long n)
    {
        if (n <= 0) return "";
        var parts = new List<string>();
        foreach (var (unit, name) in new[] { (10_000_000L, "crore"), (100_000L, "lakh"), (1_000L, "thousand"), (100L, "hundred") })
        {
            var q = n / unit;
            if (q > 0) { parts.Add((q >= 100 ? InWords(q).Replace(" rupees", "").ToLowerInvariant() : Below100(q)) + " " + name); n %= unit; }
        }
        if (n > 0) parts.Add(Below100(n));
        var s = string.Join(" ", parts);
        return char.ToUpperInvariant(s[0]) + s[1..] + " rupees";
    }

    /// <summary>An account number as it is shown: all but the last four digits hidden.</summary>
    public static string MaskAccount(string account) =>
        account.Length > 4 ? new string('•', Math.Min(8, account.Length - 4)) + account[^4..] : account;

    /// <summary>A day as the pages write it: "14 Sep 2029".</summary>
    public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string Day(DateTime at) => at.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);
}

// ===== Bank Details & Payment ================================================

/// <summary>One account as the form posts it. A field posted empty binds as null, so every string on these forms reads null as "".</summary>
public sealed class AccountForm
{
    public string Ifsc { get => ifscValue; set => ifscValue = value ?? ""; }
    private string ifscValue = "";
    public string AccountNumber { get => accountNumberValue; set => accountNumberValue = value ?? ""; }
    private string accountNumberValue = "";
    public string AccountNumberConfirm { get => accountNumberConfirmValue; set => accountNumberConfirmValue = value ?? ""; }
    private string accountNumberConfirmValue = "";
    public bool SameAsPayment { get; set; }

    public string CleanIfsc => Ifsc.Trim().ToUpperInvariant();
    public string CleanAccount => new(AccountNumber.Where(char.IsAsciiDigit).ToArray());
}

public sealed class ChequeForm
{
    public string Number { get => numberValue; set => numberValue = value ?? ""; }
    private string numberValue = "";

    /// <summary>
    /// The date as "DD / MM / YYYY", made of the three boxes the page draws it in
    /// (the app's date control). Set whole - from what was saved, or read off the
    /// cheque - it is split into them.
    /// </summary>
    public string Date
    {
        get => Dd.Length + Mm.Length + Yyyy.Length == 0 ? "" : $"{Dd} / {Mm} / {Yyyy}";
        set
        {
            var digits = new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
            (Dd, Mm, Yyyy) = digits.Length == 8 ? (digits[..2], digits[2..4], digits[4..]) : ("", "", "");
        }
    }

    public string Dd { get => ddValue; set => ddValue = (value ?? "").Trim(); }
    private string ddValue = "";
    public string Mm { get => mmValue; set => mmValue = (value ?? "").Trim(); }
    private string mmValue = "";
    public string Yyyy { get => yyyyValue; set => yyyyValue = (value ?? "").Trim(); }
    private string yyyyValue = "";

    public string CmsLocation { get => cmsLocationValue; set => cmsLocationValue = value ?? ""; }
    private string cmsLocationValue = "";
}

/// <summary>What Bank Details &amp; Payment posts. <c>Find</c> names the IFSC to look up without moving on.</summary>
public sealed class BankForm
{
    public AccountForm Payment { get; set; } = new();
    public AccountForm Repayment { get; set; } = new() { SameAsPayment = true };
    public ChequeForm Cheque { get; set; } = new();
    public string? Find { get; set; }

    /// <summary>The form as the backend last saved it; empty before it ever was.</summary>
    public static BankForm From(PaymentDetails? saved)
    {
        if (saved is null) return new BankForm();
        AccountForm Of(BankAccount? a) => new() { Ifsc = a?.Ifsc ?? "", AccountNumber = a?.AccountNumber ?? "", AccountNumberConfirm = a?.AccountNumber ?? "" };
        var form = new BankForm { Payment = Of(saved.Payment), Repayment = Of(saved.Repayment) };
        form.Repayment.SameAsPayment = saved.RepaymentSameAsPayment;
        if (saved.Cheque is { } c) form.Cheque = new ChequeForm { Number = c.Number, Date = c.Date.Replace("-", " / "), CmsLocation = c.CmsLocation };
        return form;
    }

    /// <summary>
    /// Fills what is still empty of the paying account and the cheque from what was
    /// read off the cheque filed on Upload Documents. Nothing typed or saved is
    /// replaced. True when anything was filled.
    /// </summary>
    public bool FillFrom(UnoTP.Backend.External.ChequeFields? read)
    {
        if (read is null) return false;
        var filled = false;
        void Fill(string now, string from, Action<string> set)
        {
            if (now.Trim().Length > 0 || from.Length == 0) return;
            set(from);
            filled = true;
        }
        Fill(Payment.Ifsc, read.Ifsc, v => Payment.Ifsc = v);
        Fill(Payment.AccountNumber, read.AccountNumber, v => (Payment.AccountNumber, Payment.AccountNumberConfirm) = (v, v));
        Fill(Cheque.Number, read.Number, v => Cheque.Number = v);
        Fill(Cheque.Date, read.Date, v => Cheque.Date = v);
        return filled;
    }

    /// <summary>
    /// Whether the repayment account is the payment account. Only a cheque names an
    /// account the deposit is paid from, so paid any other way it never is.
    /// </summary>
    public bool RepaysToPayment(bool byCheque) => byCheque && Repayment.SameAsPayment;

    /// <summary>
    /// The form as the backend saves it: typed, whether finished or not. The paying
    /// account and the cheque are kept only when the deposit is paid by cheque.
    /// </summary>
    public PaymentDetails ToDetails(bool byCheque)
    {
        var payment = byCheque ? new BankAccount(Payment.CleanIfsc, Payment.CleanAccount) : null;
        var same = RepaysToPayment(byCheque);
        var repayment = same ? payment : new BankAccount(Repayment.CleanIfsc, Repayment.CleanAccount);
        var cheque = byCheque ? new ChequeDetails(Cheque.Number.Trim(), ChequeDay ?? Cheque.Date.Trim(), Cheque.CmsLocation) : null;
        return new PaymentDetails(payment, repayment, same, cheque);
    }

    /// <summary>The cheque date as dd-MM-yyyy, or null when it is not a date.</summary>
    public string? ChequeDay =>
        DateTime.TryParseExact(new string(Cheque.Date.Where(char.IsAsciiDigit).ToArray()), "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : null;

    /// <summary>What stops the step, by field; empty when nothing does.</summary>
    public Dictionary<string, string> Problems(bool byCheque, BankBranch? paymentBranch, BankBranch? repaymentBranch, IReadOnlyList<string> cmsLocations)
    {
        var problems = new Dictionary<string, string>();
        void Account(AccountForm a, string key, BankBranch? branch)
        {
            if (a.CleanIfsc.Length == 0) problems[key + ".Ifsc"] = "Search for the bank and pick its branch";
            else if (branch is null) problems[key + ".Ifsc"] = "No branch has this IFSC — pick one from the search, or check it against the cheque";
            if (a.CleanAccount.Length is < 6 or > 18) problems[key + ".AccountNumber"] = "Enter the account number, 6 to 18 digits";
            else if (new string(a.AccountNumberConfirm.Where(char.IsAsciiDigit).ToArray()) != a.CleanAccount) problems[key + ".AccountNumberConfirm"] = "Does not match the account number";
        }
        if (byCheque) Account(Payment, "Payment", paymentBranch);
        if (!RepaysToPayment(byCheque)) Account(Repayment, "Repayment", repaymentBranch);
        if (byCheque)
        {
            if (Cheque.Number.Trim() is not { Length: 6 } n || !n.All(char.IsAsciiDigit)) problems["Cheque.Number"] = "Enter the six-digit cheque number";
            if (ChequeDay is null) problems["Cheque.Date"] = "Enter the cheque date";
            if (!cmsLocations.Contains(Cheque.CmsLocation)) problems["Cheque.CmsLocation"] = Cheque.CmsLocation.Trim().Length == 0 ? "Enter the Axis CMS branch" : "Choose an Axis CMS branch from the list";
        }
        return problems;
    }
}

/// <summary>Bank Details &amp; Payment: the application's accounts and cheque, and the branches their IFSCs name.</summary>
public sealed class BankDetailsViewModel(UploadDocumentsViewModel docs, BankForm form, BankBranch? paymentBranch, BankBranch? repaymentBranch, IReadOnlyDictionary<string, string> problems)
{
    public UploadDocumentsViewModel Docs { get; } = docs;
    public BankForm Form { get; } = form;
    public BankBranch? PaymentBranch { get; } = paymentBranch;
    public BankBranch? RepaymentBranch { get; } = repaymentBranch;
    public IReadOnlyDictionary<string, string> Problems { get; } = problems;

    public string? Problem(string key) => Problems.GetValueOrDefault(key);

    /// <summary>The payment mode chosen on Upload Documents.</summary>
    public string PayMode => Docs.State.PayMode;

    /// <summary>Paid by an instrument - a cheque - rather than electronically.</summary>
    public bool ByCheque => PayMode.Length > 0 && Docs.DocumentOf(PayMode) is not null;

    /// <summary>Whether interest and the maturity amount go back to the account the cheque is drawn on.</summary>
    public bool SameAsPayment => Form.RepaysToPayment(ByCheque);

    /// <summary>Whether the paying account or the cheque were filled in from the cheque read on Upload Documents.</summary>
    public bool FilledFromCheque { get; init; }

    /// <summary>What the cheque filed on Upload Documents was read to say, if one is filed.</summary>
    public ReadCard? ChequeRead => Docs.State.Docs.ContainsKey("payment") ? Docs.State.Reads.GetValueOrDefault("payment") : null;

    public IReadOnlyList<string> CmsLocations => Docs.Ref.CmsLocations;

    /// <summary>The test branches, while test data is on and the backend has them.</summary>
    public DemoBanks? Demo { get; init; }
}

// ===== FD Configuration ======================================================

/// <summary>What FD Configuration posts.</summary>
public sealed class DepositForm
{
    public string Amount { get => amountValue; set => amountValue = value ?? ""; }
    private string amountValue = "";
    public int TenureMonths { get; set; }
    public string InterestPayout { get => interestPayoutValue; set => interestPayoutValue = value ?? ""; }
    private string interestPayoutValue = "";
    public bool AutoRenewal { get; set; }
    public string RenewInstruction { get => renewInstructionValue; set => renewInstructionValue = value ?? ""; }
    private string renewInstructionValue = "";
    public bool NoTds { get; set; }
    public string DeliveryType { get => deliveryTypeValue; set => deliveryTypeValue = value ?? ""; }
    private string deliveryTypeValue = "";

    public long AmountValue => long.TryParse(new string(Amount.Where(char.IsAsciiDigit).ToArray()), out var n) ? n : 0;

    /// <summary>The deposit as the backend last saved it, or the first of each list before it ever was.</summary>
    public static DepositForm From(DepositDetails? saved, ReferenceData reference) => saved is null
        ? new DepositForm
        {
            TenureMonths = reference.Tenures.FirstOrDefault(),
            InterestPayout = reference.Payouts.FirstOrDefault()?.Code ?? "",
            DeliveryType = reference.DeliveryTypes.FirstOrDefault()?.Code ?? "",
        }
        : new DepositForm
        {
            Amount = saved.Amount > 0 ? Money.Group(saved.Amount) : "",
            TenureMonths = saved.TenureMonths,
            InterestPayout = saved.Payout,
            AutoRenewal = saved.AutoRenewal,
            RenewInstruction = saved.RenewInstruction,
            NoTds = saved.NoTds,
            DeliveryType = saved.DeliveryType,
        };

    public DepositDetails ToDetails() =>
        new(AmountValue, TenureMonths, InterestPayout, AutoRenewal, AutoRenewal ? RenewInstruction : "", NoTds, DeliveryType);

    /// <summary>What is wrong with the amount, or null.</summary>
    public string? AmountProblem(AppConfig config)
    {
        var n = AmountValue;
        if (n == 0) return "Required — enter the deposit amount";
        if (n < config.MinAmount) return $"Below the {Money.Rupees(config.MinAmount)} minimum";
        if (n > config.MaxAmount) return $"Above the {Money.Rupees(config.MaxAmount)} maximum";
        if (config.AmountStep > 0 && n % config.AmountStep != 0) return $"Not a multiple of {Money.Rupees(config.AmountStep)} — {Money.InWords(n).ToLowerInvariant()}";
        return null;
    }

    /// <summary>What stops the step, by field.</summary>
    public Dictionary<string, string> Problems(AppConfig config, ReferenceData reference)
    {
        var problems = new Dictionary<string, string>();
        if (AmountProblem(config) is { } amount) problems["Amount"] = amount;
        if (!reference.Tenures.Contains(TenureMonths)) problems["TenureMonths"] = "Choose the tenure";
        if (reference.Payouts.All(p => p.Code != InterestPayout)) problems["InterestPayout"] = "Choose the interest payout";
        if (AutoRenewal && reference.RenewInstructions.All(r => r.Code != RenewInstruction)) problems["RenewInstruction"] = "Required — choose what auto renewal renews";
        if (reference.DeliveryTypes.All(d => d.Code != DeliveryType)) problems["DeliveryType"] = "Choose the delivery type";
        return problems;
    }
}

/// <summary>FD Configuration: the deposit as configured, and what the backend quotes for it.</summary>
public sealed class FdConfigViewModel(UploadDocumentsViewModel docs, DepositForm form, DepositQuote? quote, IReadOnlyDictionary<string, string> problems)
{
    public UploadDocumentsViewModel Docs { get; } = docs;
    public DepositForm Form { get; } = form;

    /// <summary>The backend's quote, once the amount is valid.</summary>
    public DepositQuote? Quote { get; } = quote;

    public IReadOnlyDictionary<string, string> Problems { get; } = problems;
    public ReferenceData Ref => Docs.Ref;
    public AppConfig Config => Docs.Config;

    public string? Problem(string key) => Problems.GetValueOrDefault(key);

    public PayoutOption? Payout => Ref.Payouts.FirstOrDefault(p => p.Code == Form.InterestPayout);
    public bool Cumulative => Payout?.PerYear == 0;

    /// <summary>The category the deposit is booked as, from Upload Documents.</summary>
    public string Category => Docs.State.Category.Length > 0 ? Docs.CategoryName(Docs.State.Category) : "Not chosen yet";

    /// <summary>The form that exempts the investor from TDS: 15H at the senior citizen age and over, 15G under it.</summary>
    public string TdsForm => SeniorByDob(Docs.Investor.Who.Dob, Config.SeniorAge) ? "Form 15H" : "Form 15G";

    /// <summary>Where an e-receipt goes: the investor's e-mail on Investor Information.</summary>
    public string Email => Docs.App.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor)?.Email ?? "";

    public string AmountHint => Form.AmountProblem(Config) is { } problem ? problem
        : $"{Money.InWords(Form.AmountValue)} · in multiples of {Money.Rupees(Config.AmountStep)}";

    /// <summary>What is still to do before Proceed opens, in words.</summary>
    public IReadOnlyList<string> Outstanding => [.. Problems.Values];

    public static bool SeniorByDob(string dob, int seniorAge) =>
        DateTime.TryParseExact(dob, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var born)
        && born.AddYears(seniorAge) <= DateTime.Today;
}

// ===== Review Summary and Submitted ==========================================

/// <summary>One holder on the review, from what every step saved.</summary>
public sealed record ReviewHolder(
    UploadDocumentsViewModel.DocHolder Holder,
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
public sealed class ReviewViewModel(UploadDocumentsViewModel docs, DepositQuote? quote, BankBranch? paymentBranch, BankBranch? repaymentBranch)
{
    public UploadDocumentsViewModel Docs { get; } = docs;
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

    public HolderDetails DetailsOf(UploadDocumentsViewModel.DocHolder h) =>
        App.Details?.Holders.FirstOrDefault(d => d.Holder == h.Code) ?? new HolderDetails(h.Code);

    public IReadOnlyList<ReviewHolder> Holders =>
    [
        .. Docs.JointHolders.Prepend(Docs.Investor).Select(h => new ReviewHolder(
            h, DetailsOf(h), Docs.ReadsOf(h)[0].Card, Docs.PoaTypeOf(h),
            Docs.MailDifferentOf(h) ? Docs.MailReadOf(h).Lines : "Same as permanent",
            Docs.MailDifferentOf(h) ? Docs.MailReadOf(h).State : "")),
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
                foreach (var def in UploadDocumentsViewModel.HolderSlots)
                {
                    var v = Docs.View(def, h);
                    list.Add(new ReviewDocument($"{Cap(def.Key == "poa" ? "proof of address" : def.Label)} · {who}",
                        !v.Used ? v.NotApplicable ?? "" : v.Doc is { } d ? (d.Check.Length > 0 ? d.Check : "Filed") : "Not filed yet",
                        v.Doc is not null, v.Used));
                }
            }
            foreach (var def in new[] { UploadDocumentsViewModel.FormSlot, UploadDocumentsViewModel.PaymentSlot, UploadDocumentsViewModel.EmpProofSlot })
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
            }
            if (Payment is null) list.Add(("Bank Details & Payment", "the payment and repayment accounts"));
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
