namespace UnoTP.Data;

/// <summary>
/// The SQL that reads each big table the app only looks rows up in: folios, the deposits
/// the folio check looks at, brokers, the staff each sourcing mode takes,
/// staff, bank branches, PIN codes, the Axis CMS locations, the payment gateway's
/// banks and the rate card. Change a query here when a table
/// has another name, needs a join, or needs a condition. The small lists (categories,
/// payouts, relations, occupations and the rest) are not here: they are rows of
/// t_Unotp_Ref_List, read in SqlReference.
///
///   - Keep the names after AS: they are what the app reads. A column may come from
///     any table or expression, so long as it comes back under the same name.
///   - A table in another database on the same server is written by its full name:
///     OtherDb.dbo.Table.
///   - No ORDER BY here. The app reads each query as
///     SELECT ... FROM ( the query ) x WHERE ... ORDER BY ...
///     to find one row, to search and to sort.
/// </summary>
internal static class MasterQueries
{
    /// <summary>
    /// The investor folios, from the FD system's folio master (one row per folio). Must give back: Pan, Dob,
    /// Folio, Name, Gender, Address, DocPan, DocPhoto, DocPoa, Note, Source.
    /// The address is its parts with a comma between them; a part the folio does not
    /// hold is left out.
    /// </summary>
    public const string Folios = """
        SELECT f.PAN_NO AS Pan, f.f_DOB AS Dob, f.FOLIO_NO AS Folio, f.NAME AS Name, f.F_Gender AS Gender,
               CONCAT(f.f_Address1 + ', ', f.f_Address2 + ', ', f.f_Address3 + ', ', f.F_City + ', ',
                      f.f_stateName + ', ', f.F_District + ', ', f.f_Pincode) AS Address,
               0 AS DocPan, 0 AS DocPhoto, 0 AS DocPoa, '' AS Note, 'FHLD' AS Source
        FROM FD.dbo.t_Fd_Folio_holding f
        WHERE f.f_Active = 1
        """;

    /// <summary>
    /// The first holders in the folio master: asked for a PAN the folio check found no
    /// deposit for, so that a holder who already has a folio is not taken as new.
    /// Must give back: Pan, Folio.
    /// </summary>
    public const string FirstHolders = """
        SELECT f.PAN_NO AS Pan, f.FOLIO_NO AS Folio
        FROM FD.dbo.t_Fd_Folio_holding f WITH (NOLOCK)
        WHERE f.f_Active = 1 AND f.HOLD_TYPE = '1'
        """;

    /// <summary>
    /// The deposits the folio check looks at (FolioCheck): one row per deposit that is
    /// not cancelled, with its first holder, on a folio still in use. Must give back:
    /// Folio, Pan, Dob. An investor has a folio when their PAN is on a row here.
    /// </summary>
    public const string FolioDeposits = """
        SELECT FOLIO AS Folio, PAN1 AS Pan, DOB AS Dob
        FROM FD.dbo.FDR_MST a WITH (NOLOCK)
        INNER JOIN FD.dbo.FOLIO_MST b WITH (NOLOCK) ON a.FOLIO = b.FOLIO_NO AND b.f_Active = 1
        WHERE HOLD_TYPE = '1' AND DEP_STATUS != 'X'
        """;

    /// <summary>
    /// Where a holder's latest KYC data, address and documents are kept, once their
    /// folio is found: the newest row for the PAN, date of birth and folio among the
    /// common KYC table (ORA), the applications submitted through this app (BT) and
    /// the folio master (FHLD). Must give back: source, id.
    /// Unlike the others this is run as it stands, with @pan, @dob and @folio, so it
    /// keeps its own TOP 1 and ORDER BY.
    /// </summary>
    public const string KycSource = """
        SELECT TOP 1
            source, id
        FROM (
            SELECT
                f_CreatedOn AS [date],
                f_Pk_FD_common_Kyc_Data_Dtl_id id,
                'ORA' AS source
            FROM FD.dbo.t_FD_common_Kyc_Data_Dtl_ORA WITH (NOLOCK)
            WHERE
                f_Active = 1 AND
                f_Kyc_PAN = @pan AND
                f_Kyc_DOB = @dob AND
                f_FolioNo = @folio AND
                ISNULL(f_Appl_No, '') <> ''

            UNION ALL

            SELECT
                f_CreatedOn AS [date],
                f_Pk_t_FD_BT_Kyc_Data_Dtl_id id,
                'BT' AS source
            FROM dbo.t_FD_BT_Kyc_Data_Dtl WITH (NOLOCK)
            WHERE
                f_Active = 1 AND
                f_Status = 'APR' AND
                f_Kyc_PAN = @pan AND
                f_Kyc_DOB = @dob AND
                f_FolioNo = @folio

            UNION ALL

            SELECT
                f_FolioCreationDate AS [date],
                '' id,
                'FHLD' AS source
            FROM FD.dbo.t_Fd_Folio_holding WITH (NOLOCK)
            WHERE
                f_Active = 1 AND
                PAN_NO = @pan AND
                f_DOB = @dob AND
                FOLIO_NO = @folio AND
                ISNULL(f_FD_Folio_Holding_Seq, '') <> ''
        ) AS t
        ORDER BY [date] DESC
        """;

