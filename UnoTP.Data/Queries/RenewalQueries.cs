namespace UnoTP.Data;

/// <summary>
/// The SQL for Renew FD: the deposits booked through this app, and the filters they are
/// found by. Nothing but SQL is in this file.
/// </summary>
internal static class RenewalQueries
{
    /// <summary>
    /// A booked application and its deposit as submitted, with the application that renews it:
    /// the latest one open or submitted, and not cancelled. A filter is added after it.
    /// </summary>
    public const string Booked = """
        SELECT m.f_Fdr_No AS Number, m.f_App_No AS AppNo, m.f_Folio AS Folio, m.f_Name AS Investor,
               i.f_Category AS Category, CAST(i.f_Amount AS BIGINT) AS Amount, i.f_Int_Rate AS Rate, TRY_CAST(i.f_Tenure AS INT) AS TenureMonths,
               i.f_Int_Freq AS InterestFreq, CAST(ISNULL(i.f_Is_Auto_Renewal, 0) AS BIT) AS AutoRenewal,
               m.f_Booked_On AS BookedOn, m.f_Quote_Maturity_Amount AS MaturityAmount,
               NULLIF(b.f_NEFTCode, N'') AS RepayIfsc, NULLIF(b.f_BankAccountNo, N'') AS RepayAccount,
               rn.f_App_No AS RenewalAppNo, rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn, rn.f_Accepted_On AS RenewalAcceptedOn,
               rn.AppType AS RenewalAppType, rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM dbo.t_Unotp_Application_Mst m
        JOIN dbo.t_FD_BT_Investment_Dtl i ON i.f_Appl_No = m.f_App_No AND i.f_Active = 1 AND i.f_Status IN ('APR', 'PEN_E')
        OUTER APPLY (
            SELECT TOP (1) r.f_NEFTCode, r.f_BankAccountNo FROM dbo.t_FD_BT_Investor_Bank_Dtl r
            WHERE r.f_Appl_No = m.f_App_No AND r.f_Active = 1 ORDER BY r.f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC) b
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver, r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = m.f_Fdr_No AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        WHERE m.f_Active = 1 AND m.f_Booked_On IS NOT NULL AND NULLIF(m.f_Fdr_No, '') IS NOT NULL AND m.f_Cancelled_On IS NULL
        """;

    /// <summary>The booked deposits a filter finds. filter is one of the three below.</summary>
    public static string BookedWhere(string filter) =>
        $"{Booked} AND {filter}";

    /// <summary>The filter for the deposits on a folio.</summary>
    public const string OnFolio = "m.f_Folio = @Folio";

    /// <summary>The filter for the deposits of a holder, by PAN and date of birth.</summary>
    public const string OfHolder = "m.f_Pan = @Pan AND m.f_Dob = @Dob";

    /// <summary>The filter for one deposit, by its FDR number.</summary>
    public const string ByDepositNumber = "m.f_Fdr_No = @Number";
}
