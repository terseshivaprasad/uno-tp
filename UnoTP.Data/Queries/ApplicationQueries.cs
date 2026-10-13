namespace UnoTP.Data;

/// <summary>
/// The SQL for the app's own application tables: t_Unotp_Application_Mst (the header),
/// t_Unotp_Upload_State and t_Unotp_Page_State. Nothing but SQL is in this file: each
/// statement is a constant, used by name from SqlApplications.cs. To change what is read
/// or written, change it here.
/// </summary>
internal static class ApplicationQueries
{
    /// <summary>
    /// The investor's name as far as the application knows it: as NSDL verified it, else as read
    /// off the PAN copy (or typed over it), else as the folio has it. Empty for an investor with
    /// no folio whose PAN copy is not read yet. For a query with the header as m and the upload state as u.
    /// </summary>
    public const string KnownName = """
        COALESCE(NULLIF(JSON_VALUE(u.f_Upload, '$.name'), ''), NULLIF(JSON_VALUE(u.f_Upload, '$.nsdlName'), ''), m.f_Name)
        """;

    /// <summary>
    /// The columns an application's header is read by, each named after a property of HeaderRow
    /// (ApplicationTableRows.cs). A column added here needs its property there.
    /// </summary>
    public const string HeaderColumns = """
        f_App_No AS AppNo, f_Status AS Status, f_Version AS Version,
        f_Upload_Ver AS UploadVer, f_Details_Ver AS DetailsVer, f_Payment_Ver AS PaymentVer, f_Deposit_Ver AS DepositVer,
        f_Pan AS Pan, f_Dob AS Dob, f_Name AS Name, f_Folio AS Folio, f_Gender AS Gender, f_Address AS Address, f_Data_Source AS DataSource,
        f_Pan_Filed AS PanFiled, f_Rec_Pan AS RecPan, f_Rec_Photo AS RecPhoto, f_Rec_Poa AS RecPoa,
        f_Renew_Dep_No AS RenewDepNo, f_Renew_Amount AS RenewAmount, f_Renew_Matures_On AS RenewMaturesOn,
        f_Renew_Rate AS RenewRate, f_Renew_Tenure AS RenewTenure, f_Renew_Payout AS RenewPayout, f_Renew_Principal AS RenewPrincipal,
        f_Submitted_On AS SubmittedOn, f_Sub_Status AS SubStatus, f_Link_Sent_To AS LinkSentTo,
        f_Link_Emailed_To AS LinkEmailedTo, f_Link_Valid_Until AS LinkValidUntil, f_Resends_Left AS ResendsLeft,
        f_Short_Url AS ShortUrl, f_Created_On AS CreatedOn, f_Cancelled_On AS CancelledOn
        """;

    /// <summary>
    /// The same columns as an UPDATE gives them back (OUTPUT inserted.f_App_No AS AppNo, ...):
    /// made from HeaderColumns, so the two never differ.
    /// </summary>
    public static readonly string InsertedHeaderColumns =
        string.Join(", ", HeaderColumns.Split(',').Select(c => "inserted." + c.Trim()));

    /// <summary>
    /// The partner's own application's header. locked takes the row's lock, for a save that
    /// reads it and then writes it.
    /// </summary>
    public static string Header(bool locked) =>
        $"SELECT {HeaderColumns} FROM dbo.t_Unotp_Application_Mst {(locked ? "WITH (UPDLOCK, ROWLOCK)" : "")} " +
        "WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1";

    /// <summary>
    /// The next application number, asked of the FD system's own procedure, which numbers a
    /// purchase and a renewal alike. The number comes back in @NewApplicationNo.
    /// </summary>
    public const string NextApplicationNo = """
        EXEC dbo.USP_FD_BTP_GetApplicationNo
            @f_BatchId = 1,
            @f_Entity_Id = @CreatedBy,
            @f_Agency_Cd = @AgencyCode,
            @f_Entity_Type = '',
            @f_BranchCode = '',
            @f_BTP_Trans_ref_No = NULL,
            @BusType = @BusType,
            @CreatedIP = @CreatedIP,
            @CreatedUserId = @CreatedBy,
            @CreatedUserName = @CreatedByUName,
            @SessionId = @SessionID,
            @FormCode = 'Search',
            @DSource = @DSource,
            @App_No = @NewApplicationNo OUTPUT
        """;

    /// <summary>
    /// A new application's header row.
    /// </summary>
    public const string InsertApplication = """
        INSERT dbo.t_Unotp_Application_Mst (f_App_No, f_Partner_Id, f_Status, f_Version,
            f_Upload_Ver, f_Payment_Ver, f_Deposit_Ver,
            f_Pan, f_Dob, f_Name, f_Folio, f_Gender, f_Address, f_Data_Source, f_Pan_Filed, f_Rec_Pan, f_Rec_Photo, f_Rec_Poa,
            f_Renew_Dep_No, f_Renew_Amount, f_Renew_Matures_On, f_Renew_Rate, f_Renew_Tenure, f_Renew_Payout, f_Renew_Principal, f_Created_By)
        VALUES (@AppNo, @Partner, 'PEN', @Version,
            @UploadVer, @PaymentVer, @DepositVer,
            @Pan, @Dob, @Name, @Folio, @Gender, @Address, @Source, @PanFiled, @RecPan, @RecPhoto, @RecPoa,
            @RenewDepNo, @RenewAmount, @RenewMaturesOn, @RenewRate, @RenewTenure, @RenewPayout, @RenewPrincipal, @Partner)
        """;

