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
        await RetireAsync(db, tx, at, FdConfigurationQueries.RetireInvestment);
        await db.ExecuteAsync(FdConfigurationQueries.InsertInvestment, new
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
