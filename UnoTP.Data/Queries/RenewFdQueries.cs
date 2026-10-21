namespace UnoTP.Data;

/// <summary>
/// The SQL of Renew FD: the deposits a holder can renew, as the FD system's own
/// procedure for the Renew FD screen (USP_UNOTP_GET_APPL_LIST_FOR_RENEW) lists
/// them, their joint holders, and cancelling a renewal. Investor Identification lists
/// the same deposits for a holder it finds. Nothing but SQL is in this file.
///
/// The deposits come from the FD system's deposit register (FD.dbo.fxfdrexp) joined
/// to its deposit master (T_FD_DEP_MSTR_TEMP): live deposits (DEP_STATUS L) whose KYC
/// is verified, not referred for renewal, and with no renewal application already
/// on either renewal table (the FD system's t_FD_RenewalDtls, this app's
/// t_FD_BT_RenewalDtls). The window - how many days before maturity entry opens
/// (@MaxDays) and closes (@MinDays) - comes from t_Web_Config (RenewalWindow).
/// </summary>
internal static class RenewFdQueries
{
    /// <summary>
    /// The renewal window, as the FD system's web config holds it for a source (@Source: CPTP_UNI for a
    /// partner, BOTC_UNI for a branch user): MIN and MAX days before maturity, and CAN_MIN for cancelling.
    /// </summary>
    public const string RenewalWindow = """
        SELECT MAX(CASE WHEN f_WC_Key = 'MIN' THEN TRY_CAST(f_WC_Value AS INT) END) AS MinDays,
               MAX(CASE WHEN f_WC_Key = 'MAX' THEN TRY_CAST(f_WC_Value AS INT) END) AS MaxDays,
               MAX(CASE WHEN f_WC_Key = 'CAN_MIN' THEN TRY_CAST(f_WC_Value AS INT) END) AS CancelMinDays
        FROM MMFSL_APP_FD_Common.dbo.t_Web_Config WITH (NOLOCK)
        WHERE f_Active = 1 AND f_Source = @Source AND f_Config_Type = 'RENEWAL_PERIOD'
          AND f_WC_Key IN ('MIN', 'MAX', 'CAN_MIN')
        """;