    /// <summary>
    /// A step saved: the application's version goes up, and the step's own version column with it.
    /// sectionColumn is that column: f_Upload_Ver, f_Details_Ver, f_Payment_Ver or f_Deposit_Ver.
    /// </summary>
    public static string NextVersion(string sectionColumn) =>
        $"UPDATE dbo.t_Unotp_Application_Mst SET f_Version = @Next, {sectionColumn} = @Next, f_Updated_By = @Partner, f_Updated_On = SYSDATETIME() WHERE f_App_No = @AppNo";

    /// <summary>
    /// The upload step's state, as it was saved at one version of the application.
    /// </summary>
    public const string UploadState = "SELECT f_Upload FROM dbo.t_Unotp_Upload_State WHERE f_App_No = @AppNo AND f_App_Version = @UploadVer AND f_Active = 1";

    /// <summary>
    /// The upload step's state, saved at a version of the application.
    /// </summary>
    public const string InsertUploadState = """
        INSERT dbo.t_Unotp_Upload_State (f_App_No, f_App_Version, f_Status, f_Upload, f_Created_By)
        VALUES (@AppNo, @Version, @Status, @Json, @By)
        """;

    /// <summary>
    /// What a page has typed on it and not yet saved as a step: kept by application and page,
    /// for the partner's own application only.
    /// </summary>
    public const string SavePage = """
        MERGE dbo.t_Unotp_Page_State WITH (HOLDLOCK) AS t
        USING (SELECT f_App_No FROM dbo.t_Unotp_Application_Mst WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1) AS a
            ON t.f_App_No = a.f_App_No AND t.f_Page = @Page
        WHEN MATCHED THEN UPDATE SET f_State = @State, f_Updated_On = SYSDATETIME()
        WHEN NOT MATCHED THEN INSERT (f_App_No, f_Page, f_State) VALUES (a.f_App_No, @Page, @State);
        """;

    /// <summary>
    /// The application submitted: its status becomes APR, every step takes the new version,
    /// and the quote is locked on it. Gives the header back as it now stands.
    /// </summary>
    public static readonly string Submit = $"""
        UPDATE dbo.t_Unotp_Application_Mst SET f_Status = 'APR', f_Version = @Next,
            f_Upload_Ver = CASE WHEN f_Upload_Ver IS NULL THEN NULL ELSE @Next END,
            f_Details_Ver = CASE WHEN f_Details_Ver IS NULL THEN NULL ELSE @Next END,
            f_Payment_Ver = CASE WHEN f_Payment_Ver IS NULL THEN NULL ELSE @Next END,
            f_Deposit_Ver = CASE WHEN f_Deposit_Ver IS NULL THEN NULL ELSE @Next END,
            f_Quote_Interest_Each = @InterestEach, f_Quote_Maturity_Amount = @MaturityAmount,
            f_Quote_Matures_On = @MaturesOn, f_Quote_Rate_As_On = @RateAsOn,
            f_Submitted_On = SYSDATETIME(), f_Sub_Status = 'payment-pending', f_Resends_Left = @Resends,
            f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        OUTPUT {InsertedHeaderColumns}
        WHERE f_App_No = @AppNo
        """;

    /// <summary>
    /// Where a submitted application's link went and until when it is open. Only while the
    /// application is unpaid, not cancelled and inside its days. Gives the header back.
    /// </summary>
    public static readonly string RecordLink = $"""
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Link_Sent_To = @LinkSentTo, f_Link_Emailed_To = @LinkEmailedTo,
            f_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), f_Short_Url = @ShortUrl,
            f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        OUTPUT {InsertedHeaderColumns}
        WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Status = 'APR' AND f_Active = 1
          AND f_Paid_On IS NULL AND f_Cancelled_On IS NULL AND f_Created_On >= DATEADD(DAY, -@Days, SYSDATETIME())
        """;

    /// <summary>
    /// A link sent again: its validity runs from now. Only for an application that already
    /// has a link, is unpaid, not cancelled and inside its days. Gives the header back.
    /// </summary>
    public static readonly string ResendLink = $"""
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Link_Valid_Until = DATEADD(HOUR, @Hours, SYSDATETIME()), f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        OUTPUT {InsertedHeaderColumns}
        WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Status = 'APR' AND f_Active = 1
          AND f_Paid_On IS NULL AND f_Cancelled_On IS NULL AND f_Created_On >= DATEADD(DAY, -@Days, SYSDATETIME())
          AND f_Link_Valid_Until IS NOT NULL
        """;

