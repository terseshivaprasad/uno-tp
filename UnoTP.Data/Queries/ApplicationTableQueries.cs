namespace UnoTP.Data;

/// <summary>
/// The SQL for the FD system's own tables an application is written to and read from
/// (db/create_transaction_tables.sql): t_FD_BT_KYC_document, t_FD_BT_Kyc_Data_Dtl,
/// t_FD_BT_Address_Dtl, t_FD_BT_Nominee_Dtl, t_FD_BT_Payment_Dtl,
/// t_FD_BT_Investor_Bank_Dtl, t_FD_BT_Investment_Dtl and the source of funds log.
/// Nothing but SQL is in this file. A column added to an INSERT here needs its value
/// in the save beside it (SaveStep.*.cs); a column added to a column list needs its
/// property on the row class (ApplicationTableRows.cs).
/// </summary>
internal static class ApplicationTableQueries
{
    /// <summary>
    /// The repayment account's row, named after the properties of RepaymentBankRow. The account's
    /// columns take no NULL: one not given is blank there, and NULL here.
    /// </summary>
    public const string RepaymentColumns = """
        ISNULL(f_sameAsCheque, 0) AS SameAsPayment, NULLIF(f_NEFTCode, N'') AS Ifsc, NULLIF(f_BankAccountNo, N'') AS AccountNo
        """;

    /// <summary>
    /// What t_FD_BT_Kyc_Data_Dtl holds of a holder, named after the properties of KycRow. The name
    /// given is the father's, the mother's or the spouse's: whichever was filled.
    /// </summary>
    public const string KycColumns = """
        f_Holder_Type AS HolderType, f_Kyc_NamePrefix AS NamePrefix,
        CASE WHEN f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
             WHEN f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
        COALESCE(f_Kyc_FatherFullName, f_Kyc_MotherFullName, f_Kyc_SpouseFullName, N'') AS ParentName,
        f_Kyc_AnnualIncome_Desc AS AnnualIncome, f_CustSeg_Type_desc AS Occupation, f_CustSeg_Subtype_Desc AS SubOccupation,
        f_Kyc_MaritalStatus AS MaritalStatus,
        CASE f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
        CASE f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated
        """;

    /// <summary>
    /// A holder's address row, named after the properties of AddressRow. The permanent address's
    /// row also carries the holder's mobile and e-mail.
    /// </summary>
    public const string AddressColumns = """
        f_Holder_Type AS HolderType, f_AddType_Code AS AddrType, f_Add1 AS Line1, f_Add2 AS Line2, f_Add3 AS Line3,
        f_AddCity_Desc AS City, f_AddPin AS PinCode, f_AddDistrict_Desc AS District, f_AddState_Desc AS State,
        f_MobileNumber AS Mobile, f_EmailAdd AS Email
        """;

    /// <summary>
    /// The nominee's row, named after the properties of NomineeRow.
    /// </summary>
    public const string NomineeColumns = """
        f_Nominee_Name AS Name, f_Nominee_DOB AS Dob, f_Nominee_Relations AS Relation, f_GuardianName AS GuardianName,
        f_Address1 AS GuardianLine1, f_Address2 AS GuardianLine2, f_Address3 AS GuardianLine3,
        f_PIN AS GuardianPinCode, f_City AS GuardianCity
        """;

    /// <summary>
    /// The payment row, named after the properties of PaymentBankRow.
    /// </summary>
    public const string PaymentColumns = """
        f_Bank_NEFT AS Ifsc, f_BankAccountNo AS AccountNo, f_Cheque_DD_No AS ChequeNo, f_Cheque_DD_Date AS ChequeDate,
        f_CMS_Loc_Desc AS CmsLocation, f_CMS_Loc_CD AS CmsLocationCode
        """;

    /// <summary>
    /// The deposit row, named after the properties of InvestmentRow.
    /// </summary>
    public const string InvestmentColumns = """
        CAST(f_Amount AS BIGINT) AS Amount, TRY_CAST(f_Tenure AS INT) AS TenureMonths, f_Int_Freq AS Payout,
        f_Is_Auto_Renewal AS AutoRenewal,
        CASE f_Renewal_For WHEN 'P' THEN 'principal' WHEN 'F' THEN 'principal-interest' ELSE '' END AS RenewInstruction,
        CASE f_ExistingFDRNoRenewalFor WHEN 'P' THEN 'principal' WHEN 'F' THEN 'principal-interest' ELSE '' END AS RenewalFor,
        CAST(CASE WHEN f_TDS_Flag = 'N' THEN 1 ELSE 0 END AS BIT) AS NoTds, f_FDR_Dispatch_Mode AS DeliveryType,
        f_AML_Source_Of_Funds AS SourceOfFunds, f_AML_Source_Of_Funds_Remarks AS SourceOfFundsRemark,
        f_AML_Source_Of_Funds_reason AS SourceOfFundsReason
        """;