    /// <summary>
    /// The deposits of a holder, by PAN and date of birth (@Pan, @Dob), that the Renew FD screen lists, as
    /// the FD system's procedure lists them on @Today with the window @MinDays..@MaxDays:
    /// - first part: a deposit not tagged for auto renewal (rep_type not N), maturing before @Today + @MaxDays.
    ///   Renewable unless a renewal request is already on FD_CLOSE_REN_REQ (REC_STATUS A or C, not yet or
    ///   already processed) or a loan or a freeze stands on it; RegisterStatus says which.
    ///   TO CONFIRM: lines 236-247 of the procedure, the rest of this part's WHERE, were not in the photos.
    /// - second part: a deposit tagged for auto renewal (rep_type N) with no request at all, maturing from
    ///   @Today + @MinDays up to @Today + @MaxDays: listed, never renewable here (RegisterStatus Auto-Renewal).
    /// With each, the application that renews it in this app: the latest one open or submitted, not cancelled.
    /// </summary>
    public const string DepositsOfHolder = """
        ;WITH Base AS (
            SELECT A.FOLIO, A.FDR_NO, A.NAME, A.princ_amt, A.period_mm, A.int_rate, A.matu_amt, A.BROK_CODE, A.SCH_FD_CUM,
                   A.BANK_ACNO, A.NEFT_CODE, ISNULL(D.rep_type, '') AS rep_type, D.LOAN_IND, D.DEP_FREEZE,
                   CONVERT(DATE, SUBSTRING(A.matu_date, 5, 4) + '-' + SUBSTRING(A.matu_date, 3, 2) + '-' + SUBSTRING(A.matu_date, 1, 2)) AS MaturityDate
            FROM FD.dbo.fxfdrexp A WITH (NOLOCK)
            INNER JOIN FD.dbo.T_FD_DEP_MSTR_TEMP D WITH (NOLOCK) ON A.FDR_NO = D.DEP_NO
            LEFT JOIN FD.dbo.t_FD_RenewalDtls B WITH (NOLOCK) ON A.FDR_NO = B.F_Existing_FDR_No AND B.STEPS_COMPLETED = 5
                AND ISNULL(B.F_IsRenewalReqGen, '0') = '1' AND B.IsRenewed = 0 AND B.f_Active = 1
            LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_RenewalDtls C WITH (NOLOCK) ON A.FDR_NO = C.F_Existing_FDR_No AND C.f_Active = 1
            WHERE A.DEP_STATUS = 'L' AND A.KYC_VERIFICATION = 'V' AND ISNULL(A.REF_RENW_FLG, '0') = '0'
              AND ISNULL(B.APPLNO, '0') = '0' AND ISNULL(C.APPLNO, '0') = '0'
              AND A.PAN1 = @Pan AND CAST(A.DOB AS DATE) = @Dob
        )
        SELECT d.Folio, d.Number, d.Investor, d.Amount, d.TenureMonths, d.Rate, d.MaturityAmount, d.BrokerCode,
               d.Cumulative, d.RepayAccount, d.RepayIfsc, d.AutoRenewal, d.MaturesOn, d.Renewable, d.RegisterStatus,
               rn.f_App_No AS RenewalAppNo, rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn,
               rn.f_Accepted_On AS RenewalAcceptedOn, rn.AppType AS RenewalAppType,
               rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM (
            SELECT DISTINCT B.FOLIO AS Folio, B.FDR_NO AS Number, B.NAME AS Investor, B.princ_amt AS Amount,
                   B.period_mm AS TenureMonths, B.int_rate AS Rate, B.matu_amt AS MaturityAmount, B.BROK_CODE AS BrokerCode,
                   B.SCH_FD_CUM AS Cumulative, B.BANK_ACNO AS RepayAccount, B.NEFT_CODE AS RepayIfsc,
                   CAST(0 AS BIT) AS AutoRenewal, B.MaturityDate AS MaturesOn,
                   CAST(CASE
                        WHEN EXISTS (SELECT 1 FROM FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK)
                                     WHERE R.DEP_NO = B.FDR_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
                                       AND R.REC_STATUS IN ('A', 'C') AND R.REQ_PROCESS_STATUS IN ('N', 'Y')) THEN 0
                        WHEN B.LOAN_IND = 'Y' OR B.DEP_FREEZE = 'Y' THEN 0
                        ELSE 1 END AS BIT) AS Renewable,
                   CASE
                        WHEN EXISTS (SELECT 1 FROM FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK)
                                     WHERE R.DEP_NO = B.FDR_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
                                       AND R.REC_STATUS = 'C' AND R.REQ_PROCESS_STATUS = 'Y') THEN 'Renewal request processed successfully'
                        WHEN EXISTS (SELECT 1 FROM FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK)
                                     WHERE R.DEP_NO = B.FDR_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
                                       AND R.REC_STATUS = 'A' AND R.REQ_PROCESS_STATUS = 'N') THEN 'Renewal request already generated'
                        WHEN B.LOAN_IND = 'Y' OR B.DEP_FREEZE = 'Y' THEN 'Please contact E-Sarathi Helpdesk'
                        ELSE 'Eligible for renewal' END AS RegisterStatus
            FROM Base B
            WHERE B.rep_type <> 'N'
              AND B.MaturityDate < DATEADD(DAY, @MaxDays, @Today)
            UNION
            SELECT DISTINCT B.FOLIO, B.FDR_NO, B.NAME, B.princ_amt, B.period_mm, B.int_rate, B.matu_amt, B.BROK_CODE,
                   B.SCH_FD_CUM, B.BANK_ACNO, B.NEFT_CODE, CAST(1 AS BIT), B.MaturityDate, CAST(0 AS BIT), 'Auto-Renewal'
            FROM Base B
            LEFT JOIN FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK) ON B.FDR_NO = R.DEP_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
            WHERE B.rep_type = 'N' AND R.DEP_NO IS NULL
              AND B.MaturityDate >= DATEADD(DAY, @MinDays, @Today)
              AND B.MaturityDate < DATEADD(DAY, @MaxDays, @Today)
        ) d
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver,
                   r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = d.Number AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        ORDER BY d.MaturesOn
        """;

