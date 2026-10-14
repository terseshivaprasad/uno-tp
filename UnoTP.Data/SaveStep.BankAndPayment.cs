using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

// Bank Details & Payment: t_FD_BT_Payment_Dtl (how the deposit is paid, and the
// account it is paid from) and t_FD_BT_Investor_Bank_Dtl (the repayment account).
internal static partial class Sections
{
    /// <param name="branches">The branch each IFSC names, as looked up before the save.</param>
    /// <param name="paymentModes">The 'paymentModes' list: the mode is saved by its code (1, 2 or 3), the upload state holds its name.</param>
    public static async Task WritePaymentAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState? upload,
        PaymentDetails payment, IReadOnlyDictionary<string, BankBranch> branches, IReadOnlyList<PaymentModeOption> paymentModes)
    {
        await RetireAsync(db, tx, at, BankDetailsQueries.RetirePayment);
        var pay = Branch(payment.Payment, branches);
        await db.ExecuteAsync(BankDetailsQueries.InsertPayment, new
        {
            at.AppNo, PayMode = PaymentModeOption.CodeOf(paymentModes, upload?.PayMode ?? ""),
            ChequeNo = payment.Cheque?.Number, ChequeDate = Dates.ParseDdMmYyyy(payment.Cheque?.Date),
            Bank = CutOrNull(pay?.Bank, 50), BranchName = CutOrNull(pay?.Branch, 50), pay?.Micr,
            payment.Payment?.Ifsc, AccountNo = payment.Payment?.AccountNumber,
            // The Axis CMS branch as it was picked: its code and its name.
            CmsLocationCode = EmptyAsNull(payment.Cheque?.CmsLocationCode ?? ""), payment.Cheque?.CmsLocation,
            // A cheque is presented at an Axis Bank CMS location.
            CmsBank = payment.Cheque is null ? null : "Axis Bank", at.Folio,
            Source, at.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);

        // The repayment account's columns take no NULL: an account not given yet goes in blank.
        await RetireAsync(db, tx, at, BankDetailsQueries.RetireRepaymentAccount);
        var repay = Branch(payment.Repayment, branches);
        await db.ExecuteAsync(BankDetailsQueries.InsertRepaymentAccount, new
        {
            at.AppNo, Micr = repay?.Micr ?? "", Ifsc = payment.Repayment?.Ifsc ?? "",
            Bank = Cut(repay?.Bank, 50), BranchName = Cut(repay?.Branch, 50), AccountNo = payment.Repayment?.AccountNumber ?? "",
            SameAsPayment = payment.RepaymentSameAsPayment, at.Folio,
            Source, at.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);
    }

    private static BankBranch? Branch(BankAccount? account, IReadOnlyDictionary<string, BankBranch> branches) =>
        account is null ? null : branches.GetValueOrDefault(account.Ifsc.Trim().ToUpperInvariant());

    // As Cut, for a column that is left empty when there is nothing to put in it.
    private static string? CutOrNull(string? value, int length)
    {
        if (value is null) return null;
        return Cut(value, length);
    }
}
