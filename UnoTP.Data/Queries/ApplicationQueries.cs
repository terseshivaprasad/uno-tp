namespace UnoTP.Data;

/// <summary>
/// The SQL every step of an application runs, whichever page it is: finding the
/// partner's own application, opening one, moving its version on when a step is
/// saved, and reading everything saved on it in one go.
/// A page's own SQL is in the file named after the page. Nothing but SQL is in this file.
///
/// Every query in the Queries folder is one whole statement: none is built from
/// another constant or from a piece passed to it, so any of them can be changed on
/// its own. What several queries share is written out in each of them, and a change
/// to it is made in each (docs/CODE_GUIDE.md lists what is shared).
/// </summary>
internal static class ApplicationQueries
{
    /// <summary>
    /// The partner's own application's header.
    /// The columns are each named after a property of HeaderRow (ApplicationTableRows.cs): a column
    /// added here needs its property there, and is added to every query that gives the header back
    /// (Header, HeaderLocked, ReviewSummaryQueries.Submit and RecordLink, ApplicationSubmittedQueries.ResendLink).
    /// </summary>
    public const string Header = """
        SELECT f_App_No AS AppNo, f_Status AS Status, f_Version AS Version, f_Upload_Ver AS UploadVer,
               f_Details_Ver AS DetailsVer, f_Payment_Ver AS PaymentVer, f_Deposit_Ver AS DepositVer,
               f_Pan AS Pan, f_Dob AS Dob, f_Name AS Name, f_Folio AS Folio, f_Gender AS Gender,
               f_Address AS Address, f_Data_Source AS DataSource, f_Pan_Filed AS PanFiled, f_Rec_Pan AS RecPan,
               f_Rec_Photo AS RecPhoto, f_Rec_Poa AS RecPoa, f_Renew_Dep_No AS RenewDepNo,
               f_Renew_Amount AS RenewAmount, f_Renew_Matures_On AS RenewMaturesOn, f_Renew_Rate AS RenewRate,
               f_Renew_Tenure AS RenewTenure, f_Renew_Payout AS RenewPayout,
               f_Renew_Principal AS RenewPrincipal, f_Submitted_On AS SubmittedOn, f_Sub_Status AS SubStatus,
               f_Link_Sent_To AS LinkSentTo, f_Link_Emailed_To AS LinkEmailedTo,
               f_Link_Valid_Until AS LinkValidUntil, f_Resends_Left AS ResendsLeft, f_Short_Url AS ShortUrl,
               f_Created_On AS CreatedOn, f_Cancelled_On AS CancelledOn
        FROM dbo.t_Unotp_Application_Mst
        WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1
        """;

    /// <summary>
    /// The partner's own application's header, with the row's lock taken: for a save that reads it
    /// and then writes it. The same columns as Header.
    /// </summary>
    public const string HeaderLocked = """
        SELECT f_App_No AS AppNo, f_Status AS Status, f_Version AS Version, f_Upload_Ver AS UploadVer,
               f_Details_Ver AS DetailsVer, f_Payment_Ver AS PaymentVer, f_Deposit_Ver AS DepositVer,
               f_Pan AS Pan, f_Dob AS Dob, f_Name AS Name, f_Folio AS Folio, f_Gender AS Gender,
               f_Address AS Address, f_Data_Source AS DataSource, f_Pan_Filed AS PanFiled, f_Rec_Pan AS RecPan,
               f_Rec_Photo AS RecPhoto, f_Rec_Poa AS RecPoa, f_Renew_Dep_No AS RenewDepNo,
               f_Renew_Amount AS RenewAmount, f_Renew_Matures_On AS RenewMaturesOn, f_Renew_Rate AS RenewRate,
               f_Renew_Tenure AS RenewTenure, f_Renew_Payout AS RenewPayout,
               f_Renew_Principal AS RenewPrincipal, f_Submitted_On AS SubmittedOn, f_Sub_Status AS SubStatus,
               f_Link_Sent_To AS LinkSentTo, f_Link_Emailed_To AS LinkEmailedTo,
               f_Link_Valid_Until AS LinkValidUntil, f_Resends_Left AS ResendsLeft, f_Short_Url AS ShortUrl,
               f_Created_On AS CreatedOn, f_Cancelled_On AS CancelledOn
        FROM dbo.t_Unotp_Application_Mst WITH (UPDLOCK, ROWLOCK)
        WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1
        """;

