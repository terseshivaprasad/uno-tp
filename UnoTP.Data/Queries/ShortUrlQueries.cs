namespace UnoTP.Data;

/// <summary>
/// The SQL of Short URL: the partner's submitted applications a link can go to, the
/// live link of each, and everything Send link, Resend and New link write - the link
/// row, the same link again, the count behind the limits, and the messages that carry
/// the link. This page's queries are its own, shared with no other page: Review
/// Summary and Application Submitted keep their own copies of the link queries, and a
/// change to one is made in the other too. Nothing but SQL is in this file.
///
/// The messages go through the communication table (t_FD_PGW_Comm_Hdr), which is what
/// sends them: a row in it is an SMS or an e-mail the FD system's sender picks up. The
/// row is copied from the application's live link row, as the FD system's own
/// procedure copies it, so with no live link no row is written; the message's text is
/// the sender's, by the event type. One column is not copied: f_CreatedOn is when the
/// message was written (the procedure copies the link row's date), as the limits
/// (CommLimits) count by it.
///
/// Both queries take the same two filters, and the app gives one of them: @From, the
/// first day listed (an application submitted before it has stood unpaid for longer
/// than cancellationDays, and can no longer carry a link); or @AppNo, the one
/// application. A filter given as NULL lets every row through, and OPTION (RECOMPILE)
/// has SQL Server plan the read for the values this call gives.
///
/// Which link an application needs is read off its payment mode and its rows:
/// - paid online through the gateway (the Online mode): a link to pay, until the payment
///   log says it is paid;
/// - paid offline - a cheque, RTGS - or a renewal: a link to accept it, until it is
///   accepted. A physical one has a signed form, so it is accepted already - unless its
///   rows are PEN_E (the investor's KYC was fetched from CKYC), when the link asks them to
///   confirm that KYC;
/// - a physical application paid offline, otherwise: no link at all, and it is in neither
///   list (@Physical is the application type; @OfflineModes the modes not paid through the
///   gateway).
/// An application is cancelled when the app cancelled it (f_Cancelled_On: a cancelled renewal) or
/// the FD system did: an active row for it in t_FD_BT_Cancelled_Application_Dtl, as the FD
/// system's own procedure reads it. No link goes to a cancelled application.
/// An application paid online is paid once the payment log (t_FD_PGW_Payment_Log) holds a
/// row for it with status Y: no link goes to it then, and its link is done. The app's
/// own f_Paid_On is not read for this.
/// An application is accepted when any of its link rows has all four flags set (both
/// disclaimers, the terms and the KYC confirmation); AcceptedOn is when that row was
/// last written. The status asked for is the app's own (t_Unotp_Application_Mst), APR
/// for every submitted application; the FD tables are read by f_Active alone, so PEN_E
/// rows are read too.
///
/// Every query a send runs is this page's own: the other pages that send a link keep
/// their own copies (ReviewSummaryQueries, ApplicationSubmittedQueries, ShortUrlQueries),
/// and a change to one is made in each.
/// </summary>
internal static class ShortUrlQueries
{
    /// <summary>
    /// The submitted applications a link can go to, the one submitted last first: the application as
    /// submitted, where it has got to since, and the Axis CMS branch. Named after the properties of
    /// SubmittedRow (SqlPayInSlipsAndLinks.cs).
    /// </summary>
    public const string SubmittedApplications = """
        SELECT m.f_App_No AS AppNo, COALESCE(NULLIF(k.f_Kyc_FullName, N''), m.f_Name) AS Name,
               m.f_Submitted_On AS SubmittedOn,
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
            m.f_Booked_On AS BookedOn,
            CAST(CASE WHEN m.f_Cancelled_On IS NOT NULL
                        OR EXISTS (SELECT 1 FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Cancelled_Application_Dtl c WITH (NOLOCK)
                                   WHERE c.f_Appl_No = m.f_App_No AND c.f_Active = 1) THEN 1 ELSE 0 END AS BIT) AS Cancelled,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_Payment_Log pl WITH (NOLOCK)
                                   WHERE pl.f_Appl_No = m.f_App_No AND pl.f_Status = 'Y') THEN 1 ELSE 0 END AS BIT) AS Paid,
            i.AppType, i.Status, i.Amount,
            CAST(CASE WHEN m.f_Renew_Dep_No IS NULL THEN 0 ELSE 1 END AS BIT) AS Renewal,
            p.f_Payment_Mode AS PayMode, p.f_Cheque_DD_No AS ChequeNo, p.f_Drawn_Bank_Name AS BankName,
            a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email, ISNULL(p.f_CMS_Loc_Desc, N'') AS Branch,
            m.f_Pan AS Pan
        FROM dbo.t_Unotp_Application_Mst m
        OUTER APPLY (
            SELECT TOP (1) CAST(d.f_Amount AS BIGINT) AS Amount, TRY_CAST(d.f_Tenure AS INT) AS TenureMonths,
                d.f_Int_Freq AS Payout, d.f_ApplicationDeclarationType AS AppType, d.f_Status AS Status
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl d
            WHERE d.f_Appl_No = m.f_App_No AND d.f_Active = 1
            ORDER BY d.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) i
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl k ON k.f_Appl_No = m.f_App_No
            AND k.f_Holder_Type = '01' AND k.f_Active = 1
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl a ON a.f_Appl_No = m.f_App_No
            AND a.f_Holder_Type = '01' AND a.f_AddType_Code = 'PER' AND a.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'APR' AND m.f_Active = 1

            AND NOT (ISNULL(i.AppType, N'') = @Physical AND (ISNULL(p.f_Payment_Mode, N'') IN @OfflineModes OR m.f_Renew_Dep_No IS NOT NULL) AND ISNULL(i.Status, N'') <> 'PEN_E')
          AND (@From IS NULL OR m.f_Submitted_On >= @From)
          AND (@AppNo IS NULL OR m.f_App_No = @AppNo)
        ORDER BY m.f_Submitted_On DESC
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The live link of each submitted application that has one and is not cancelled - its active row in the purchase or
    /// the renewal link table - the one sent last first.
    /// </summary>
    public const string SentLinks = """
        SELECT l.f_AppNo AS AppNo, COALESCE(NULLIF(k.f_Kyc_FullName, N''), m.f_Name) AS Name,
               p.f_Payment_Mode AS PayMode, i.AppType, i.Status, a.f_MobileNumber AS Mobile,
               CAST(CASE WHEN m.f_Renew_Dep_No IS NULL THEN 0 ELSE 1 END AS BIT) AS Renewal,
               a.f_EmailAdd AS Email, COALESCE(l.f_UpdatedOn, l.f_CreatedOn, m.f_Submitted_On) AS SentOn,
               COALESCE(l.f_Link_Expiry_Date, l.f_CreatedOn, m.f_Submitted_On) AS ExpiresOn,
               m.f_Submitted_On AS SubmittedOn, (SELECT MAX(COALESCE(d.f_UpdatedOn, d.f_CreatedOn))
                                              FROM (SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                                                           f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
                                                    FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
                                                    UNION ALL
                                                    SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                                                           f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
                                                    FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL) d
                                              WHERE d.f_AppNo = m.f_App_No
                                                AND d.f_Disclaimer1 = 1 AND d.f_Disclaimer2 = 1
                                                  AND d.f_TermsCondition = 1
                                                  AND d.f_KycConfirmation = 1) AS AcceptedOn,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_Payment_Log pl WITH (NOLOCK)
                                   WHERE pl.f_Appl_No = m.f_App_No AND pl.f_Status = 'Y') THEN 1 ELSE 0 END AS BIT) AS Paid
        FROM (SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                     f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
              FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
              UNION ALL
              SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                     f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation
              FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL) l
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = l.f_AppNo
        OUTER APPLY (
            SELECT TOP (1) CAST(d.f_Amount AS BIGINT) AS Amount, TRY_CAST(d.f_Tenure AS INT) AS TenureMonths,
                d.f_Int_Freq AS Payout, d.f_ApplicationDeclarationType AS AppType, d.f_Status AS Status
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl d
            WHERE d.f_Appl_No = m.f_App_No AND d.f_Active = 1
            ORDER BY d.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) i
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl k ON k.f_Appl_No = m.f_App_No
            AND k.f_Holder_Type = '01' AND k.f_Active = 1
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl a ON a.f_Appl_No = m.f_App_No
            AND a.f_Holder_Type = '01' AND a.f_AddType_Code = 'PER' AND a.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1 AND m.f_Submitted_On IS NOT NULL
          AND m.f_Cancelled_On IS NULL
          AND NOT EXISTS (SELECT 1 FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Cancelled_Application_Dtl c WITH (NOLOCK)
                          WHERE c.f_Appl_No = m.f_App_No AND c.f_Active = 1)

            AND NOT (ISNULL(i.AppType, N'') = @Physical AND (ISNULL(p.f_Payment_Mode, N'') IN @OfflineModes OR m.f_Renew_Dep_No IS NOT NULL) AND ISNULL(i.Status, N'') <> 'PEN_E')
          AND l.f_Active = 1

            AND l.f_CreatedOn = (SELECT MAX(latest.f_CreatedOn) FROM (SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active, f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
                                                                    UNION ALL
                                                                    SELECT f_AppNo, f_Link_Expiry_Date,
                                                                           f_CreatedOn, f_UpdatedOn, f_Active,
                                                                           f_Disclaimer1, f_Disclaimer2,
                                                                           f_TermsCondition,
                                                                           f_KycConfirmation FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL) latest
                               WHERE latest.f_AppNo = l.f_AppNo AND latest.f_Active = 1)
          AND (@From IS NULL OR m.f_Submitted_On >= @From)
          AND (@AppNo IS NULL OR m.f_App_No = @AppNo)
        ORDER BY SentOn DESC
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// A new link is about to be written for a purchase: any row the application had goes out of use, its link closed now (f_Link_Validity 0, f_Link_Expiry_Date now).
    /// </summary>
    public const string RetirePurchaseLink = """
        UPDATE MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
        SET f_Active = 0, f_Link_Validity = 0, f_Link_Expiry_Date = GETDATE(),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// A new link is about to be written for a renewal: any row the application had goes out of use, its link closed now (f_Link_Validity 0, f_Link_Expiry_Date now).
    /// </summary>
    public const string RetireRenewalLink = """
        UPDATE MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL
        SET f_Active = 0, f_Link_Validity = 0, f_Link_Expiry_Date = GETDATE(),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// A purchase's link, sent for the first time or anew: a new row, open for @ValidityHours from now.
    /// Written as the FD system's own app writes it: the key (f_Key_GUID), the user as f_CreatedID and
    /// f_CreatedBy both, f_CreatedType empty, and f_SourceAPI UNO_TP. TO CONFIRM: f_FormCode, which that app
    /// writes too (its value was not seen), is left empty.
    /// </summary>
    public const string InsertPurchaseLink = """
        INSERT MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl (f_AppNo, f_InvestorName, f_InvestmentAmount, f_Investor_Pan, F_ApplicationDate, f_ShotrURL, f_FullURL, f_Key_GUID, f_Link_Validity, f_Link_Expiry_Date, f_SourceAPI, f_CreatedID, f_CreatedType, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @InvestorName, @InvestmentAmount, @InvestorPan, @ApplicationDate, @ShortUrl, @FullUrl, @Key, @ValidityHours, DATEADD(HOUR, @ValidityHours, GETDATE()), 'UNO_TP', @CreatedBy, '', 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// A renewal's link, sent for the first time or anew: a new row, open for @ValidityHours from now.
    /// </summary>
    public const string InsertRenewalLink = """
        INSERT MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL (f_AppNo, f_InvestorName, f_InvestmentAmount, f_Investor_Pan, F_ApplicationDate, f_ShotrURL, f_FullURL, f_Key_GUID, f_Link_Validity, f_Link_Expiry_Date, f_SourceAPI, f_CreatedID, f_CreatedType, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @InvestorName, @InvestmentAmount, @InvestorPan, @ApplicationDate, @ShortUrl, @FullUrl, @Key, @ValidityHours, DATEADD(HOUR, @ValidityHours, GETDATE()), 'UNO_TP', @CreatedBy, '', 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Resend, for a purchase: the application's live link row is given @Hours from now. No new row.
    /// </summary>
    public const string SendPurchaseLinkAgain = """
        UPDATE MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
        SET f_Link_Validity = @Hours, f_Link_Expiry_Date = DATEADD(HOUR, @Hours, GETDATE()),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Resend, for a renewal: the application's live link row is given @Hours from now. No new row.
    /// </summary>
    public const string SendRenewalLinkAgain = """
        UPDATE MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL
        SET f_Link_Validity = @Hours, f_Link_Expiry_Date = DATEADD(HOUR, @Hours, GETDATE()),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// How many messages went out lately, for the limits a send is held to before a row goes into the
    /// communication table: to this application (and how long ago the last one), to this mobile
    /// number and to this e-mail address, all within the last @Hours hours. Named after the properties
    /// of MessagesSentRow (LinkRequestTables.cs). @Mobile and @Email are NULL when the send has none.
    /// </summary>
    public const string MessagesSentLately = """
        SELECT ISNULL(SUM(CASE WHEN c.f_Appl_No = @AppNo THEN 1 ELSE 0 END), 0) AS ToApplication,
               DATEDIFF(SECOND, MAX(CASE WHEN c.f_Appl_No = @AppNo THEN c.f_CreatedOn END), GETDATE()) AS SecondsSinceLast,
               ISNULL(SUM(CASE WHEN c.f_CommType = 'SMS' AND c.f_EntityMobileNo = @Mobile THEN 1 ELSE 0 END), 0) AS ToMobile,
               ISNULL(SUM(CASE WHEN c.f_CommType = 'Email' AND c.f_EntityMailId = @Email THEN 1 ELSE 0 END), 0) AS ToEmail
        FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_Comm_Hdr c WITH (NOLOCK)
        WHERE c.f_CreatedOn > DATEADD(HOUR, -@Hours, GETDATE())
          AND (c.f_Appl_No = @AppNo OR c.f_EntityMobileNo = @Mobile OR c.f_EntityMailId = @Email)
        """;

    /// <summary>
    /// A purchase's link sent to the investor by SMS (@Mobile; @Email goes on the row too).
    /// </summary>
    public const string TellPurchaseBySms = """
        INSERT MMFSL_APP_FDPGW.dbo.t_FD_PGW_Comm_Hdr (f_RequestHdrSequence, f_CommType, f_CommUserType, f_CommEventType, f_CommLevel, f_CommReply, f_Appl_No, f_EntityMailId, f_EntityMobileNo, f_RequestFormCode, f_SourceAPI, f_CreatedID, f_CreatedType, f_CreatedIP, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_IsMobile, f_RequestUrl, f_FormCode)
        SELECT TOP (1) l.f_RequestHdrSequence, 'SMS', 'CREATEDBY', 'CPTP_SHORT_URL_SMS', 1, 'Information',
            l.f_AppNo, @Email, @Mobile, @RequestFormCode, l.f_SourceAPI, l.f_CreatedID, l.f_CreatedType,
            l.f_CreatedIP, l.f_CreatedBy, l.f_CreatedByUName, GETDATE(), l.f_IsMobile, l.f_RequestUrl,
            l.f_FormCode
        FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl l WITH (NOLOCK)
        WHERE l.f_AppNo = @AppNo AND l.f_Active = 1
        """;

    /// <summary>
    /// A purchase's link sent to the investor by e-mail (@Email; @Mobile goes on the row too).
    /// </summary>
    public const string TellPurchaseByEmail = """
        INSERT MMFSL_APP_FDPGW.dbo.t_FD_PGW_Comm_Hdr (f_RequestHdrSequence, f_CommType, f_CommUserType, f_CommEventType, f_CommLevel, f_CommReply, f_Appl_No, f_EntityMailId, f_EntityMobileNo, f_RequestFormCode, f_SourceAPI, f_CreatedID, f_CreatedType, f_CreatedIP, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_IsMobile, f_RequestUrl, f_FormCode)
        SELECT TOP (1) l.f_RequestHdrSequence, 'Email', 'CREATEDBY', 'CPTP_SHORT_URL_EMAIL', 1, 'Information',
            l.f_AppNo, @Email, @Mobile, @RequestFormCode, l.f_SourceAPI, l.f_CreatedID, l.f_CreatedType,
            l.f_CreatedIP, l.f_CreatedBy, l.f_CreatedByUName, GETDATE(), l.f_IsMobile, l.f_RequestUrl,
            l.f_FormCode
        FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl l WITH (NOLOCK)
        WHERE l.f_AppNo = @AppNo AND l.f_Active = 1
        """;

    /// <summary>
    /// A renewal's link sent to the investor by SMS. TO CONFIRM: the event type - the procedure's line
    /// naming it was not seen; it is written here after the e-mail's.
    /// </summary>
    public const string TellRenewalBySms = """
        INSERT MMFSL_APP_FDPGW.dbo.t_FD_PGW_Comm_Hdr (f_RequestHdrSequence, f_CommType, f_CommUserType, f_CommEventType, f_CommLevel, f_CommReply, f_Appl_No, f_EntityMailId, f_EntityMobileNo, f_RequestFormCode, f_SourceAPI, f_CreatedID, f_CreatedType, f_CreatedIP, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_IsMobile, f_RequestUrl, f_FormCode)
        SELECT TOP (1) l.f_RequestHdrSequence, 'SMS', 'CREATEDBY', 'FD_RENEWAL_REQUEST_URL_SMS', 1,
            'Information', l.f_AppNo, @Email, @Mobile, @RequestFormCode, l.f_SourceAPI, l.f_CreatedID,
            l.f_CreatedType, l.f_CreatedIP, l.f_CreatedBy, l.f_CreatedByUName, GETDATE(), l.f_IsMobile,
            l.f_RequestUrl, l.f_FormCode
        FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL l WITH (NOLOCK)
        WHERE l.f_AppNo = @AppNo AND l.f_Active = 1
        """;

    /// <summary>
    /// A renewal's link sent to the investor by e-mail.
    /// </summary>
    public const string TellRenewalByEmail = """
        INSERT MMFSL_APP_FDPGW.dbo.t_FD_PGW_Comm_Hdr (f_RequestHdrSequence, f_CommType, f_CommUserType, f_CommEventType, f_CommLevel, f_CommReply, f_Appl_No, f_EntityMailId, f_EntityMobileNo, f_RequestFormCode, f_SourceAPI, f_CreatedID, f_CreatedType, f_CreatedIP, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_IsMobile, f_RequestUrl, f_FormCode)
        SELECT TOP (1) l.f_RequestHdrSequence, 'Email', 'CREATEDBY', 'FD_RENEWAL_REQUEST_URL_EMAIL', 1,
            'Information', l.f_AppNo, @Email, @Mobile, @RequestFormCode, l.f_SourceAPI, l.f_CreatedID,
            l.f_CreatedType, l.f_CreatedIP, l.f_CreatedBy, l.f_CreatedByUName, GETDATE(), l.f_IsMobile,
            l.f_RequestUrl, l.f_FormCode
        FROM MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL l WITH (NOLOCK)
        WHERE l.f_AppNo = @AppNo AND l.f_Active = 1
        """;

    /// <summary>
    /// Where a submitted application's link went and until when it is open. Only while the
    /// application is unpaid, not cancelled (neither by the app nor by the FD system: no active row in
    /// t_FD_BT_Cancelled_Application_Dtl) and inside its days. Gives the header back.
    /// The header is given back by the same columns as ApplicationQueries.Header.
    /// </summary>
    public const string RecordLink = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Link_Sent_To = @LinkSentTo, f_Link_Emailed_To = @LinkEmailedTo,
            f_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), f_Short_Url = @ShortUrl,
            f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        OUTPUT inserted.f_App_No AS AppNo, inserted.f_Status AS Status, inserted.f_Version AS Version,
               inserted.f_Upload_Ver AS UploadVer, inserted.f_Details_Ver AS DetailsVer,
               inserted.f_Payment_Ver AS PaymentVer, inserted.f_Deposit_Ver AS DepositVer,
               inserted.f_Pan AS Pan, inserted.f_Dob AS Dob, inserted.f_Name AS Name, inserted.f_Folio AS Folio,
               inserted.f_Gender AS Gender, inserted.f_Address AS Address, inserted.f_Data_Source AS DataSource,
               inserted.f_Pan_Filed AS PanFiled, inserted.f_Rec_Pan AS RecPan, inserted.f_Rec_Photo AS RecPhoto,
               inserted.f_Rec_Poa AS RecPoa, inserted.f_Renew_Dep_No AS RenewDepNo,
               inserted.f_Renew_Amount AS RenewAmount, inserted.f_Renew_Matures_On AS RenewMaturesOn,
               inserted.f_Renew_Rate AS RenewRate, inserted.f_Renew_Tenure AS RenewTenure,
               inserted.f_Renew_Payout AS RenewPayout, inserted.f_Renew_Principal AS RenewPrincipal,
               inserted.f_Submitted_On AS SubmittedOn, inserted.f_Sub_Status AS SubStatus,
               inserted.f_Link_Sent_To AS LinkSentTo, inserted.f_Link_Emailed_To AS LinkEmailedTo,
               inserted.f_Link_Valid_Until AS LinkValidUntil, inserted.f_Resends_Left AS ResendsLeft,
               inserted.f_Short_Url AS ShortUrl, inserted.f_Created_On AS CreatedOn,
               inserted.f_Cancelled_On AS CancelledOn
        WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Status = 'APR' AND f_Active = 1
          AND NOT EXISTS (SELECT 1 FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_Payment_Log pl WITH (NOLOCK)
                          WHERE pl.f_Appl_No = f_App_No AND pl.f_Status = 'Y')
          AND f_Cancelled_On IS NULL
          AND NOT EXISTS (SELECT 1 FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Cancelled_Application_Dtl c WITH (NOLOCK)
                          WHERE c.f_Appl_No = f_App_No AND c.f_Active = 1)
            AND f_Created_On >= DATEADD(DAY, -@Days, SYSDATETIME())
        """;

    /// <summary>
    /// A renewal's link is sent: its row in the renewal transaction table says the renewal request is
    /// generated (F_IsRenewalReqGen), as the FD system's Renew FD list reads it.
    /// </summary>
    public const string MarkRenewalRequested = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_RenewalDtls
        SET F_IsRenewalReqGen = 1, UPDATED_DATE = GETDATE()
        WHERE APPLNO = @AppNo AND f_Active = 1
        """;
}
