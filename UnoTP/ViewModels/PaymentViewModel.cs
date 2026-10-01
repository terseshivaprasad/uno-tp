using System.Globalization;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

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

    /// <summary>An account on record against the folio, picked by its place in the list: fills the repayment fields.</summary>
    public int? UseRepayment { get; set; }

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
    public bool FillFrom(UnoTP.Models.ChequeFields? read)
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
public sealed class PaymentViewModel(DocumentsViewModel docs, BankForm form, BankBranch? paymentBranch, BankBranch? repaymentBranch, IReadOnlyDictionary<string, string> problems)
{
    public DocumentsViewModel Docs { get; } = docs;
    public BankForm Form { get; } = form;
    public BankBranch? PaymentBranch { get; } = paymentBranch;
    public BankBranch? RepaymentBranch { get; } = repaymentBranch;
    public IReadOnlyDictionary<string, string> Problems { get; } = problems;

    /// <summary>The repayment accounts on record against the investor's folio, to fill the fields from; empty without a folio.</summary>
    public IReadOnlyList<AccountOnRecord> AccountsOnRecord { get; init; } = [];

    /// <summary>
    /// The repayment bank chosen is not on the payment gateway while the deposit is
    /// paid online: what is wrong and what to do. Null while nothing is wrong, or the
    /// deposit is not paid online.
    /// </summary>
    public string? GatewayProblem
    {
        get
        {
            if (PayMode != "Online") return null;
            var ifsc = Form.Repayment.CleanIfsc;
            if (ifsc.Length < 4) return null;
            if (Ref.OnPaymentGateway(ifsc)) return null;
            var bank = RepaymentBranch?.Bank ?? "This bank";
            return GatewayProblemFor(bank);
        }
    }

    /// <summary>The error for a bank the gateway does not take, and what to do about it.</summary>
    public static string GatewayProblemFor(string bank) =>
        $"{bank} is not available on our payment gateway for online payment. What to do: choose a repayment account with a bank that is, or change the payment mode to RTGS or Cheque on Upload Documents.";

    /// <summary>Whether an account on the folio can be used for online payment.</summary>
    public bool OnGateway(AccountOnRecord account) => PayMode != "Online" || Ref.OnPaymentGateway(account.Ifsc);

    private ReferenceData Ref => Docs.Ref;

    /// <summary>An account number as shown on a list: its last four digits.</summary>
    public static string MaskAccount(string number) =>
        number.Length <= 4 ? number : new string('•', 4) + " " + number[^4..];

    public string? Problem(string key) => Problems.GetValueOrDefault(key);

    /// <summary>The payment mode chosen on Upload Documents.</summary>
    public string PayMode => Docs.State.PayMode;

    /// <summary>Paid by an instrument - a cheque - rather than electronically.</summary>
    public bool ByCheque => PayMode.Length > 0 && Docs.DocumentOf(PayMode) is not null;

    /// <summary>A renewal: nothing is paid, so only the repayment account is asked - opened with the deposit's own.</summary>
    public bool IsRenewal => Docs.IsRenewal;

    /// <summary>Whether interest and the maturity amount go back to the account the cheque is drawn on.</summary>
    public bool SameAsPayment => Form.RepaysToPayment(ByCheque);

    /// <summary>Whether the paying account or the cheque were filled in from the cheque read on Upload Documents.</summary>
    public bool FilledFromCheque { get; init; }

    /// <summary>What the cheque filed on Upload Documents was read to say, if one is filed.</summary>
    public ReadCard? ChequeRead => Docs.State.Docs.ContainsKey("payment") ? Docs.State.Reads.GetValueOrDefault("payment") : null;

    public IReadOnlyList<string> CmsLocations => Docs.Ref.CmsLocations;
}

// ===== FD Configuration ======================================================
