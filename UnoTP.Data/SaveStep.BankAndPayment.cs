using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

// Bank Details & Payment: t_FD_BT_Payment_Dtl (how the deposit is paid, and the
// account it is paid from) and t_FD_BT_Investor_Bank_Dtl (the repayment account).
internal static partial class Sections
{
    /// <param name="branches">The branch each IFSC names, as looked up before the save.</param>
    public static async Task WritePaymentAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState? upload,
        PaymentDetails payment, IReadOnlyDictionary<string, BankBranch> branches)
    {
        await RetireAsync(db, tx, at, "t_FD_BT_Payment_Dtl");
        var pay = Branch(payment.Payment, branches);
        await db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_Payment_Dtl (f_Appl_No, f_Payment_Mode, f_Cheque_DD_No, f_Cheque_DD_Date,
                f_Drawn_Bank_Name, f_Bank_Branch_Name, f_Bank_MICR, f_Bank_NEFT, f_BankAccountNo, f_CMS_Loc_CD, f_CMS_Loc_Desc, f_CMS_BANK_NAME, f_FolioNo,
                f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
            VALUES (@AppNo, @PayMode, @ChequeNo, @ChequeDate,
                @Bank, @BranchName, @Micr, @Ifsc, @AccountNo, @CmsLocationCode, @CmsLocation, @CmsBank, @Folio,
                @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            at.AppNo, PayMode = upload?.PayMode ?? "",
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
        await RetireAsync(db, tx, at, "t_FD_BT_Investor_Bank_Dtl");
        var repay = Branch(payment.Repayment, branches);
        await db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_Investor_Bank_Dtl (f_Appl_No, f_MICRCode, f_NEFTCode, f_BankName, f_BranchName, f_BankAccountNo, f_sameAsCheque, f_FolioNo,
                f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
            VALUES (@AppNo, @Micr, @Ifsc, @Bank, @BranchName, @AccountNo, @SameAsPayment, @Folio,
                @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
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
