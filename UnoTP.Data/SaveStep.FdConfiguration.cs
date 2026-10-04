using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

internal static partial class Sections
{
    // ----- FD Configuration: t_FD_BT_Investment_Dtl --------------------------------
    //
    // The form number, sub-broker and employee proof type have no column here; they
    // stay on the upload step's JSON. The quote's interest, maturity and dates are
    // kept on t_Unotp_Application_Mst. The CMS location is the one on the
    // application's payment row, which is written before this one.

    /// <summary>f_Depositor_Status_Code and f_Ind_Nind: every investor here is an individual.</summary>
    private const string Individual = "IND";

    /// <param name="line">The rate card's row for the deposit - its scheme, scheme code and rate are the row's; null where the card offers none.</param>
    /// <param name="lists">The employee relations list, for the code a relation is saved as.</param>
    public static async Task WriteDepositAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState? upload,
        DepositDetails deposit, string? renews, RateOption? line, ReferenceData lists)
    {
        var u = upload ?? new UploadState();
        await RetireAsync(db, tx, at, "t_FD_BT_Investment_Dtl");
        await db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_Investment_Dtl (f_Appl_No, f_Status, f_Amount, f_Tenure, f_Int_Freq, f_Scheme, f_Scheme_Code,
                f_Is_Auto_Renewal, f_Renewal_For, f_TDS_Flag, f_FDR_Dispatch_Mode,
                f_AML_Source_Of_Funds, f_AML_Source_Of_Funds_Remarks, f_AML_Source_Of_Funds_reason,
                f_ApplicationDeclarationType, f_Category, f_Source, f_Broker_Code,
                f_Employee_Code, f_EmpCompanyName, f_EmpHolder, f_EmpRelation, f_ExistingFDRNo, f_ExistingFDRNoRenewalFor,
                f_Depositor_Status_Code, f_Ind_Nind, f_HNG, f_FolioNo, f_Cms_Location_Code, f_Cms_Location_Name,
                f_Int_Rate, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
            VALUES (@AppNo, @Status, @Amount, @Tenure, @Payout, @Scheme, @SchemeCode,
                @AutoRenewal, @RenewInstruction, @TdsFlag, @DeliveryType,
                @SourceOfFunds, @SourceOfFundsRemark, @SourceOfFundsReason,
                @AppType, @Category, @Source, @SourceCode,
                @EmpCode, @EmpCompany, @EmpHolder, @EmpRelation, @Renews, @ExistingRenewalFor,
                @Individual, @Individual, @Hng, @Folio,
                (SELECT TOP (1) f_CMS_Loc_CD FROM dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC),
                (SELECT TOP (1) f_CMS_Loc_Desc FROM dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC),
                @Rate, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            at.AppNo, at.Status, deposit.Amount, Tenure = deposit.TenureMonths, deposit.Payout,
            line?.Scheme, line?.SchemeCode,
            deposit.AutoRenewal, RenewInstruction = PrincipalOrFull(deposit.RenewInstruction), TdsFlag = TdsFlag(deposit.NoTds), deposit.DeliveryType,
            deposit.SourceOfFunds, deposit.SourceOfFundsRemark, deposit.SourceOfFundsReason,
            u.AppType, u.Category, Source, u.SourceCode,
            u.EmpCode, u.EmpCompany, EmpHolder = EmpHolderCode(u.EmpHolder),
            EmpRelation = MasterLists.CodeOf((lists.Masters ?? MasterLists.None).EmployeeRelations, u.EmpRelation), Renews = renews,
            ExistingRenewalFor = renews is null ? null : PrincipalOrFull(deposit.RenewalFor),
            Individual, Hng = FormFiled(deposit.NoTds), at.Folio,
            line?.Rate, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);
    }

    // f_Renewal_For and f_ExistingFDRNoRenewalFor: P, the principal; F, principal and interest.
    private static string? PrincipalOrFull(string renewalFor)
    {
        if (renewalFor == RenewalChoice.Principal) return "P";
        if (renewalFor == RenewalChoice.PrincipalInterest) return "F";
        return null;
    }

    // f_TDS_Flag: Y, tax is deducted; N, the investor filed the form for none.
    private static string TdsFlag(bool noTds)
    {
        if (noTds) return "N";
        return "Y";
    }

    // f_HNG: 121 when the investor filed Form 121 for no tax to be deducted; empty otherwise.
    private static string? FormFiled(bool noTds)
    {
        if (noTds) return "121";
        return null;
    }

    // f_EmpHolder takes 10 characters, so the holder goes in as DMS codes it.
    private static string EmpHolderCode(string holder)
    {
        if (holder == "First holder") return HolderType.Investor;
        if (holder == "Second holder") return HolderType.Second;
        if (holder == "Third holder") return HolderType.Third;
        return holder;
    }
}