    /// <summary>
    /// The application's current deposit row as i, for a query over t_Unotp_Application_Mst m:
    /// its active row. The newest is taken, should there ever be two.
    /// </summary>
    public const string CurrentDepositOf = """
        OUTER APPLY (
            SELECT TOP (1) CAST(d.f_Amount AS BIGINT) AS Amount, TRY_CAST(d.f_Tenure AS INT) AS TenureMonths,
                d.f_Int_Freq AS Payout, d.f_ApplicationDeclarationType AS AppType
            FROM dbo.t_FD_BT_Investment_Dtl d
            WHERE d.f_Appl_No = m.f_App_No AND d.f_Active = 1
            ORDER BY d.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) i
        """;

    /// <summary>
    /// Everything saved on an application, read in one go: the upload state, then the active
    /// rows of each holder, address, nominee, payment, repayment account and deposit.
    /// </summary>
    public const string WholeApplication = $"""
        SELECT f_Upload FROM dbo.t_Unotp_Upload_State WHERE f_App_No = @AppNo AND f_App_Version = @UploadVer AND f_Active = 1;
        SELECT {KycColumns} FROM dbo.t_FD_BT_Kyc_Data_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Holder_Type;
        SELECT {AddressColumns} FROM dbo.t_FD_BT_Address_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1;
        SELECT TOP (1) {NomineeColumns} FROM dbo.t_FD_BT_Nominee_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Nominee_Dtl_Id DESC;
        SELECT TOP (1) {PaymentColumns} FROM dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC;
        SELECT TOP (1) {RepaymentColumns} FROM dbo.t_FD_BT_Investor_Bank_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Investor_Bank_Dtl_Id DESC;
        SELECT TOP (1) {InvestmentColumns} FROM dbo.t_FD_BT_Investment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Investment_Dtl_Id DESC;
        SELECT f_Page AS Page, f_State AS State FROM dbo.t_Unotp_Page_State WHERE f_App_No = @AppNo AND f_Active = 1;
        """;

    /// <summary>
    /// The rows a save replaces go out of use, so a section's current rows are the application's
    /// active ones. table is the section's table; updatedOn its updated-date column.
    /// </summary>
    public static string Retire(string table, string updatedOn) =>
        $"""
        UPDATE dbo.{table}
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, {updatedOn} = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// The application's document rows in use, named after the properties of DocumentRow
    /// (SaveStep.UploadDocuments.cs).
    /// </summary>
    public const string ActiveDocuments = """
        SELECT f_Pk_t_FD_BT_KYC_document_ID AS Id, f_Holder_Type_Code AS HolderType,
               f_Doc_Type_Code AS TypeCode, f_Doc_Sub_Type_Code AS SubTypeCode, f_Doc_Type_Desc AS TypeName, f_Doc_Sub_Type_Desc AS SubTypeName,
               f_Doc_FileName AS FileName, f_Doc_Filepath AS FilePath, f_FolioNo AS Folio, f_Doc_Sequence AS Sequence,
               f_Document_Source AS UploadedFrom, f_Doc_Ref_No AS Number, f_Doc_Exp_Date AS Expiry,
               f_IsDocumentMasked AS Masked, f_is_ocrextract AS OcrAsked, f_isocrdataextract AS [Read], f_isocrdataverified AS Verified,
               f_IsidfyDocIdentified AS Identified, f_IdfyIdentifiedDocument AS IdentifiedAs,
               f_IsidfyFaceCompared AS FaceCompared, f_IsidfyDocFaceDetected AS FaceFound, CAST(f_IdfyFaceMatchPercentage AS INT) AS FaceScore,
               f_IdfyGroupId AS IdfyGroupId, f_IdfyIdentifyTaskId AS IdentifyTaskId, f_IdfyExtractTaskId AS ExtractTaskId,
               f_IdfyVerifyTaskId AS VerifyTaskId, f_IdfyFaceCompareTaskId AS FaceCompareTaskId,
               f_NsdlPanMatch AS NsdlPan, f_NsdlDobMatch AS NsdlDob, f_NsdlNameMatch AS NsdlName, f_NsdlRemark AS NsdlRemark,
               f_PanPoaNameMatch AS ProofName, f_PanPoaDobMatch AS ProofDob,
               f_IsVerifiedWithSource AS SourceVerified, f_VerifiedSource AS VerifiedSource,
               f_IsPanAadhaarLinked AS PanAadhaarLinked, f_IdfyPanAadhaarLinkTaskId AS LinkTaskId,
               f_Status AS Status
        FROM dbo.t_FD_BT_KYC_document
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Document rows taken out of use, by their ids: those a save replaced or took off.
    /// </summary>
    public const string RetireDocuments = """
        UPDATE dbo.t_FD_BT_KYC_document
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedDate = GETDATE(), f_UpdatedIP = @Ip
        WHERE f_Pk_t_FD_BT_KYC_document_ID IN @Ids
        """;

