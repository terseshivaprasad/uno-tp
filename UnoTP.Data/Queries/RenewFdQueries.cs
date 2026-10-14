namespace UnoTP.Data;

/// <summary>
/// The SQL of Renew FD: the booked deposits a holder can renew, their joint holders,
/// and cancelling a renewal. Investor Identification lists the same deposits for a
/// holder it finds. Nothing but SQL is in this file.
/// </summary>
internal static class RenewFdQueries
{
    /// <summary>
    /// The booked deposits on a folio (@Folio): each a booked application and its deposit as submitted, with the
    /// application that renews it: the latest one open or submitted, and not cancelled.
    /// </summary>
    public const string BookedOnFolio = """
        SELECT m.f_Fdr_No AS Number, m.f_App_No AS AppNo, m.f_Folio AS Folio, m.f_Name AS Investor,
               i.f_Category AS Category, CAST(i.f_Amount AS BIGINT) AS Amount, i.f_Int_Rate AS Rate,
               TRY_CAST(i.f_Tenure AS INT) AS TenureMonths, i.f_Int_Freq AS InterestFreq,
               CAST(ISNULL(i.f_Is_Auto_Renewal, 0) AS BIT) AS AutoRenewal, m.f_Booked_On AS BookedOn,
               m.f_Quote_Maturity_Amount AS MaturityAmount, NULLIF(b.f_NEFTCode, N'') AS RepayIfsc,
               NULLIF(b.f_BankAccountNo, N'') AS RepayAccount, rn.f_App_No AS RenewalAppNo,
               rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn,
               rn.f_Accepted_On AS RenewalAcceptedOn, rn.AppType AS RenewalAppType,
               rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM dbo.t_Unotp_Application_Mst m
        JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl i ON i.f_Appl_No = m.f_App_No AND i.f_Active = 1
            AND i.f_Status IN ('APR', 'PEN_E')
        OUTER APPLY (
            SELECT TOP (1) r.f_NEFTCode, r.f_BankAccountNo FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl r
            WHERE r.f_Appl_No = m.f_App_No
              AND r.f_Active = 1 ORDER BY r.f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC) b
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver,
                r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No
                      AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = m.f_Fdr_No AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        WHERE m.f_Active = 1 AND m.f_Booked_On IS NOT NULL AND NULLIF(m.f_Fdr_No, '') IS NOT NULL
          AND m.f_Cancelled_On IS NULL
          AND m.f_Folio = @Folio
        """;

    /// <summary>
    /// The booked deposits of a holder, by PAN and date of birth (@Pan, @Dob): each a booked application and its deposit as submitted, with the
    /// application that renews it: the latest one open or submitted, and not cancelled.
    /// </summary>
    public const string BookedOfHolder = """
        SELECT m.f_Fdr_No AS Number, m.f_App_No AS AppNo, m.f_Folio AS Folio, m.f_Name AS Investor,
               i.f_Category AS Category, CAST(i.f_Amount AS BIGINT) AS Amount, i.f_Int_Rate AS Rate,
               TRY_CAST(i.f_Tenure AS INT) AS TenureMonths, i.f_Int_Freq AS InterestFreq,
               CAST(ISNULL(i.f_Is_Auto_Renewal, 0) AS BIT) AS AutoRenewal, m.f_Booked_On AS BookedOn,
               m.f_Quote_Maturity_Amount AS MaturityAmount, NULLIF(b.f_NEFTCode, N'') AS RepayIfsc,
               NULLIF(b.f_BankAccountNo, N'') AS RepayAccount, rn.f_App_No AS RenewalAppNo,
               rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn,
               rn.f_Accepted_On AS RenewalAcceptedOn, rn.AppType AS RenewalAppType,
               rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM dbo.t_Unotp_Application_Mst m
        JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl i ON i.f_Appl_No = m.f_App_No AND i.f_Active = 1
            AND i.f_Status IN ('APR', 'PEN_E')
        OUTER APPLY (
            SELECT TOP (1) r.f_NEFTCode, r.f_BankAccountNo FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl r
            WHERE r.f_Appl_No = m.f_App_No
              AND r.f_Active = 1 ORDER BY r.f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC) b
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver,
                r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No
                      AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = m.f_Fdr_No AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        WHERE m.f_Active = 1 AND m.f_Booked_On IS NOT NULL AND NULLIF(m.f_Fdr_No, '') IS NOT NULL
          AND m.f_Cancelled_On IS NULL
          AND m.f_Pan = @Pan AND m.f_Dob = @Dob
        """;