    /// <summary>
    /// Whether an application is the partner's own: 1 when it is.
    /// </summary>
    public const string IsPartnersApplication = """
        SELECT COUNT(*) FROM dbo.t_Unotp_Application_Mst WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1
        """;

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
        INSERT dbo.t_Unotp_Application_Mst (f_App_No, f_Partner_Id, f_Status, f_Version, f_Upload_Ver, f_Payment_Ver, f_Deposit_Ver, f_Pan, f_Dob, f_Name, f_Folio, f_Gender, f_Address, f_Data_Source, f_Pan_Filed, f_Rec_Pan, f_Rec_Photo, f_Rec_Poa, f_Renew_Dep_No, f_Renew_Amount, f_Renew_Matures_On, f_Renew_Rate, f_Renew_Tenure, f_Renew_Payout, f_Renew_Principal, f_Created_By)
        VALUES (@AppNo, @Partner, 'PEN', @Version, @UploadVer, @PaymentVer, @DepositVer, @Pan, @Dob, @Name, @Folio, @Gender, @Address, @Source, @PanFiled, @RecPan, @RecPhoto, @RecPoa, @RenewDepNo, @RenewAmount, @RenewMaturesOn, @RenewRate, @RenewTenure, @RenewPayout, @RenewPrincipal, @Partner)
        """;

    /// <summary>
    /// Upload Documents saved: the application's version goes up, and the step's own version column
    /// (f_Upload_Ver) with it.
    /// </summary>
    public const string UploadDocumentsSaved = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Version = @Next, f_Upload_Ver = @Next, f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        WHERE f_App_No = @AppNo
        """;

    /// <summary>
    /// Investor Information saved: the application's version goes up, and the step's own version
    /// column (f_Details_Ver) with it.
    /// </summary>
    public const string InvestorInformationSaved = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Version = @Next, f_Details_Ver = @Next, f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        WHERE f_App_No = @AppNo
        """;

    /// <summary>
    /// Bank Details and Payment saved: the application's version goes up, and the step's own version
    /// column (f_Payment_Ver) with it.
    /// </summary>
    public const string BankDetailsSaved = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Version = @Next, f_Payment_Ver = @Next, f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        WHERE f_App_No = @AppNo
        """;

    /// <summary>
    /// FD Configuration saved: the application's version goes up, and the step's own version column
    /// (f_Deposit_Ver) with it.
    /// </summary>
    public const string FdConfigurationSaved = """
        UPDATE dbo.t_Unotp_Application_Mst
        SET f_Version = @Next, f_Deposit_Ver = @Next, f_Updated_By = @Partner, f_Updated_On = SYSDATETIME()
        WHERE f_App_No = @AppNo
        """;

    /// <summary>
    /// The upload step's state, as it was saved at one version of the application.
    /// </summary>
    public const string UploadState = """
        SELECT f_Upload FROM dbo.t_Unotp_Upload_State WHERE f_App_No = @AppNo AND f_App_Version = @UploadVer AND f_Active = 1
        """;