    // ----- What a folio's data source holds -------------------------------------
    // Once a folio is found, KycSource says where its latest KYC is kept: the common
    // tables (ORA), the applications submitted through this app (BT) or the folio
    // master (FHLD). The queries below read the KYC details, the address and the
    // documents from the common tables and from BT, by folio; the folio master's
    // address is in Folios. The common tables are
    // built like the BT ones, but for the two codes a document is filed under; only
    // BT rows carry a status, so only they are held to APR.

    /// <summary>
    /// The KYC details the common tables hold, shown on Investor Information so they
    /// are not typed again. Must give back: Folio, SavedOn (the latest row for a folio
    /// is the one used), NameType (Father, Mother or Spouse), ParentName, AnnualIncome,
    /// Occupation, SubOccupation (the names the lists show), MaritalStatus (a code of the
    /// 'maritalStatuses' list), Pep and PepRelated (yes, no or empty), Mobile, Email,
    /// and the mailing address where one is held: MailLine1, MailLine2, MailLine3,
    /// MailCity, MailPinCode.
    /// </summary>
    public const string KycOnCommon = """
        SELECT k.f_FolioNo AS Folio, k.f_CreatedOn AS SavedOn,
               CASE WHEN k.f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN k.f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
                    WHEN k.f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
               COALESCE(k.f_Kyc_FatherFullName, k.f_Kyc_MotherFullName, k.f_Kyc_SpouseFullName, N'') AS ParentName,
               k.f_Kyc_AnnualIncome_Desc AS AnnualIncome, k.f_CustSeg_Type_desc AS Occupation, k.f_CustSeg_Subtype_Desc AS SubOccupation,
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
        WHERE k.f_Active = 1
        """;

    /// <summary>The same KYC details, from the applications submitted through this app. Must give back the same names.</summary>
    public const string KycOnBt = """
        SELECT k.f_FolioNo AS Folio, k.f_CreatedOn AS SavedOn,
               CASE WHEN k.f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN k.f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
                    WHEN k.f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
               COALESCE(k.f_Kyc_FatherFullName, k.f_Kyc_MotherFullName, k.f_Kyc_SpouseFullName, N'') AS ParentName,
               k.f_Kyc_AnnualIncome_Desc AS AnnualIncome, k.f_CustSeg_Type_desc AS Occupation, k.f_CustSeg_Subtype_Desc AS SubOccupation,
               k.f_Kyc_MaritalStatus AS MaritalStatus,
               CASE k.f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
               CASE k.f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated,
               a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email,
               m.f_Add1 AS MailLine1, m.f_Add2 AS MailLine2, m.f_Add3 AS MailLine3,
               m.f_AddCity_Desc AS MailCity, m.f_AddPin AS MailPinCode
        FROM dbo.t_FD_BT_Kyc_Data_Dtl k
        LEFT JOIN dbo.t_FD_BT_Address_Dtl a
            ON a.f_Appl_No = k.f_Appl_No AND a.f_Holder_Type = k.f_Holder_Type
           AND a.f_AddType_Code = 'PER' AND a.f_Status = 'APR' AND a.f_Active = 1
        LEFT JOIN dbo.t_FD_BT_Address_Dtl m
            ON m.f_Appl_No = k.f_Appl_No AND m.f_Holder_Type = k.f_Holder_Type
           AND m.f_AddType_Code = 'MAIL' AND m.f_Status = 'APR' AND m.f_Active = 1
        WHERE k.f_Status = 'APR' AND k.f_Active = 1
        """;

