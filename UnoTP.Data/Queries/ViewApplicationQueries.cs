namespace UnoTP.Data;

/// <summary>
/// The SQL of View Application: the partner's applications a search reaches, with where
/// each has got to. One application's details are read by ApplicationQueries.
/// Nothing but SQL is in this file.
///
/// An application is cancelled when the app cancelled it (f_Cancelled_On) or the FD system did: an
/// active row for it in t_FD_BT_Cancelled_Application_Dtl, dated by that row.
///
/// The list is never read whole: the one query takes the search as parameters, and
/// gives back no more than @Most rows.
/// </summary>
internal static class ViewApplicationQueries
{
    /// <summary>
    /// The partner's applications a search reaches, with where each has got to: the newest @Most of
    /// them, the newest first. One query for every search: the app gives the search's own values and
    /// leaves the others NULL, and a condition whose value is NULL lets every row through.
    /// - by dates: @From and @Until - raised from @From up to, and not on, @Until. An application is
    ///   raised the day it is submitted; one not yet submitted, the day it was opened.
    /// - by application number: @Number, a LIKE pattern - the number holds what was typed.
    /// - by folio: @Folio, a LIKE pattern - the folio number holds what was typed.
    /// OPTION (RECOMPILE) has SQL Server plan the read for the values this call gives, so a search by
    /// number is not run on a plan kept from a search by dates.
    /// </summary>
    public const string Applications = """
        SELECT TOP (@Most) m.f_App_No AS AppNo, m.f_Status AS Status, m.f_Folio AS Folio,
            COALESCE(NULLIF(JSON_VALUE(u.f_Upload, '$.name'), ''), NULLIF(JSON_VALUE(u.f_Upload, '$.nsdlName'), ''), m.f_Name) AS Name,
            m.f_Pan AS Pan, m.f_Dob AS Dob, i.Amount, i.TenureMonths, i.Payout,
            JSON_VALUE(u.f_Upload, '$.appType') AS AppType, JSON_VALUE(u.f_Upload, '$.payMode') AS PayMode,
            ISNULL((SELECT COUNT(*) FROM OPENJSON(u.f_Upload, '$.joint')), 0) AS JointHolders,
            m.f_Upload_Ver AS UploadVer, m.f_Details_Ver AS DetailsVer, m.f_Payment_Ver AS PaymentVer,
            m.f_Deposit_Ver AS DepositVer, m.f_Created_On AS CreatedOn, m.f_Submitted_On AS SubmittedOn,
            (SELECT MAX(COALESCE(d.f_UpdatedOn, d.f_CreatedOn))
             FROM (SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                          f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
                   FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
                   UNION ALL
                   SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                          f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
                   FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL) d
             WHERE d.f_AppNo = m.f_App_No
               AND d.f_Disclaimer1 = 1 AND d.f_Disclaimer2 = 1 AND d.f_TermsCondition = 1
               AND d.f_KycConfirmation = 1) AS AcceptedOn,
            m.f_Paid_On AS PaidOn, m.f_Booked_On AS BookedOn, m.f_Fdr_No AS FdrNo,
            COALESCE(m.f_Cancelled_On,
                     (SELECT MAX(COALESCE(c.f_Cancelled_Date, c.f_CreatedOn, GETDATE()))
                      FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Cancelled_Application_Dtl c WITH (NOLOCK)
                      WHERE c.f_Appl_No = m.f_App_No AND c.f_Active = 1)) AS CancelledOn,
            (SELECT MIN(f.f_Created_On) FROM dbo.t_Unotp_Upload_State f WHERE f.f_App_No = m.f_App_No AND f.f_Active = 1) AS UploadedOn,
            (SELECT MIN(p.f_CreatedDate) FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Dtl p WHERE p.f_Application_Number = m.f_App_No) AS SlipOn,
            (SELECT MIN(l.f_CreatedOn) FROM (SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                                                    f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
                                             FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
                                             UNION ALL
                                             SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                                                    f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
                                             FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL) l WHERE l.f_AppNo = m.f_App_No) AS LinkSentOn,
            m.f_Penny_Drop_On AS PennyDropOn, m.f_Penny_Drop_Status AS PennyDropStatus,
            m.f_Kyc_Verified_On AS KycVerifiedOn, m.f_Kyc_Status AS KycStatus
        FROM dbo.t_Unotp_Application_Mst m
        OUTER APPLY (
            SELECT TOP (1) CAST(d.f_Amount AS BIGINT) AS Amount, TRY_CAST(d.f_Tenure AS INT) AS TenureMonths,
                d.f_Int_Freq AS Payout, d.f_ApplicationDeclarationType AS AppType
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl d
            WHERE d.f_Appl_No = m.f_App_No AND d.f_Active = 1
            ORDER BY d.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) i
        LEFT JOIN dbo.t_Unotp_Upload_State u ON u.f_App_No = m.f_App_No AND u.f_App_Version = m.f_Upload_Ver
            AND u.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1
          AND (@From IS NULL OR COALESCE(m.f_Submitted_On, m.f_Created_On) >= @From)
          AND (@Until IS NULL OR COALESCE(m.f_Submitted_On, m.f_Created_On) < @Until)
          AND (@Number IS NULL OR m.f_App_No LIKE @Number)
          AND (@Folio IS NULL OR m.f_Folio LIKE @Folio)
        ORDER BY m.f_Created_On DESC
        OPTION (RECOMPILE)
        """;
}
