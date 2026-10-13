namespace UnoTP.Data;

/// <summary>
/// The SQL for the short links sent to investors: t_FD_PGW_PaymentUrlRequest_dtl for a
/// purchase and T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL for a renewal, and the lists
/// that run across them. Nothing but SQL is in this file. The row written is in
/// Models/LinkRequest.cs.
/// </summary>
internal static class LinkQueries
{
    /// <summary>The table a purchase's link is kept in.</summary>
    public const string PurchaseTable = "dbo.t_FD_PGW_PaymentUrlRequest_dtl";

    /// <summary>The table a renewal's link is kept in.</summary>
    public const string RenewalTable = "dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL";

    /// <summary>
    /// Both link tables as one, for a list that runs across purchases and renewals.
    /// </summary>
    public const string BothTables = $"""
        (SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation FROM {PurchaseTable}
         UNION ALL
         SELECT f_AppNo, f_Link_Expiry_Date, f_CreatedOn, f_UpdatedOn, f_Active,
                f_Disclaimer1, f_Disclaimer2, f_TermsCondition, f_KycConfirmation FROM {RenewalTable})
        """;

    /// <summary>
    /// When an application's investor accepted it, for a query over the application master as m;
    /// NULL until they have. Accepted is all four flags set on the application's live link row:
    /// both disclaimers, the terms and conditions and the KYC confirmation. The tables keep no
    /// date for it, so the date is when the row was last written.
    /// </summary>
    public const string AcceptedOn = $"""
        (SELECT TOP (1) CASE WHEN d.f_Disclaimer1 = 1 AND d.f_Disclaimer2 = 1 AND d.f_TermsCondition = 1 AND d.f_KycConfirmation = 1
                             THEN COALESCE(d.f_UpdatedOn, d.f_CreatedOn) END
         FROM {BothTables} d
         WHERE d.f_AppNo = m.f_App_No AND d.f_Active = 1
         ORDER BY d.f_CreatedOn DESC)
        """;

    /// <summary>
    /// A first link is about to be written: any row the application had goes out of use.
    /// </summary>
    public static string Retire(string table) =>
        $"""
        UPDATE {table}
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// A link sent for the first time: a new row, open for @ValidityHours from now.
    /// </summary>
    public static string Insert(string table) =>
        $"""
        INSERT {table} (f_AppNo, f_InvestorName, f_InvestmentAmount, f_Investor_Pan, F_ApplicationDate,
            f_ShotrURL, f_FullURL, f_Link_Validity, f_Link_Expiry_Date,
            f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @InvestorName, @InvestmentAmount, @InvestorPan, @ApplicationDate,
            @ShortUrl, @FullUrl, @ValidityHours, DATEADD(HOUR, @ValidityHours, GETDATE()),
            1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Regenerate: the application's live link row is given @Hours from now. No new row.
    /// </summary>
    public static string SendAgain(string table) =>
        $"""
        UPDATE {table}
        SET f_Link_Validity = @Hours, f_Link_Expiry_Date = DATEADD(HOUR, @Hours, GETDATE()),
            f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_AppNo = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// A submitted application as the registers see it: the application as submitted, where it
    /// has got to since, and the Axis CMS branch. Named after the properties of SubmittedRow.
    /// </summary>
    public const string SubmittedApplication = $"""
        SELECT m.f_App_No AS AppNo, COALESCE(NULLIF(k.f_Kyc_FullName, N''), m.f_Name) AS Name, m.f_Submitted_On AS SubmittedOn,
            {AcceptedOn} AS AcceptedOn, m.f_Paid_On AS PaidOn, m.f_Booked_On AS BookedOn, m.f_Cancelled_On AS CancelledOn,
            i.AppType, i.Amount, CAST(CASE WHEN m.f_Renew_Dep_No IS NULL THEN 0 ELSE 1 END AS BIT) AS Renewal,
            p.f_Payment_Mode AS PayMode, p.f_Cheque_DD_No AS ChequeNo, p.f_Drawn_Bank_Name AS BankName,
            a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email, ISNULL(p.f_CMS_Loc_Desc, N'') AS Branch, m.f_Pan AS Pan
        FROM dbo.t_Unotp_Application_Mst m
        {ApplicationTableQueries.CurrentDepositOf}
        LEFT JOIN dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Kyc_Data_Dtl k ON k.f_Appl_No = m.f_App_No AND k.f_Holder_Type = '01' AND k.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Address_Dtl a ON a.f_Appl_No = m.f_App_No AND a.f_Holder_Type = '01' AND a.f_AddType_Code = 'PER' AND a.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'APR' AND m.f_Active = 1
        """;

    /// <summary>
    /// The live link of each of the partner's submitted applications, the one sent last first.
    /// </summary>
    public const string SentLinks = $"""
        SELECT l.f_AppNo AS AppNo, COALESCE(NULLIF(k.f_Kyc_FullName, N''), m.f_Name) AS Name,
            p.f_Payment_Mode AS PayMode, i.AppType, a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email,
            COALESCE(l.f_UpdatedOn, l.f_CreatedOn, m.f_Submitted_On) AS SentOn,
            COALESCE(l.f_Link_Expiry_Date, l.f_CreatedOn, m.f_Submitted_On) AS ExpiresOn,
            m.f_Submitted_On AS SubmittedOn, {AcceptedOn} AS AcceptedOn, m.f_Paid_On AS PaidOn
        FROM {BothTables} l
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = l.f_AppNo
        {ApplicationTableQueries.CurrentDepositOf}
        LEFT JOIN dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Kyc_Data_Dtl k ON k.f_Appl_No = m.f_App_No AND k.f_Holder_Type = '01' AND k.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Address_Dtl a ON a.f_Appl_No = m.f_App_No AND a.f_Holder_Type = '01' AND a.f_AddType_Code = 'PER' AND a.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1 AND m.f_Submitted_On IS NOT NULL
          AND l.f_Active = 1
          AND l.f_CreatedOn = (SELECT MAX(latest.f_CreatedOn) FROM {BothTables} latest
                               WHERE latest.f_AppNo = l.f_AppNo AND latest.f_Active = 1)
        ORDER BY SentOn DESC
        """;

    /// <summary>
    /// The partner's submitted applications, the one submitted last first: all of them, or the
    /// one @AppNo names.
    /// </summary>
    public static string SubmittedApplications(bool oneApplication) =>
        SubmittedApplication + (oneApplication ? " AND m.f_App_No = @AppNo" : "") + " ORDER BY m.f_Submitted_On DESC";
}