    /// <summary>
    /// The permanent address the common tables hold for a folio. Must give back: Folio,
    /// SavedOn (the latest row is the one used), Address: its parts with a comma between
    /// them, a part the row does not hold left out.
    /// </summary>
    public const string AddressOnCommon = """
        SELECT a.f_FolioNo AS Folio, a.f_CreatedOn AS SavedOn,
               CONCAT(NULLIF(a.f_Add1, '') + ', ', NULLIF(a.f_Add2, '') + ', ', NULLIF(a.f_Add3, '') + ', ',
                      NULLIF(a.f_AddCity_Desc, '') + ', ', NULLIF(a.f_AddState_Desc, '') + ', ',
                      NULLIF(a.f_AddDistrict_Desc, '') + ', ', a.f_AddPin) AS Address
        FROM FD.dbo.t_FD_common_Address_Dtl_ORA a WITH (NOLOCK)
        WHERE a.f_AddType_Code = 'PER' AND a.f_Active = 1
        """;

    /// <summary>
    /// The same, from the applications submitted through this app. This app writes a
    /// permanent address across f_Add1 to f_Add3, broken at its spaces, so the three
    /// lines are put back together with a space; whatever follows them takes a comma.
    /// </summary>
    public const string AddressOnBt = """
        SELECT a.f_FolioNo AS Folio, a.f_CreatedOn AS SavedOn,
               CONCAT(NULLIF(a.f_Add1, '') + ' ', NULLIF(a.f_Add2, '') + ' ', NULLIF(a.f_Add3, '') + ', ',
                      NULLIF(a.f_AddCity_Desc, '') + ', ', NULLIF(a.f_AddState_Desc, '') + ', ',
                      NULLIF(a.f_AddDistrict_Desc, '') + ', ', a.f_AddPin) AS Address
        FROM dbo.t_FD_BT_Address_Dtl a
        WHERE a.f_AddType_Code = 'PER' AND a.f_Status = 'APR' AND a.f_Active = 1
        """;

    /// <summary>
    /// The documents the common tables hold for a folio, one row each. Must give back:
    /// Folio, SubTypeCode (the document's sub-type, as 'documentSubTypes' codes it),
    /// Verified (1 when what was read off the copy was confirmed). There the type is in
    /// f_Category and the sub-type in f_Doc_Type. Only the PAN copy, the photograph and
    /// the proofs of address are looked for among them.
    /// </summary>
    public const string DocumentsOnCommon = """
        SELECT d.f_FolioNo AS Folio, d.f_Doc_Type AS SubTypeCode,
               CASE WHEN d.f_isocrdataverified = 1 OR d.f_IsidfyDocDataVerified = 1 THEN 1 ELSE 0 END AS Verified
        FROM FD.dbo.t_FD_common_KYC_document_ORA d WITH (NOLOCK)
        WHERE d.f_Active = 1
        """;

    /// <summary>
    /// The same, from the applications submitted through this app: the copies filed
    /// with them. A row with no file is a document that was already on record then.
    /// </summary>
    public const string DocumentsOnBt = """
        SELECT d.f_FolioNo AS Folio, d.f_Doc_Sub_Type_Code AS SubTypeCode,
               CASE WHEN d.f_isocrdataverified = 1 OR d.f_IsidfyDocDataVerified = 1 THEN 1 ELSE 0 END AS Verified
        FROM dbo.t_FD_BT_KYC_document d
        WHERE d.f_Status = 'APR' AND d.f_Active = 1 AND d.f_Doc_Filepath IS NOT NULL
        """;

    /// <summary>The brokers an application can be sourced under. Must give back: Code, Name.</summary>
    public const string Brokers = """
        SELECT b.BROKER_CODE AS Code, b.NAME AS Name
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BTP_Broker_Mst b
        WHERE b.REC_STATUS = 'A'
        """;

    // ----- The staff a sourcing mode takes ---------------------------------------
    // A sourcing mode names its staff rule in the 'staff' setting of its row in
    // 'sourcingModes' (t_Unotp_Ref_List): branch, mfis or mflEx. Each rule is one
    // query below; a mode with no rule takes any employee in service. Each must
    // give back: Code, Name.

    /// <summary>Every employee in service: where a sourcing mode names no staff rule.</summary>
    public const string Staff = """
        SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
        FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
        WHERE e.ACTIVE = 'A'
        """;

    /// <summary>MMFSS - BRANCH (staff rule 'branch'): pay group 1033 outside the investment and deposit departments, and the retail branch.</summary>
    public const string StaffForBranch = """
        SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
        FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
        WHERE e.ACTIVE = 'A'
          AND (
                (e.DEPARTMENT_NAME NOT IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
                 AND e.PAYGRP IN ('1033'))
                OR e.DEPARTMENT_NAME = 'RETAIL BRANCH'
              )
        """;

