namespace UnoTP.Data;

/// <summary>
/// The SQL of Upload Documents: saving the step's state and its document rows
/// (t_FD_BT_KYC_document), and the two registers the sourcing codes are searched in
/// as they are typed - brokers, and the staff each sourcing mode takes.
/// Investor Information runs the document queries too, for a joint holder's
/// documents. Nothing but SQL is in this file.
///
/// A query on one of the FD system's own tables follows the rules in docs/CODE_GUIDE.md
/// ("A query on an FD table"): keep the names after AS, and keep each parameter in its WHERE.
/// A register's query takes two parameters, both in its own WHERE: the key of one
/// row (@Code), and what was typed (@Search, a LIKE pattern). The app gives one and
/// leaves the other NULL, so neither ever brings back the whole register.
/// </summary>
internal static class UploadDocumentsQueries
{
    /// <summary>
    /// The upload step's state, saved at a version of the application.
    /// </summary>
    public const string InsertUploadState = """
        INSERT dbo.t_Unotp_Upload_State (f_App_No, f_App_Version, f_Status, f_Upload, f_Created_By)
        VALUES (@AppNo, @Version, @Status, @Json, @By)
        """;

    /// <summary>
    /// The application's document rows in use, named after the properties of DocumentRow
    /// (SaveStep.UploadDocuments.cs).
    /// </summary>
    public const string ActiveDocuments = """
        SELECT f_Pk_t_FD_BT_KYC_document_ID AS Id, f_Holder_Type_Code AS HolderType,
               f_Doc_Type_Code AS TypeCode, f_Doc_Sub_Type_Code AS SubTypeCode, f_Doc_Type_Desc AS TypeName,
               f_Doc_Sub_Type_Desc AS SubTypeName, f_Doc_FileName AS FileName, f_Doc_Filepath AS FilePath,
               f_FolioNo AS Folio, f_Doc_Sequence AS Sequence, f_Document_Source AS UploadedFrom,
               f_Doc_Ref_No AS Number, f_Doc_Exp_Date AS Expiry, f_IsDocumentMasked AS Masked,
               f_is_ocrextract AS OcrAsked, f_isocrdataextract AS [Read], f_isocrdataverified AS Verified,
               f_IsidfyDocIdentified AS Identified, f_IdfyIdentifiedDocument AS IdentifiedAs,
               f_IsidfyFaceCompared AS FaceCompared, f_IsidfyDocFaceDetected AS FaceFound,
               CAST(f_IdfyFaceMatchPercentage AS INT) AS FaceScore, f_IdfyGroupId AS IdfyGroupId,
               f_IdfyIdentifyTaskId AS IdentifyTaskId, f_IdfyExtractTaskId AS ExtractTaskId,
               f_IdfyVerifyTaskId AS VerifyTaskId, f_IdfyFaceCompareTaskId AS FaceCompareTaskId,
               f_NsdlPanMatch AS NsdlPan, f_NsdlDobMatch AS NsdlDob, f_NsdlNameMatch AS NsdlName,
               f_NsdlRemark AS NsdlRemark, f_PanPoaNameMatch AS ProofName, f_PanPoaDobMatch AS ProofDob,
               f_IsVerifiedWithSource AS SourceVerified, f_VerifiedSource AS VerifiedSource,
               f_IsPanAadhaarLinked AS PanAadhaarLinked, f_IdfyPanAadhaarLinkTaskId AS LinkTaskId,
               f_Status AS Status
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_KYC_document
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Document rows taken out of use, by their ids: those a save replaced or took off.
    /// </summary>
    public const string RetireDocuments = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_KYC_document
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedDate = GETDATE(),
            f_UpdatedIP = @Ip
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
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_KYC_document (f_Appl_No, f_Holder_Type_Code, f_Doc_Type_Code, f_Doc_Sub_Type_Code, f_Doc_Type_Desc, f_Doc_Sub_Type_Desc, f_Doc_FileName, f_Doc_Filepath, f_FolioNo, f_Doc_Sequence, f_Document_Source, f_doc_source, f_Doc_Ref_No, f_Doc_Exp_Date, f_IsDocumentMasked, f_is_ocrextract, f_isocrdataextract, f_isocrdataverified, f_IsidfyDocIdentified, f_IdfyIdentifiedDocument, f_IsidfyDocDataExtracted, f_IsidfyDocDataVerified, f_IsidfyFaceCompared, f_IsidfyDocFaceDetected, f_IdfyFaceMatchPercentage, f_IdfyGroupId, f_IdfyIdentifyTaskId, f_IdfyExtractTaskId, f_IdfyVerifyTaskId, f_IdfyFaceCompareTaskId, f_NsdlPanMatch, f_NsdlDobMatch, f_NsdlNameMatch, f_NsdlRemark, f_PanPoaNameMatch, f_PanPoaDobMatch, f_IsVerifiedWithSource, f_VerifiedSource, f_IsPanAadhaarLinked, f_IdfyPanAadhaarLinkTaskId, f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedDate, f_CreatedIP, f_Session_ID)
        VALUES (@AppNo, @HolderType, @TypeCode, @SubTypeCode, @TypeName, @SubTypeName, @FileName, @FilePath, @Folio, @Sequence, @UploadedFrom, @UploadedFrom, @Number, @Expiry, @Masked, @OcrAsked, @Read, @Verified, @Identified, @IdentifiedAs, @Read, @Verified, @FaceCompared, @FaceFound, @FaceScore, @IdfyGroupId, @IdentifyTaskId, @ExtractTaskId, @VerifyTaskId, @FaceCompareTaskId, @NsdlPan, @NsdlDob, @NsdlName, @NsdlRemark, @ProofName, @ProofDob, @SourceVerified, @VerifiedSource, @PanAadhaarLinked, @LinkTaskId, @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// The one broker a code names (@Code; @Search is given as NULL).
    /// The inner read is the register - the brokers an application can be sourced under - and must give back: Code, Name.
    /// It finds one broker by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in Broker and in SearchBrokers.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string Broker = """
        SELECT TOP (1) p.Code, p.Name
        FROM (
            SELECT b.BROKER_CODE AS Code, b.NAME AS Name
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BTP_Broker_Mst b
            WHERE b.REC_STATUS = 'A'
              AND (@Code IS NULL OR b.BROKER_CODE = @Code)
              AND (@Search IS NULL OR b.BROKER_CODE + ' ' + b.NAME LIKE @Search)
        ) p
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The brokers whose code and name hold what was typed (@Search; @Code is given as NULL); a code
    /// that starts with the first word (@First) comes first. At most 20 of them.
    /// The inner read is the register - the brokers an application can be sourced under - and must give back: Code, Name.
    /// It finds one broker by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in Broker and in SearchBrokers.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string SearchBrokers = """
        SELECT TOP (20) p.Code, p.Name
        FROM (
            SELECT b.BROKER_CODE AS Code, b.NAME AS Name
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BTP_Broker_Mst b
            WHERE b.REC_STATUS = 'A'
              AND (@Code IS NULL OR b.BROKER_CODE = @Code)
              AND (@Search IS NULL OR b.BROKER_CODE + ' ' + b.NAME LIKE @Search)
        ) p
        ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name
        OPTION (RECOMPILE)
        """;

    // ----- The staff a sourcing mode takes ---------------------------------------
    // A sourcing mode names its staff rule in the 'staff' setting of its row in
    // 'sourcingModes' (t_Unotp_Ref_List): branch, mfis or mflEx. Each rule has two
    // queries below - the one employee a code names, and those a search finds; a
    // mode with no rule takes any employee in service.

    /// <summary>
    /// The one employee a code names (@Code; @Search is given as NULL).
    /// The inner read is the register - every employee in service: the staff of a sourcing mode that names no staff rule - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in StaffMember and in SearchStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string StaffMember = """
        SELECT TOP (1) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The employees whose code and name hold what was typed (@Search; @Code is given as NULL); a code
    /// that starts with the first word (@First) comes first. At most 20 of them.
    /// The inner read is the register - every employee in service: the staff of a sourcing mode that names no staff rule - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in StaffMember and in SearchStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string SearchStaff = """
        SELECT TOP (20) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The one employee a code names (@Code; @Search is given as NULL).
    /// The inner read is the register - the staff of MMFSS - BRANCH (staff rule 'branch'): pay group 1033 outside the investment and
    /// deposit departments, and the retail branch - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in BranchStaffMember and in SearchBranchStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string BranchStaffMember = """
        SELECT TOP (1) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND (
                    (e.DEPARTMENT_NAME NOT IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
                     AND e.PAYGRP IN ('1033'))
                    OR e.DEPARTMENT_NAME = 'RETAIL BRANCH'
                  )
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The employees whose code and name hold what was typed (@Search; @Code is given as NULL); a code
    /// that starts with the first word (@First) comes first. At most 20 of them.
    /// The inner read is the register - the staff of MMFSS - BRANCH (staff rule 'branch'): pay group 1033 outside the investment and
    /// deposit departments, and the retail branch - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in BranchStaffMember and in SearchBranchStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string SearchBranchStaff = """
        SELECT TOP (20) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND (
                    (e.DEPARTMENT_NAME NOT IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
                     AND e.PAYGRP IN ('1033'))
                    OR e.DEPARTMENT_NAME = 'RETAIL BRANCH'
                  )
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The one employee a code names (@Code; @Search is given as NULL).
    /// The inner read is the register - the staff of MFIS / MFIS-DFB / FD (staff rule 'mfis'): the investment and deposit departments - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in MfisStaffMember and in SearchMfisStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string MfisStaffMember = """
        SELECT TOP (1) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND e.DEPARTMENT_NAME IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The employees whose code and name hold what was typed (@Search; @Code is given as NULL); a code
    /// that starts with the first word (@First) comes first. At most 20 of them.
    /// The inner read is the register - the staff of MFIS / MFIS-DFB / FD (staff rule 'mfis'): the investment and deposit departments - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in MfisStaffMember and in SearchMfisStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string SearchMfisStaff = """
        SELECT TOP (20) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND e.DEPARTMENT_NAME IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The one employee a code names (@Code; @Search is given as NULL).
    /// The inner read is the register - the staff of MFL-EX (staff rule 'mflEx'): pay groups 1031, 1032, 1033 and 1059, and the retail branch - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in MflExStaffMember and in SearchMflExStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string MflExStaffMember = """
        SELECT TOP (1) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND (
                    e.PAYGRP IN ('1031', '1032', '1033', '1059')
                    OR e.DEPARTMENT_NAME = 'RETAIL BRANCH'
                  )
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The employees whose code and name hold what was typed (@Search; @Code is given as NULL); a code
    /// that starts with the first word (@First) comes first. At most 20 of them.
    /// The inner read is the register - the staff of MFL-EX (staff rule 'mflEx'): pay groups 1031, 1032, 1033 and 1059, and the retail branch - and must give back: Code, Name.
    /// It finds one employee by code (@Code), or those whose code and name hold what was typed (@Search).
    /// The same read of the register is in MflExStaffMember and in SearchMflExStaff.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole register to find one row.
    /// </summary>
    public const string SearchMflExStaff = """
        SELECT TOP (20) p.Code, p.Name
        FROM (
            SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
            FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
            WHERE e.ACTIVE = 'A'
              AND (
                    e.PAYGRP IN ('1031', '1032', '1033', '1059')
                    OR e.DEPARTMENT_NAME = 'RETAIL BRANCH'
                  )
              AND (@Code IS NULL OR e.EMPCODE = @Code)
              AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        ) p
        ORDER BY CASE WHEN p.Code LIKE @First THEN 0 ELSE 1 END, p.Name
        OPTION (RECOMPILE)
        """;
}