    /// <summary>
    /// The one deposit with the number @Number, as DepositsOfHolder lists it on @Today with the window
    /// @MinDays..@MaxDays: read when a renewal is started on it. Nothing when it is not one the screen lists.
    /// </summary>
    public const string DepositByNumber = """
        ;WITH Base AS (
            SELECT A.FOLIO, A.FDR_NO, A.NAME, A.princ_amt, A.period_mm, A.int_rate, A.matu_amt, A.BROK_CODE, A.SCH_FD_CUM,
                   A.BANK_ACNO, A.NEFT_CODE, ISNULL(D.rep_type, '') AS rep_type, D.LOAN_IND, D.DEP_FREEZE,
                   CONVERT(DATE, SUBSTRING(A.matu_date, 5, 4) + '-' + SUBSTRING(A.matu_date, 3, 2) + '-' + SUBSTRING(A.matu_date, 1, 2)) AS MaturityDate
            FROM FD.dbo.fxfdrexp A WITH (NOLOCK)
            INNER JOIN FD.dbo.T_FD_DEP_MSTR_TEMP D WITH (NOLOCK) ON A.FDR_NO = D.DEP_NO
            LEFT JOIN FD.dbo.t_FD_RenewalDtls B WITH (NOLOCK) ON A.FDR_NO = B.F_Existing_FDR_No AND B.STEPS_COMPLETED = 5
                AND ISNULL(B.F_IsRenewalReqGen, '0') = '1' AND B.IsRenewed = 0 AND B.f_Active = 1
            LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_RenewalDtls C WITH (NOLOCK) ON A.FDR_NO = C.F_Existing_FDR_No AND C.f_Active = 1
            WHERE A.DEP_STATUS = 'L' AND A.KYC_VERIFICATION = 'V' AND ISNULL(A.REF_RENW_FLG, '0') = '0'
              AND ISNULL(B.APPLNO, '0') = '0' AND ISNULL(C.APPLNO, '0') = '0'
              AND A.FDR_NO = @Number
        )
        SELECT d.Folio, d.Number, d.Investor, d.Amount, d.TenureMonths, d.Rate, d.MaturityAmount, d.BrokerCode,
               d.Cumulative, d.RepayAccount, d.RepayIfsc, d.AutoRenewal, d.MaturesOn, d.Renewable, d.RegisterStatus,
               rn.f_App_No AS RenewalAppNo, rn.f_Partner_Id AS RenewalPartner, rn.f_Submitted_On AS RenewalSubmittedOn,
               rn.f_Accepted_On AS RenewalAcceptedOn, rn.AppType AS RenewalAppType,
               rn.f_Upload_Ver AS RenewalUploadVer, rn.f_Details_Ver AS RenewalDetailsVer,
               rn.f_Payment_Ver AS RenewalPaymentVer, rn.f_Deposit_Ver AS RenewalDepositVer
        FROM (
            SELECT DISTINCT B.FOLIO AS Folio, B.FDR_NO AS Number, B.NAME AS Investor, B.princ_amt AS Amount,
                   B.period_mm AS TenureMonths, B.int_rate AS Rate, B.matu_amt AS MaturityAmount, B.BROK_CODE AS BrokerCode,
                   B.SCH_FD_CUM AS Cumulative, B.BANK_ACNO AS RepayAccount, B.NEFT_CODE AS RepayIfsc,
                   CAST(0 AS BIT) AS AutoRenewal, B.MaturityDate AS MaturesOn,
                   CAST(CASE
                        WHEN EXISTS (SELECT 1 FROM FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK)
                                     WHERE R.DEP_NO = B.FDR_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
                                       AND R.REC_STATUS IN ('A', 'C') AND R.REQ_PROCESS_STATUS IN ('N', 'Y')) THEN 0
                        WHEN B.LOAN_IND = 'Y' OR B.DEP_FREEZE = 'Y' THEN 0
                        ELSE 1 END AS BIT) AS Renewable,
                   CASE
                        WHEN EXISTS (SELECT 1 FROM FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK)
                                     WHERE R.DEP_NO = B.FDR_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
                                       AND R.REC_STATUS = 'C' AND R.REQ_PROCESS_STATUS = 'Y') THEN 'Renewal request processed successfully'
                        WHEN EXISTS (SELECT 1 FROM FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK)
                                     WHERE R.DEP_NO = B.FDR_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
                                       AND R.REC_STATUS = 'A' AND R.REQ_PROCESS_STATUS = 'N') THEN 'Renewal request already generated'
                        WHEN B.LOAN_IND = 'Y' OR B.DEP_FREEZE = 'Y' THEN 'Please contact E-Sarathi Helpdesk'
                        ELSE 'Eligible for renewal' END AS RegisterStatus
            FROM Base B
            WHERE B.rep_type <> 'N'
              AND B.MaturityDate < DATEADD(DAY, @MaxDays, @Today)
            UNION
            SELECT DISTINCT B.FOLIO, B.FDR_NO, B.NAME, B.princ_amt, B.period_mm, B.int_rate, B.matu_amt, B.BROK_CODE,
                   B.SCH_FD_CUM, B.BANK_ACNO, B.NEFT_CODE, CAST(1 AS BIT), B.MaturityDate, CAST(0 AS BIT), 'Auto-Renewal'
            FROM Base B
            LEFT JOIN FD.dbo.FD_CLOSE_REN_REQ R WITH (NOLOCK) ON B.FDR_NO = R.DEP_NO AND ISNULL(R.REQ_TYPE, '') = 'N'
            WHERE B.rep_type = 'N' AND R.DEP_NO IS NULL
              AND B.MaturityDate >= DATEADD(DAY, @MinDays, @Today)
              AND B.MaturityDate < DATEADD(DAY, @MaxDays, @Today)
        ) d
        OUTER APPLY (
            SELECT TOP (1) r.f_App_No, r.f_Partner_Id, r.f_Submitted_On, r.f_Accepted_On, r.f_Upload_Ver,
                   r.f_Details_Ver, r.f_Payment_Ver, r.f_Deposit_Ver,
                   (SELECT TOP (1) x.f_ApplicationDeclarationType FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl x
                    WHERE x.f_Appl_No = r.f_App_No AND x.f_Active = 1 ORDER BY x.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) AS AppType
            FROM dbo.t_Unotp_Application_Mst r
            WHERE r.f_Renew_Dep_No = d.Number AND r.f_Active = 1 AND r.f_Cancelled_On IS NULL
            ORDER BY r.f_Created_On DESC) rn
        ORDER BY d.MaturesOn
        """;