    /// <summary>
    /// A draft cancelled by its partner.
    /// </summary>
    public const string CancelDraft = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Cancelled_On = SYSDATETIME(), f_Sub_Status = 'cancelled', f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1
          AND f_Status = 'PEN' AND f_Submitted_On IS NULL AND f_Cancelled_On IS NULL
        """;

    /// <summary>
    /// A renewal cancelled: one not submitted yet, or a digital one the investor has not accepted.
    /// </summary>
    public const string CancelRenewal = """
        UPDATE m
        SET f_Cancelled_On = SYSDATETIME(), f_Sub_Status = 'cancelled', f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        FROM dbo.t_Unotp_Application_Mst m
        WHERE m.f_Renew_Dep_No = @Number AND m.f_Partner_Id = @Partner AND m.f_Cancelled_On IS NULL AND m.f_Active = 1
          AND (m.f_Submitted_On IS NULL
               OR (m.f_Accepted_On IS NULL AND EXISTS (
                    SELECT 1 FROM dbo.t_FD_BT_Investment_Dtl i
                    WHERE i.f_Appl_No = m.f_App_No AND i.f_Active = 1 AND i.f_ApplicationDeclarationType = @Digital)))
        """;

    /// <summary>
    /// The partner's applications opened and not submitted, the one touched last first.
    /// </summary>
    public const string Drafts = $"""
        SELECT m.f_App_No AS AppNo, {KnownName} AS Name, m.f_Pan AS Pan, m.f_Dob AS Dob, ISNULL(i.Amount, 0) AS Amount,
            m.f_Upload_Ver AS UploadVer, m.f_Details_Ver AS DetailsVer, m.f_Payment_Ver AS PaymentVer, m.f_Deposit_Ver AS DepositVer,
            m.f_Renew_Dep_No AS Renews,
            -- On the database's clock, where the time was written; turned into the app's below.
            DATEDIFF(MINUTE, COALESCE(m.f_Updated_On, m.f_Created_On), SYSDATETIME()) AS MinutesAgo
        FROM dbo.t_Unotp_Application_Mst m
        {ApplicationTableQueries.CurrentDepositOf}
        LEFT JOIN dbo.t_Unotp_Upload_State u ON u.f_App_No = m.f_App_No AND u.f_App_Version = m.f_Upload_Ver AND u.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'PEN' AND m.f_Active = 1 AND m.f_Cancelled_On IS NULL
          AND COALESCE(m.f_Updated_On, m.f_Created_On) > DATEADD(DAY, -@Days, SYSDATETIME())
        ORDER BY COALESCE(m.f_Updated_On, m.f_Created_On) DESC
        """;

    /// <summary>
    /// Every application of the partner, with where each has got to, for View Application.
    /// </summary>
    public const string Applications = $"""
        SELECT m.f_App_No AS AppNo, m.f_Status AS Status, m.f_Folio AS Folio, {KnownName} AS Name, m.f_Pan AS Pan, m.f_Dob AS Dob,
            i.Amount, i.TenureMonths, i.Payout,
            JSON_VALUE(u.f_Upload, '$.appType') AS AppType, JSON_VALUE(u.f_Upload, '$.payMode') AS PayMode,
            ISNULL((SELECT COUNT(*) FROM OPENJSON(u.f_Upload, '$.joint')), 0) AS JointHolders,
            m.f_Upload_Ver AS UploadVer, m.f_Details_Ver AS DetailsVer, m.f_Payment_Ver AS PaymentVer, m.f_Deposit_Ver AS DepositVer,
            m.f_Created_On AS CreatedOn, m.f_Submitted_On AS SubmittedOn,
            {LinkQueries.AcceptedOn} AS AcceptedOn, m.f_Paid_On AS PaidOn, m.f_Booked_On AS BookedOn, m.f_Fdr_No AS FdrNo,
            m.f_Cancelled_On AS CancelledOn,
            (SELECT MIN(f.f_Created_On) FROM dbo.t_Unotp_Upload_State f WHERE f.f_App_No = m.f_App_No AND f.f_Active = 1) AS UploadedOn,
            (SELECT MIN(p.f_CreatedDate) FROM dbo.t_FD_BT_Payinslip_Dtl p WHERE p.f_Application_Number = m.f_App_No) AS SlipOn,
            (SELECT MIN(l.f_CreatedOn) FROM {LinkQueries.BothTables} l WHERE l.f_AppNo = m.f_App_No) AS LinkSentOn,
            m.f_Penny_Drop_On AS PennyDropOn, m.f_Penny_Drop_Status AS PennyDropStatus,
            m.f_Kyc_Verified_On AS KycVerifiedOn, m.f_Kyc_Status AS KycStatus
        FROM dbo.t_Unotp_Application_Mst m
        {ApplicationTableQueries.CurrentDepositOf}
        LEFT JOIN dbo.t_Unotp_Upload_State u ON u.f_App_No = m.f_App_No AND u.f_App_Version = m.f_Upload_Ver AND u.f_Active = 1
        WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1
        ORDER BY m.f_Created_On DESC
        """;

    /// <summary>
    /// Whether an application is the partner's own: 1 when it is.
    /// </summary>
    public const string IsPartnersApplication = "SELECT COUNT(*) FROM dbo.t_Unotp_Application_Mst WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1";
}