    /// <summary>MFIS / MFIS-DFB / FD (staff rule 'mfis'): the investment and deposit departments.</summary>
    public const string StaffForMfis = """
        SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
        FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
        WHERE e.ACTIVE = 'A'
          AND e.DEPARTMENT_NAME IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
        """;

    /// <summary>MFL-EX (staff rule 'mflEx'): pay groups 1031, 1032, 1033 and 1059, and the retail branch.</summary>
    public const string StaffForMflEx = """
        SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
        FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
        WHERE e.ACTIVE = 'A'
          AND (
                e.PAYGRP IN ('1031', '1032', '1033', '1059')
                OR e.DEPARTMENT_NAME = 'RETAIL BRANCH'
              )
        """;

    /// <summary>The bank branches, by IFSC. Must give back: Ifsc, Bank, Branch, Micr.</summary>
    public const string BankBranches = """
        SELECT b.f_Ifsc AS Ifsc, b.f_Bank AS Bank, b.f_Branch AS Branch, b.f_Micr AS Micr
        FROM dbo.t_Unotp_Ifsc_Mst b
        WHERE b.f_Active = 1
        """;

    /// <summary>The PIN codes. Must give back: PinCode, District, State.</summary>
    public const string PinCodes = """
        SELECT p.f_Pin_Code AS PinCode, p.f_District AS District, p.f_State AS State
        FROM dbo.t_Unotp_Pincode_Mst p
        WHERE p.f_Active = 1
        """;

    /// <summary>
    /// The Axis CMS locations a cheque is presented at. Must give back: Code, Name.
    /// Put the Axis CMS master's own table and columns here. Until then this reads
    /// the development rows kept under 'cmsLocations' in t_Unotp_Ref_List.
    /// </summary>
    public const string CmsLocations = """
        SELECT c.f_Code AS Code, c.f_Name AS Name
        FROM dbo.t_Unotp_Ref_List c
        WHERE c.f_List = 'cmsLocations' AND c.f_Active = 1
        """;

    /// <summary>
    /// The banks the payment gateway takes for online payment. Must give back: Code
    /// (the bank's code, the first four letters of its IFSCs), Name. Put the gateway
    /// bank master's own table and columns here. Until then this reads the
    /// development rows kept under 'gatewayBanks' in t_Unotp_Ref_List.
    /// </summary>
    public const string GatewayBanks = """
        SELECT g.f_Code AS Code, g.f_Name AS Name
        FROM dbo.t_Unotp_Ref_List g
        WHERE g.f_List = 'gatewayBanks' AND g.f_Active = 1
        """;

    // ----- The rate cards -----------------------------------------------------------
    // Two tables of the same structure: a branch user's card, which also carries the
    // employee and special schemes (and their extra tenures), and a partner's, which
    // does not. Which one a deposit is quoted from follows who is signed in.

    /// <summary>
    /// A branch user's rate card, one row per scheme code. Must give back: Category, Mode (AF or R),
    /// Scheme, SchemeCode, InterestFreq, TenureMonths, Rate, MinAmount, MaxAmount,
    /// FromDate, ToDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out.
    /// </summary>
    public const string RateCard = """
        SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme, RTRIM(r.SCHEME_CODE) AS SchemeCode,
               RTRIM(r.INTEREST_FREQ) AS InterestFreq, TRY_CAST(r.PERIOD AS INT) AS TenureMonths, TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
               CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
               CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
               r.FROM_DATE AS FromDate, r.TO_DATE AS ToDate, r.SCHEME_ID AS SchemeId
        FROM dbo.t_FD_BOTC_SCHEME r
        """;

    /// <summary>
    /// A partner's rate card: the same structure, without the employee and special
    /// schemes. Must give back: Category, Mode (AF or R),
    /// Scheme, SchemeCode, InterestFreq, TenureMonths, Rate, MinAmount, MaxAmount,
    /// FromDate, ToDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out.
    /// </summary>
    public const string RateCardForPartners = """
        SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme, RTRIM(r.SCHEME_CODE) AS SchemeCode,
               RTRIM(r.INTEREST_FREQ) AS InterestFreq, TRY_CAST(r.PERIOD AS INT) AS TenureMonths, TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
               CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
               CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
               r.FROM_DATE AS FromDate, r.TO_DATE AS ToDate, r.SCHEME_ID AS SchemeId
        FROM dbo.FD_SCHEME r
        """;
}