    /// <summary>
    /// The joint holders on a set of folios (@Folios), from the FD system's folio holding table: every
    /// holder but the first (HOLD_TYPE 1), in holder order.
    /// </summary>
    public const string JointHoldersOnFolios = """
        SELECT FOLIO_NO AS Folio, ISNULL(PAN_NO, N'') AS Pan, f_DOB AS Dob, ISNULL(NAME, N'') AS Name
        FROM FD.dbo.t_Fd_Folio_holding WITH (NOLOCK)
        WHERE FOLIO_NO IN @Folios AND f_Active = 1 AND HOLD_TYPE <> '1'
        ORDER BY FOLIO_NO, HOLD_TYPE
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

    /// <summary>
    /// The renewal transaction rows of a deposit's cancelled renewals go out of use, so the deposit is
    /// listed for renewal again: the rows this app wrote for @Number whose application is cancelled.
    /// </summary>
    public const string RetireRenewalRows = """
        UPDATE t
        SET f_Active = 0, UPDATED_DATE = GETDATE()
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_RenewalDtls t
        WHERE t.F_Existing_FDR_No = @Number AND t.f_Active = 1 AND t.f_Source = 'UNO_TP_V2'
          AND t.APPLNO IN (SELECT m.f_App_No FROM dbo.t_Unotp_Application_Mst m
                           WHERE m.f_Renew_Dep_No = @Number AND m.f_Cancelled_On IS NOT NULL)
        """;
}
