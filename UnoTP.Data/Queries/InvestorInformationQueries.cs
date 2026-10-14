namespace UnoTP.Data;

/// <summary>
/// The SQL of Investor Information: what is typed on the page and not yet saved, the
/// KYC details a holder on a folio need not type again, the PIN code master, the
/// nominee a renewal opens with, and saving the step - a KYC row and the address
/// rows of each holder, and the nominee. A joint holder's documents are saved by
/// UploadDocumentsQueries. Nothing but SQL is in this file.
///
/// A query on one of the FD system's own tables follows the rules in docs/CODE_GUIDE.md
/// ("A query on an FD table"): keep the names after AS, and keep each parameter in its WHERE.
/// </summary>
internal static class InvestorInformationQueries
{
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
    /// A folio holder's latest KYC row, where their KYC is on the common table. The inner read:
    /// The KYC details the common tables hold, shown on Investor Information so they
    /// are not typed again, for one holder (@Folio, @Pan, @Dob). Must give back: SavedOn (the
    /// latest row for the holder is the one used), NamePrefix (MR, MRS or MS, in any case; Miss on older rows: the holder's
    /// gender, where the folio master holds none), NameType (Father, Mother or Spouse), ParentName, AnnualIncome,
    /// Occupation, SubOccupation (the names the lists show), MaritalStatus (a code of the
    /// 'maritalStatuses' list), Pep and PepRelated (yes, no or empty), Mobile, Email,
    /// and the mailing address where one is held: MailLine1, MailLine2, MailLine3,
    /// MailCity, MailPinCode.
    /// The same read is in InvestorIdentificationQueries.NamePrefixOnCommon.
    /// </summary>
    public const string LatestKycOnCommon = """
        SELECT TOP (1) *
        FROM (
            SELECT k.f_CreatedOn AS SavedOn, k.f_Kyc_NamePrefix AS NamePrefix,
                   CASE WHEN k.f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN k.f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
                        WHEN k.f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
                   COALESCE(k.f_Kyc_FatherFullName, k.f_Kyc_MotherFullName, k.f_Kyc_SpouseFullName, N'') AS ParentName,
                   k.f_Kyc_AnnualIncome_Desc AS AnnualIncome, k.f_CustSeg_Type_desc AS Occupation,
                   k.f_CustSeg_Subtype_Desc AS SubOccupation,
                   k.f_Kyc_MaritalStatus AS MaritalStatus,
                   CASE k.f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
                   CASE k.f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated,
                   a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email,
                   m.f_Add1 AS MailLine1, m.f_Add2 AS MailLine2, m.f_Add3 AS MailLine3,
                   m.f_AddCity_Desc AS MailCity, m.f_AddPin AS MailPinCode
            FROM FD.dbo.t_FD_common_Kyc_Data_Dtl_ORA k WITH (NOLOCK)
            LEFT JOIN FD.dbo.t_FD_common_Address_Dtl_ORA a WITH (NOLOCK)
                ON a.f_Appl_No = k.f_Appl_No AND a.f_Holder_Type = k.f_Holder_Type
               AND a.f_AddType_Code = 'PER' AND a.f_Active = 1
            LEFT JOIN FD.dbo.t_FD_common_Address_Dtl_ORA m WITH (NOLOCK)
                ON m.f_Appl_No = k.f_Appl_No AND m.f_Holder_Type = k.f_Holder_Type
               AND m.f_AddType_Code = 'MAIL' AND m.f_Active = 1
            WHERE k.f_Active = 1 AND k.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        ) k
        ORDER BY k.SavedOn DESC
        """;

    /// <summary>
    /// A folio holder's latest KYC row, where their KYC is on the BT table. The inner read:
    /// The same KYC details, from the applications submitted through this app. Must give back the same names.
    /// The same read is in InvestorIdentificationQueries.NamePrefixOnBt.
    /// </summary>
    public const string LatestKycOnBt = """
        SELECT TOP (1) *
        FROM (
            SELECT k.f_CreatedOn AS SavedOn, k.f_Kyc_NamePrefix AS NamePrefix,
                   CASE WHEN k.f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN k.f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
                        WHEN k.f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
                   COALESCE(k.f_Kyc_FatherFullName, k.f_Kyc_MotherFullName, k.f_Kyc_SpouseFullName, N'') AS ParentName,
                   k.f_Kyc_AnnualIncome_Desc AS AnnualIncome, k.f_CustSeg_Type_desc AS Occupation,
                   k.f_CustSeg_Subtype_Desc AS SubOccupation,
                   k.f_Kyc_MaritalStatus AS MaritalStatus,
                   CASE k.f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
                   CASE k.f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated,
                   a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email,
                   m.f_Add1 AS MailLine1, m.f_Add2 AS MailLine2, m.f_Add3 AS MailLine3,
                   m.f_AddCity_Desc AS MailCity, m.f_AddPin AS MailPinCode
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl k
            LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl a
                ON a.f_Appl_No = k.f_Appl_No AND a.f_Holder_Type = k.f_Holder_Type
               AND a.f_AddType_Code = 'PER' AND a.f_Status = 'APR' AND a.f_Active = 1
            LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl m
                ON m.f_Appl_No = k.f_Appl_No AND m.f_Holder_Type = k.f_Holder_Type
               AND m.f_AddType_Code = 'MAIL' AND m.f_Status = 'APR' AND m.f_Active = 1
            WHERE k.f_Status = 'APR' AND k.f_Active = 1 AND k.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan
              AND k.f_Kyc_DOB = @Dob
        ) k
        ORDER BY k.SavedOn DESC
        """;