    /// <summary>
    /// Everything saved on an application, read in one go, a statement to a table and in this order:
    /// the upload state, then the active rows of each holder (named after the properties of KycRow;
    /// the name given is the father's, the mother's or the spouse's, whichever was filled), each
    /// address (AddressRow; the permanent address's row also carries the holder's mobile and e-mail),
    /// the nominee (NomineeRow), the payment (PaymentBankRow), the repayment account (RepaymentBankRow;
    /// its columns take no NULL, so one not given is blank there and NULL here), the deposit
    /// (InvestmentRow) and what is typed and not yet saved on each page. A column added to one of
    /// them needs its property on that row class.
    /// </summary>
    public const string WholeApplication = """
        SELECT f_Upload FROM dbo.t_Unotp_Upload_State WHERE f_App_No = @AppNo AND f_App_Version = @UploadVer AND f_Active = 1;

        SELECT f_Holder_Type AS HolderType, f_Kyc_NamePrefix AS NamePrefix, f_Kyc_Gender AS Gender,
            CASE WHEN f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
                 WHEN f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
            COALESCE(f_Kyc_FatherFullName, f_Kyc_MotherFullName, f_Kyc_SpouseFullName, N'') AS ParentName,
            f_Kyc_AnnualIncome_Desc AS AnnualIncome, f_CustSeg_Type_desc AS Occupation,
            f_CustSeg_Subtype_Desc AS SubOccupation,
            f_Kyc_MaritalStatus AS MaritalStatus,
            CASE f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
            CASE f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        ORDER BY f_Holder_Type;

        SELECT f_Holder_Type AS HolderType, f_AddType_Code AS AddrType, f_Add1 AS Line1, f_Add2 AS Line2,
               f_Add3 AS Line3, f_AddCity_Desc AS City, f_AddPin AS PinCode, f_AddDistrict_Desc AS District,
               f_AddState_Desc AS State, f_MobileNumber AS Mobile, f_EmailAdd AS Email
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl
        WHERE f_Appl_No = @AppNo AND f_Active = 1;

        SELECT TOP (1) f_Nominee_Name AS Name, f_Nominee_DOB AS Dob, f_Nominee_Relations AS Relation,
            f_GuardianName AS GuardianName, f_Address1 AS GuardianLine1, f_Address2 AS GuardianLine2,
            f_Address3 AS GuardianLine3, f_PIN AS GuardianPinCode, f_City AS GuardianCity
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Nominee_Dtl
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        ORDER BY f_Pk_t_FD_BT_Nominee_Dtl_Id DESC;

        SELECT TOP (1) f_Bank_NEFT AS Ifsc, f_BankAccountNo AS AccountNo, f_Cheque_DD_No AS ChequeNo,
            f_Cheque_DD_Date AS ChequeDate, f_CMS_Loc_Desc AS CmsLocation, f_CMS_Loc_CD AS CmsLocationCode
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC;

        SELECT TOP (1) ISNULL(f_sameAsCheque, 0) AS SameAsPayment, NULLIF(f_NEFTCode, N'') AS Ifsc,
            NULLIF(f_BankAccountNo, N'') AS AccountNo
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        ORDER BY f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC;

        SELECT TOP (1) CAST(f_Amount AS BIGINT) AS Amount, TRY_CAST(f_Tenure AS INT) AS TenureMonths,
            f_Int_Freq AS Payout, f_Is_Auto_Renewal AS AutoRenewal,
            CASE f_Renewal_For WHEN 'P' THEN 'principal' WHEN 'F' THEN 'principal-interest' ELSE '' END AS RenewInstruction,
            CASE f_ExistingFDRNoRenewalFor WHEN 'P' THEN 'principal' WHEN 'F' THEN 'principal-interest' ELSE '' END AS RenewalFor,
            CAST(CASE WHEN f_TDS_Flag = 'N' THEN 1 ELSE 0 END AS BIT) AS NoTds,
            f_FDR_Dispatch_Mode AS DeliveryType,
            f_AML_Source_Of_Funds AS SourceOfFunds, f_AML_Source_Of_Funds_Remarks AS SourceOfFundsRemark,
            f_AML_Source_Of_Funds_reason AS SourceOfFundsReason
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        ORDER BY f_Pk_t_FD_BT_Investment_Dtl_Id DESC;

        SELECT f_Page AS Page,
               f_State AS State FROM dbo.t_Unotp_Page_State WHERE f_App_No = @AppNo AND f_Active = 1;
        """;
}