    /// <summary>
    /// Upload Documents: one document row, with what the checks said of the copy, IDfy's id for
    /// each request it answered about it, and the application's group id (the f_Idfy...Id
    /// columns). A PAN copy's row also says what NSDL and the PAN-Aadhaar link said, a proof of
    /// address's row how its name and date of birth stand against the PAN's, and either whether
    /// its source confirmed it. All of these columns are added by db/alter_transaction_tables.sql.
    /// </summary>
    public const string InsertDocument = """
        INSERT dbo.t_FD_BT_KYC_document (f_Appl_No, f_Holder_Type_Code, f_Doc_Type_Code, f_Doc_Sub_Type_Code, f_Doc_Type_Desc, f_Doc_Sub_Type_Desc,
            f_Doc_FileName, f_Doc_Filepath, f_FolioNo, f_Doc_Sequence, f_Document_Source, f_doc_source, f_Doc_Ref_No, f_Doc_Exp_Date,
            f_IsDocumentMasked, f_is_ocrextract, f_isocrdataextract, f_isocrdataverified,
            f_IsidfyDocIdentified, f_IdfyIdentifiedDocument, f_IsidfyDocDataExtracted, f_IsidfyDocDataVerified,
            f_IsidfyFaceCompared, f_IsidfyDocFaceDetected, f_IdfyFaceMatchPercentage,
            f_IdfyGroupId, f_IdfyIdentifyTaskId, f_IdfyExtractTaskId, f_IdfyVerifyTaskId, f_IdfyFaceCompareTaskId,
            f_NsdlPanMatch, f_NsdlDobMatch, f_NsdlNameMatch, f_NsdlRemark, f_PanPoaNameMatch, f_PanPoaDobMatch,
            f_IsVerifiedWithSource, f_VerifiedSource, f_IsPanAadhaarLinked, f_IdfyPanAadhaarLinkTaskId,
            f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedDate, f_CreatedIP, f_Session_ID)
        VALUES (@AppNo, @HolderType, @TypeCode, @SubTypeCode, @TypeName, @SubTypeName,
            @FileName, @FilePath, @Folio, @Sequence, @UploadedFrom, @UploadedFrom, @Number, @Expiry,
            @Masked, @OcrAsked, @Read, @Verified,
            @Identified, @IdentifiedAs, @Read, @Verified,
            @FaceCompared, @FaceFound, @FaceScore,
            @IdfyGroupId, @IdentifyTaskId, @ExtractTaskId, @VerifyTaskId, @FaceCompareTaskId,
            @NsdlPan, @NsdlDob, @NsdlName, @NsdlRemark, @ProofName, @ProofDob,
            @SourceVerified, @VerifiedSource, @PanAadhaarLinked, @LinkTaskId,
            @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Investor Information: one holder's KYC row.
    /// </summary>
    public const string InsertKyc = """
        INSERT dbo.t_FD_BT_Kyc_Data_Dtl (f_Holder_Type, f_Appl_No, f_Kyc_ConstiType, f_Kyc_NamePrefix, f_Kyc_FirstName, f_Kyc_FullName,
            f_Kyc_FatherFirstName, f_Kyc_FatherFullName, f_Kyc_SpouseFirstName, f_Kyc_SpouseFullName,
            f_Kyc_MotherFirstName, f_Kyc_MotherFullName, f_Kyc_MaritalStatus, f_Kyc_Nationality_Code, f_Kyc_Nationality_Desc,
            f_Kyc_DOB, f_IsMinor, f_Kyc_PAN, f_Kyc_Number, f_FolioNo, f_Kyc_AnnualIncome_Code, f_Kyc_AnnualIncome_Desc, f_CustSeg_Type_desc, f_CustSeg_Subtype_Desc,
            f_CustSeg_Type_Code, f_CustSeg_Subtype_Code, f_Kyc_Occupation_Code, f_Kyc_Occupation_Desc,
            f_NSA_Response, f_NSA_Date, f_IsPEP, f_IsPEP_Relative, f_IsPanVerified, f_Data_Source,
            f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionID)
        VALUES (@HolderType, @AppNo, '01', @NamePrefix, @FirstName, @Name,
            @Father, @Father, @Spouse, @Spouse,
            @Mother, @Mother, @MaritalStatus, 'IN', 'India',
            @Dob, @Minor, @Pan, @CkycNumber, @Folio, @AnnualIncomeCode, @AnnualIncome, @Occupation, @SubOccupation,
            @TypeCode, @SubTypeCode, @OccupationCode, @OccupationName,
            @ScreeningStatus, @ScreenedOn, @Pep, @PepRelated, @PanVerified, @DataSource,
            @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Investor Information: one address row of a holder (PER or MAIL).
    /// </summary>
    public const string InsertAddress = """
        INSERT dbo.t_FD_BT_Address_Dtl (f_Holder_Type, f_Appl_No, f_AddType_Code, f_AddType_Desc, f_Add1, f_Add2, f_Add3,
            f_AddCity_Desc, f_AddDistrict_Desc, f_AddState_Desc, f_AddPin, f_MobileNumber, f_EmailAdd, f_FolioNo,
            f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionID)
        VALUES (@HolderType, @AppNo, @Type, @TypeName, @Line1, @Line2, @Line3,
            @City, @District, @State, @PinCode, @Mobile, @Email, @Folio,
            @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Investor Information: the nominee's row.
    /// </summary>
    public const string InsertNominee = """
        INSERT dbo.t_FD_BT_Nominee_Dtl (f_Appl_No, f_Nominee_Name, f_Nominee_Relations, f_Nominee_DOB, f_Is_Nominee_Minor,
            f_GuardianName, f_Address1, f_Address2, f_Address3, f_City, f_PIN, f_FolioNo,
            f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @Name, @Relation, @Dob, @Minor,
            @GuardianName, @GuardianLine1, @GuardianLine2, @GuardianLine3, @GuardianCity, @GuardianPinCode, @Folio,
            @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Bank Details and Payment: how the deposit is paid, and the account it is paid from.
    /// </summary>
    public const string InsertPayment = """
        INSERT dbo.t_FD_BT_Payment_Dtl (f_Appl_No, f_Payment_Mode, f_Cheque_DD_No, f_Cheque_DD_Date,
            f_Drawn_Bank_Name, f_Bank_Branch_Name, f_Bank_MICR, f_Bank_NEFT, f_BankAccountNo, f_CMS_Loc_CD, f_CMS_Loc_Desc, f_CMS_BANK_NAME, f_FolioNo,
            f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @PayMode, @ChequeNo, @ChequeDate,
            @Bank, @BranchName, @Micr, @Ifsc, @AccountNo, @CmsLocationCode, @CmsLocation, @CmsBank, @Folio,
            @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Bank Details and Payment: the repayment account.
    /// </summary>
    public const string InsertRepaymentAccount = """
        INSERT dbo.t_FD_BT_Investor_Bank_Dtl (f_Appl_No, f_MICRCode, f_NEFTCode, f_BankName, f_BranchName, f_BankAccountNo, f_sameAsCheque, f_FolioNo,
            f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @Micr, @Ifsc, @Bank, @BranchName, @AccountNo, @SameAsPayment, @Folio,
            @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// FD Configuration: the deposit row. The Axis CMS location is copied from the active payment row.
    /// </summary>
    public const string InsertInvestment = """
        INSERT dbo.t_FD_BT_Investment_Dtl (f_Appl_No, f_Status, f_Amount, f_Tenure, f_Int_Freq, f_Scheme, f_Scheme_Code,
            f_Is_Auto_Renewal, f_Renewal_For, f_TDS_Flag, f_FDR_Dispatch_Mode,
            f_AML_Source_Of_Funds, f_AML_Source_Of_Funds_Remarks, f_AML_Source_Of_Funds_reason,
            f_ApplicationDeclarationType, f_Category, f_Source, f_Broker_Code,
            f_Employee_Code, f_EmpCompanyName, f_EmpHolder, f_EmpRelation, f_ExistingFDRNo, f_ExistingFDRNoRenewalFor,
            f_Depositor_Status_Code, f_Ind_Nind, f_HNG, f_FolioNo, f_Cms_Location_Code, f_Cms_Location_Name,
            f_Int_Rate, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @Status, @Amount, @Tenure, @Payout, @Scheme, @SchemeCode,
            @AutoRenewal, @RenewInstruction, @TdsFlag, @DeliveryType,
            @SourceOfFunds, @SourceOfFundsRemark, @SourceOfFundsReason,
            @AppType, @Category, @Source, @SourceCode,
            @EmpCode, @EmpCompany, @EmpHolder, @EmpRelation, @Renews, @ExistingRenewalFor,
            @Individual, @Individual, @Hng, @Folio,
            (SELECT TOP (1) f_CMS_Loc_CD FROM dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC),
            (SELECT TOP (1) f_CMS_Loc_Desc FROM dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC),
            @Rate, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// The source of funds log: the application's earlier entry goes out of use.
    /// </summary>
    public const string RetireSourceOfFunds = "UPDATE dbo.t_FD_CMN_AML_Source_Of_Funds_Log SET f_Active = 0 WHERE f_Appl_No = @AppNo AND f_Active = 1";

    /// <summary>
    /// The source of funds log: the source the investor gave, with what it was asked for.
    /// </summary>
    public const string InsertSourceOfFunds = """
        INSERT dbo.t_FD_CMN_AML_Source_Of_Funds_Log (f_Appl_No, f_Holder_Type, f_Investment_Amt, f_AnnualIncome_Desc,
            f_AML_Source_Of_Funds, f_AML_Source_Of_Funds_Remarks, f_AML_Source_Of_Funds_reason,
            f_FormCode, f_Source, f_Folio_No, f_Occupation_Desc,
            f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @HolderType, @Amount, @AnnualIncome,
            @SourceOfFunds, @SourceOfFundsRemark, @SourceOfFundsReason,
            @FormNo, 'UNO_TP', @Folio, @Occupation,
            1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// The nominee on a booked deposit's application, for a renewal to open with.
    /// </summary>
    public const string NomineesOnDeposit = """
        SELECT n.f_Nominee_Name, n.f_Nominee_DOB, n.f_Nominee_Relations, ISNULL(n.f_GuardianName, N''), m.f_App_No
        FROM dbo.t_FD_BT_Nominee_Dtl n
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = n.f_Appl_No AND m.f_Active = 1
        WHERE m.f_Folio = @Folio AND m.f_Fdr_No = @DepositNumber AND n.f_Status IN ('APR', 'PEN_E') AND n.f_Active = 1
        ORDER BY m.f_Submitted_On DESC
        """;

    /// <summary>
    /// The repayment account on a booked deposit's application, for a renewal to open with.
    /// </summary>
    public const string AccountsOnDeposit = """
        SELECT b.f_NEFTCode, b.f_BankAccountNo, b.f_BankName, b.f_BranchName, m.f_App_No
        FROM dbo.t_FD_BT_Investor_Bank_Dtl b
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = b.f_Appl_No AND m.f_Active = 1
        WHERE m.f_Folio = @Folio AND m.f_Fdr_No = @DepositNumber AND b.f_Status IN ('APR', 'PEN_E') AND b.f_Active = 1
          AND b.f_NEFTCode <> '' AND b.f_BankAccountNo <> ''
        ORDER BY m.f_Submitted_On DESC
        """;

    /// <summary>
    /// The joint holders on a set of applications: every holder but the first.
    /// </summary>
    public const string JointHoldersOf = """
        SELECT f_Appl_No, ISNULL(f_Kyc_PAN, N''), f_Kyc_DOB, ISNULL(f_Kyc_FullName, N''), ISNULL(f_FolioNo, N'')
        FROM dbo.t_FD_BT_Kyc_Data_Dtl
        WHERE f_Appl_No IN @AppNos AND f_Active = 1 AND f_Holder_Type <> @Investor
        ORDER BY f_Appl_No, f_Holder_Type
        """;
}