    /// <summary>
    /// The one booked deposit with the FDR number @Number: a booked application and its deposit as
    /// submitted, with the application that renews it: the latest one open or submitted, and not cancelled.
    /// </summary>
    public const string BookedByDepositNumber = """
        SELECT m.f_Fdr_No AS Number, m.f_App_No AS AppNo, m.f_Folio AS Folio, m.f_Name AS Investor,
               i.f_Category AS Category, CAST(i.f_Amount AS BIGINT) AS Amount, i.f_Int_Rate AS Rate,
               TRY_CAST(i.f_Tenure AS INT) AS TenureMonths, i.f_Int_Freq AS InterestFreq,
               CAST(ISNULL(i.f_Is_Auto_Renewal, 0) AS BIT) AS AutoRenewal, m.f_Booked_On AS BookedOn,
               m.f_Quote_Maturity_Amount AS MaturityAmount, NULLIF(b.f_NEFTCode, N'') AS RepayIfsc,
               NULLIF(b.f_BankAccountNo, N'') AS RepayAccount, rn.f_App_No AS RenewalAppNo,
               rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn,
               rn.f_Accepted_On AS RenewalAcceptedOn, rn.AppType AS RenewalAppType,
               rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM dbo.t_Unotp_Application_Mst m
        JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl i ON i.f_Appl_No = m.f_App_No AND i.f_Active = 1
            AND i.f_Status IN ('APR', 'PEN_E')
        OUTER APPLY (
            SELECT TOP (1) r.f_NEFTCode, r.f_BankAccountNo FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl r
            WHERE r.f_Appl_No = m.f_App_No
              AND r.f_Active = 1 ORDER BY r.f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC) b
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver,
                r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No
                      AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = m.f_Fdr_No AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        WHERE m.f_Active = 1 AND m.f_Booked_On IS NOT NULL AND NULLIF(m.f_Fdr_No, '') IS NOT NULL
          AND m.f_Cancelled_On IS NULL
          AND m.f_Fdr_No = @Number
        """;

    /// <summary>
    /// The joint holders on a set of applications: every holder but the first.
    /// </summary>
    public const string JointHoldersOf = """
        SELECT f_Appl_No, ISNULL(f_Kyc_PAN, N''), f_Kyc_DOB, ISNULL(f_Kyc_FullName, N''), ISNULL(f_FolioNo, N'')
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl
        WHERE f_Appl_No IN @AppNos AND f_Active = 1 AND f_Holder_Type <> @Investor
        ORDER BY f_Appl_No, f_Holder_Type
        """;

    /// <summary>
    /// A renewal cancelled: one not submitted yet, or a digital one the investor has not accepted.
    /// </summary>
    public const string CancelRenewal = """
        UPDATE m
        SET f_Cancelled_On = SYSDATETIME(), f_Sub_Status = 'cancelled', f_Updated_By = @Partner,
            f_Updated_On = SYSDATETIME()
        FROM dbo.t_Unotp_Application_Mst m
        WHERE m.f_Renew_Dep_No = @Number AND m.f_Partner_Id = @Partner AND m.f_Cancelled_On IS NULL
          AND m.f_Active = 1
          AND (m.f_Submitted_On IS NULL
               OR (m.f_Accepted_On IS NULL AND EXISTS (
                    SELECT 1 FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl i
                    WHERE i.f_Appl_No = m.f_App_No AND i.f_Active = 1
                      AND i.f_ApplicationDeclarationType = @Digital)))
        """;
}