    /// <summary>One PIN code (@Pin). Must give back: PinCode, District, State.</summary>
    public const string PinCodes = """
        SELECT p.f_FD_CKYC_Pin_Code AS PinCode, p.f_FD_CKYC_District_Desc AS District,
               p.f_FD_CKYC_State_Name AS State
        FROM MMFSL_APP_FD_Common.dbo.t_FD_CMN_District_Mst p
        WHERE p.f_Active = 1 AND p.f_FD_CKYC_Pin_Code = @Pin
        """;

    /// <summary>
    /// The nominee on a booked deposit's application, for a renewal to open with.
    /// </summary>
    public const string NomineesOnDeposit = """
        SELECT n.f_Nominee_Name, n.f_Nominee_DOB, n.f_Nominee_Relations, ISNULL(n.f_GuardianName, N''),
               m.f_App_No
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Nominee_Dtl n
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = n.f_Appl_No AND m.f_Active = 1
        WHERE m.f_Folio = @Folio AND m.f_Fdr_No = @DepositNumber AND n.f_Status IN ('APR', 'PEN_E')
          AND n.f_Active = 1
        ORDER BY m.f_Submitted_On DESC
        """;

    /// <summary>
    /// Investor Information saved again: the holders' KYC rows it replaces go out of use, so the application's current KYC rows are the active ones.
    /// </summary>
    public const string RetireKyc = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(),
            f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Investor Information: one holder's KYC row.
    /// </summary>
    public const string InsertKyc = """
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Kyc_Data_Dtl (f_Holder_Type, f_Appl_No, f_Kyc_ConstiType, f_Kyc_NamePrefix, f_Kyc_Gender, f_Kyc_FirstName, f_Kyc_FullName, f_Kyc_FatherFirstName, f_Kyc_FatherFullName, f_Kyc_SpouseFirstName, f_Kyc_SpouseFullName, f_Kyc_MotherFirstName, f_Kyc_MotherFullName, f_Kyc_MaritalStatus, f_Kyc_Nationality_Code, f_Kyc_Nationality_Desc, f_Kyc_DOB, f_IsMinor, f_Kyc_PAN, f_Kyc_Number, f_FolioNo, f_Kyc_AnnualIncome_Code, f_Kyc_AnnualIncome_Desc, f_CustSeg_Type_desc, f_CustSeg_Subtype_Desc, f_CustSeg_Type_Code, f_CustSeg_Subtype_Code, f_Kyc_Occupation_Code, f_Kyc_Occupation_Desc, f_NSA_Response, f_NSA_Date, f_IsPEP, f_IsPEP_Relative, f_IsPanVerified, f_Data_Source, f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionID)
        VALUES (@HolderType, @AppNo, '01', @NamePrefix, @Gender, @FirstName, @Name, @Father, @Father, @Spouse, @Spouse, @Mother, @Mother, @MaritalStatus, 'IN', 'India', @Dob, @Minor, @Pan, @CkycNumber, @Folio, @AnnualIncomeCode, @AnnualIncome, @Occupation, @SubOccupation, @TypeCode, @SubTypeCode, @OccupationCode, @OccupationName, @ScreeningStatus, @ScreenedOn, @Pep, @PepRelated, @PanVerified, @DataSource, @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Investor Information saved again: the address rows it replaces go out of use, so the application's current address rows are the active ones.
    /// </summary>
    public const string RetireAddresses = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(),
            f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Investor Information: one address row of a holder (PER or MAIL).
    /// </summary>
    public const string InsertAddress = """
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Address_Dtl (f_Holder_Type, f_Appl_No, f_AddType_Code, f_AddType_Desc, f_Add1, f_Add2, f_Add3, f_AddCity_Desc, f_AddDistrict_Desc, f_AddState_Desc, f_AddPin, f_MobileNumber, f_EmailAdd, f_FolioNo, f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionID)
        VALUES (@HolderType, @AppNo, @Type, @TypeName, @Line1, @Line2, @Line3, @City, @District, @State, @PinCode, @Mobile, @Email, @Folio, @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Investor Information saved again: the nominee row it replaces goes out of use, so the
    /// application's current nominee is the active one.
    /// </summary>
    public const string RetireNominee = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Nominee_Dtl
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(),
            f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Investor Information: the nominee's row.
    /// </summary>
    public const string InsertNominee = """
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Nominee_Dtl (f_Appl_No, f_Nominee_Name, f_Nominee_Relations, f_Nominee_DOB, f_Is_Nominee_Minor, f_GuardianName, f_Address1, f_Address2, f_Address3, f_City, f_PIN, f_FolioNo, f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @Name, @Relation, @Dob, @Minor, @GuardianName, @GuardianLine1, @GuardianLine2, @GuardianLine3, @GuardianCity, @GuardianPinCode, @Folio, @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;
}
