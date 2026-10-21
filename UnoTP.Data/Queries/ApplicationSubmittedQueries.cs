namespace UnoTP.Data;

/// <summary>
/// The SQL of Application Submitted's Regenerate: the same link is sent again, and
/// its validity runs from now, on the application and on its link row. Short URL
/// runs these too. Nothing but SQL is in this file.
///
/// Every query a send runs is this page's own: the other pages that send a link keep
/// their own copies (ReviewSummaryQueries, ApplicationSubmittedQueries, ShortUrlQueries),
/// and a change to one is made in each.
///
/// The messages go through the communication table (t_FD_PGW_Comm_Hdr), which is what
/// sends them: a row in it is an SMS or an e-mail the FD system's sender picks up. The
/// row is copied from the application's live link row, as the FD system's own
/// procedure copies it, so with no live link no row is written; the message's text is
/// the sender's, by the event type. One column is not copied: f_CreatedOn is when the
/// message was written (the procedure copies the link row's date), as the limits
/// (CommLimits) count by it.
/// </summary>
internal static class ApplicationSubmittedQueries
{
    /// <summary>
    /// A link sent again: its validity runs from now. Only for an application that already
    /// has a link, is unpaid (no row with status Y in the payment log), not cancelled (neither by the app nor by the FD system: no active row in
    /// t_FD_BT_Cancelled_Application_Dtl) and
    /// inside its days. Gives the header back, by the
    /// same columns as ApplicationQueries.Header.
    /// </summary>
    public const string ResendLink = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), f_Updated_By = @Partner,
            f_Updated_On = SYSDATETIME()
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
          AND f_Link_Valid_Until IS NOT NULL
        """;

    /// <summary>
    /// Regenerate, for a purchase: the application's live link row is given @Hours from now. No new row.
    /// </summary>
    public const string SendPurchaseLinkAgain = """
        UPDATE MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
        SET f_Link_Validity = @Hours, f_Link_Expiry_Date = DATEADD(HOUR, @Hours, GETDATE()),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Regenerate, for a renewal: the application's live link row is given @Hours from now. No new row.
    /// </summary>
    public const string SendRenewalLinkAgain = """
        UPDATE MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL
        SET f_Link_Validity = @Hours, f_Link_Expiry_Date = DATEADD(HOUR, @Hours, GETDATE()),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
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
    /// A first link is about to be written for a purchase: any row the application had goes out of use, its link closed now (f_Link_Validity 0, f_Link_Expiry_Date now).
    /// </summary>
    public const string RetirePurchaseLink = """
        UPDATE MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl
        SET f_Active = 0, f_Link_Validity = 0, f_Link_Expiry_Date = GETDATE(),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// A first link is about to be written for a renewal: any row the application had goes out of use, its link closed now (f_Link_Validity 0, f_Link_Expiry_Date now).
    /// </summary>
    public const string RetireRenewalLink = """
        UPDATE MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL
        SET f_Active = 0, f_Link_Validity = 0, f_Link_Expiry_Date = GETDATE(),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// A purchase's link sent for the first time: a new row, open for @ValidityHours from now.
    /// Written as the FD system's own app writes it: the key (f_Key_GUID), the user as f_CreatedID and
    /// f_CreatedBy both, f_CreatedType empty, and f_SourceAPI UNO_TP_V2. TO CONFIRM: f_FormCode, which that app
    /// writes too (its value was not seen), is left empty.
    /// </summary>
    public const string InsertPurchaseLink = """
        INSERT MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl (f_AppNo, f_InvestorName, f_InvestmentAmount, f_Investor_Pan, F_ApplicationDate, f_ShotrURL, f_FullURL, f_Key_GUID, f_Link_Validity, f_Link_Expiry_Date, f_SourceAPI, f_CreatedID, f_CreatedType, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @InvestorName, @InvestmentAmount, @InvestorPan, @ApplicationDate, @ShortUrl, @FullUrl, @Key, @ValidityHours, DATEADD(HOUR, @ValidityHours, GETDATE()), 'UNO_TP_V2', @CreatedBy, '', 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// A renewal's link sent for the first time: a new row, open for @ValidityHours from now.
    /// </summary>
    public const string InsertRenewalLink = """
        INSERT MMFSL_APP_FDPGW.dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL (f_AppNo, f_InvestorName, f_InvestmentAmount, f_Investor_Pan, F_ApplicationDate, f_ShotrURL, f_FullURL, f_Key_GUID, f_Link_Validity, f_Link_Expiry_Date, f_SourceAPI, f_CreatedID, f_CreatedType, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @InvestorName, @InvestmentAmount, @InvestorPan, @ApplicationDate, @ShortUrl, @FullUrl, @Key, @ValidityHours, DATEADD(HOUR, @ValidityHours, GETDATE()), 'UNO_TP_V2', @CreatedBy, '', 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
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
    /// A renewal's link is sent: its row in the renewal transaction table says the renewal request is
    /// generated (F_IsRenewalReqGen), as the FD system's Renew FD list reads it.
    /// </summary>
    public const string MarkRenewalRequested = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_RenewalDtls
        SET F_IsRenewalReqGen = 1, UPDATED_DATE = GETDATE()
        WHERE APPLNO = @AppNo AND f_Active = 1
        """;
}
