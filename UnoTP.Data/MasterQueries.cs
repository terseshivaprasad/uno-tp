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
///   - A query that finds rows by a key carries that key as a parameter, written in
///     its own WHERE (@Folio, @Pan, @Dob, @Pin). It is run as it stands, so the table
///     is read by that key and never whole. Keep the parameter names, and compare
///     them with the table's own column as it is (f.FOLIO_NO = @Folio): a column
///     wrapped in a function cannot be found by its index.
///   - No ORDER BY here: the app sorts what a query gives back.
///   - The registers searched as they are typed (brokers, staff, bank branches, Axis
///     CMS branches) take two parameters, both written in their own WHERE: the key
///     of one row (@Code or @Ifsc), and what was typed - @Search, a LIKE pattern
///     (%word%word%), or for the bank @Words, the words themselves. The app gives
///     one and leaves the other empty (NULL): a row is found by its key, or a
///     handful are found by what was typed. Neither ever brings back the whole register.
/// </summary>
internal static class MasterQueries
{
    /// <summary>
    /// One holder of a folio, from the FD system's folio master. The master has a row
    /// for every holder of a folio - the first holder (HOLD_TYPE 1) and each joint
    /// holder - all under the same folio number, so a folio number alone is not one
    /// row. This reads the row of the holder whose PAN is given (@Folio, @Pan); with
    /// no PAN given (@Pan empty), the first holder's. Must give back: Pan, Dob,
    /// Folio, Name, Gender, Address, Line1, PinCode, DocPhoto, DocPoa, Note, Source.
    /// The address is its parts with a comma between them; a part the folio does not
    /// hold is left out. Line1 and PinCode are its first line and PIN code, apart.
    /// DocPhoto and DocPoa say whether the master itself holds the holder's
    /// photograph and proof of address (1 or 0). There is no DocPan: a holder on a
    /// folio is never asked for the PAN copy again.
    /// </summary>
    public const string Folios = """
        SELECT f.PAN_NO AS Pan, f.f_DOB AS Dob, f.FOLIO_NO AS Folio, f.NAME AS Name, f.F_Gender AS Gender,
               CONCAT(f.f_Address1 + ', ', f.f_Address2 + ', ', f.f_Address3 + ', ', f.F_City + ', ',
                      f.f_stateName + ', ', f.F_District + ', ', f.f_Pincode) AS Address,
               f.f_Address1 AS Line1, f.f_Pincode AS PinCode,
               0 AS DocPhoto, 0 AS DocPoa, '' AS Note, 'FHLD' AS Source
        FROM FD.dbo.t_Fd_Folio_holding f
        WHERE f.f_Active = 1 AND f.FOLIO_NO = @Folio
          AND (f.PAN_NO = @Pan OR (@Pan = '' AND f.HOLD_TYPE = '1'))
        """;

    /// <summary>
    /// The folios a PAN (@Pan) is the first holder of, in the folio master: asked for a
    /// PAN the folio check found no deposit for, so that a holder who already has a
    /// folio is not taken as new. Must give back: Pan, Folio.
    /// </summary>
    public const string FirstHolders = """
        SELECT f.PAN_NO AS Pan, f.FOLIO_NO AS Folio
        FROM FD.dbo.t_Fd_Folio_holding f WITH (NOLOCK)
        WHERE f.f_Active = 1 AND f.HOLD_TYPE = '1' AND f.PAN_NO = @Pan
        """;

    /// <summary>
    /// The deposits the folio check looks at (FolioCheck) for a PAN (@Pan): one row per
    /// deposit that is not cancelled, with its first holder, on a folio still in use.
    /// Must give back: Folio, Pan, Dob. An investor has a folio when their PAN is on a row here.
    /// FDR_MST holds primary holders only; the holder type is the folio holding table's,
    /// whose first-holder row (HOLD_TYPE 1) is the one joined.
    /// </summary>
    public const string FolioDepositsByPan = """
        SELECT FOLIO AS Folio, PAN1 AS Pan, DOB AS Dob
        FROM FD.dbo.FDR_MST a WITH (NOLOCK)
        INNER JOIN FD.dbo.t_Fd_Folio_holding b WITH (NOLOCK) ON a.FOLIO = b.FOLIO_NO AND b.f_Active = 1
        WHERE b.HOLD_TYPE = '1' AND DEP_STATUS != 'X' AND PAN1 = @Pan
        """;

    /// <summary>The same deposits, for a folio number (@Folio). Must give back the same names.</summary>
    public const string FolioDepositsByFolio = """
        SELECT FOLIO AS Folio, PAN1 AS Pan, DOB AS Dob
        FROM FD.dbo.FDR_MST a WITH (NOLOCK)
        INNER JOIN FD.dbo.t_Fd_Folio_holding b WITH (NOLOCK) ON a.FOLIO = b.FOLIO_NO AND b.f_Active = 1
        WHERE b.HOLD_TYPE = '1' AND DEP_STATUS != 'X' AND a.FOLIO = @Folio
        """;

    /// <summary>
    /// Where a holder's latest KYC data, address and documents are kept, once their
    /// folio is found: the newest row for the PAN, date of birth and folio among the
    /// common KYC table (ORA), the applications submitted through this app (BT) and
    /// the folio master (FHLD). Must give back: source, as its first column; the id
    /// beside it is not read.
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
    // documents from the common tables and from BT; the folio master's address is
    // in Folios. The common tables are built like the BT ones, but for the two codes
    // a document is filed under; only BT rows carry a status, so only they are held
    // to APR.
    //
    // A folio can have more than one holder, and every holder's rows carry the same
    // folio number. So each query reads only the rows of one folio, PAN and date of
    // birth (@Folio, @Pan, @Dob), as KycSource does. Only the KYC table holds a PAN
    // and a date of birth: an address or a document row is matched through the KYC
    // row of the same application and holder type.

    /// <summary>
    /// The KYC details the common tables hold, shown on Investor Information so they
    /// are not typed again, for one holder (@Folio, @Pan, @Dob). Must give back: SavedOn (the
    /// latest row for the holder is the one used), NamePrefix (Mr, Mrs, Miss or Ms: the holder's
    /// gender, where the folio master holds none), NameType (Father, Mother or Spouse), ParentName, AnnualIncome,
    /// Occupation, SubOccupation (the names the lists show), MaritalStatus (a code of the
    /// 'maritalStatuses' list), Pep and PepRelated (yes, no or empty), Mobile, Email,
    /// and the mailing address where one is held: MailLine1, MailLine2, MailLine3,
    /// MailCity, MailPinCode.
    /// </summary>
    public const string KycOnCommon = """
        SELECT k.f_CreatedOn AS SavedOn, k.f_Kyc_NamePrefix AS NamePrefix,
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
        WHERE k.f_Active = 1 AND k.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    /// <summary>The same KYC details, from the applications submitted through this app. Must give back the same names.</summary>
    public const string KycOnBt = """
        SELECT k.f_CreatedOn AS SavedOn, k.f_Kyc_NamePrefix AS NamePrefix,
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
        WHERE k.f_Status = 'APR' AND k.f_Active = 1 AND k.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    /// <summary>
    /// The permanent address the common tables hold for one holder of a folio (@Folio,
    /// @Pan, @Dob; the PAN and date of birth are on the KYC row of the same application
    /// and holder type). Must give back: SavedOn (the latest row is the one used),
    /// Address: its parts with a comma between them, a part the row does not hold left
    /// out; and Line1 and PinCode, its first line and PIN code, apart.
    /// </summary>
    public const string AddressOnCommon = """
        SELECT a.f_CreatedOn AS SavedOn,
               CONCAT(NULLIF(a.f_Add1, '') + ', ', NULLIF(a.f_Add2, '') + ', ', NULLIF(a.f_Add3, '') + ', ',
                      NULLIF(a.f_AddCity_Desc, '') + ', ', NULLIF(a.f_AddState_Desc, '') + ', ',
                      NULLIF(a.f_AddDistrict_Desc, '') + ', ', a.f_AddPin) AS Address,
               a.f_Add1 AS Line1, a.f_AddPin AS PinCode
        FROM FD.dbo.t_FD_common_Address_Dtl_ORA a WITH (NOLOCK)
        JOIN FD.dbo.t_FD_common_Kyc_Data_Dtl_ORA k WITH (NOLOCK)
            ON k.f_Appl_No = a.f_Appl_No AND k.f_Holder_Type = a.f_Holder_Type AND k.f_Active = 1
        WHERE a.f_AddType_Code = 'PER' AND a.f_Active = 1
          AND a.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    /// <summary>
    /// The same, from the applications submitted through this app. This app writes a
    /// permanent address across f_Add1 to f_Add3, broken at its spaces, so the three
    /// lines are put back together with a space; whatever follows them takes a comma.
    /// </summary>
    public const string AddressOnBt = """
        SELECT a.f_CreatedOn AS SavedOn,
               CONCAT(NULLIF(a.f_Add1, '') + ' ', NULLIF(a.f_Add2, '') + ' ', NULLIF(a.f_Add3, '') + ', ',
                      NULLIF(a.f_AddCity_Desc, '') + ', ', NULLIF(a.f_AddState_Desc, '') + ', ',
                      NULLIF(a.f_AddDistrict_Desc, '') + ', ', a.f_AddPin) AS Address,
               a.f_Add1 AS Line1, a.f_AddPin AS PinCode
        FROM dbo.t_FD_BT_Address_Dtl a
        JOIN dbo.t_FD_BT_Kyc_Data_Dtl k
            ON k.f_Appl_No = a.f_Appl_No AND k.f_Holder_Type = a.f_Holder_Type AND k.f_Status = 'APR' AND k.f_Active = 1
        WHERE a.f_AddType_Code = 'PER' AND a.f_Status = 'APR' AND a.f_Active = 1
          AND a.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    /// <summary>
    /// The documents the common tables hold for one holder of a folio (@Folio, @Pan,
    /// @Dob; the PAN and date of birth are on the KYC row of the same application and
    /// holder type), one row each. Must give back: SubTypeCode (the document's sub-type, as
    /// 'documentSubTypes' codes it). There the type is in f_Category and the sub-type in
    /// f_Doc_Type. Only the photograph and the proofs of address are looked for among
    /// them, and one that is there counts whether it was verified or not. A row with
    /// no file name or no file path is not a document on record: there is no copy to
    /// show for it, so the holder is asked to upload it.
    /// </summary>
    public const string DocumentsOnCommon = """
        SELECT d.f_Doc_Type AS SubTypeCode
        FROM FD.dbo.t_FD_common_KYC_document_ORA d WITH (NOLOCK)
        JOIN FD.dbo.t_FD_common_Kyc_Data_Dtl_ORA k WITH (NOLOCK)
            ON k.f_Appl_No = d.f_Appl_No AND k.f_Holder_Type = d.f_Holder_Type_Code AND k.f_Active = 1
        WHERE d.f_Active = 1
          AND ISNULL(d.f_Doc_FileName, '') <> '' AND ISNULL(d.f_Doc_Filepath, '') <> ''
          AND d.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    /// <summary>
    /// The same, from the applications submitted through this app: the copies filed
    /// with them. A row with no file name or no file path is left out here too.
    /// </summary>
    public const string DocumentsOnBt = """
        SELECT d.f_Doc_Sub_Type_Code AS SubTypeCode
        FROM dbo.t_FD_BT_KYC_document d
        JOIN dbo.t_FD_BT_Kyc_Data_Dtl k
            ON k.f_Appl_No = d.f_Appl_No AND k.f_Holder_Type = d.f_Holder_Type_Code AND k.f_Status = 'APR' AND k.f_Active = 1
        WHERE d.f_Status = 'APR' AND d.f_Active = 1
          AND ISNULL(d.f_Doc_FileName, '') <> '' AND ISNULL(d.f_Doc_Filepath, '') <> ''
          AND d.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    /// <summary>
    /// The brokers an application can be sourced under: one by its code (@Code), or
    /// those whose code and name hold what was typed (@Search). Must give back: Code, Name.
    /// </summary>
    public const string Brokers = """
        SELECT b.BROKER_CODE AS Code, b.NAME AS Name
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BTP_Broker_Mst b
        WHERE b.REC_STATUS = 'A'
          AND (@Code IS NULL OR b.BROKER_CODE = @Code)
          AND (@Search IS NULL OR b.BROKER_CODE + ' ' + b.NAME LIKE @Search)
        """;

    // ----- The staff a sourcing mode takes ---------------------------------------
    // A sourcing mode names its staff rule in the 'staff' setting of its row in
    // 'sourcingModes' (t_Unotp_Ref_List): branch, mfis or mflEx. Each rule is one
    // query below; a mode with no rule takes any employee in service. Each finds one
    // employee by code (@Code), or those whose code and name hold what was typed
    // (@Search), and must give back: Code, Name.

    /// <summary>Every employee in service: where a sourcing mode names no staff rule.</summary>
    public const string Staff = """
        SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
        FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
        WHERE e.ACTIVE = 'A'
          AND (@Code IS NULL OR e.EMPCODE = @Code)
          AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
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
          AND (@Code IS NULL OR e.EMPCODE = @Code)
          AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        """;

    /// <summary>MFIS / MFIS-DFB / FD (staff rule 'mfis'): the investment and deposit departments.</summary>
    public const string StaffForMfis = """
        SELECT DISTINCT e.EMPCODE AS Code, e.EMPNAME AS Name
        FROM MMFSL_DATA_IMPORTED.dbo.T_PA_VIEW_EMPLOYEE e WITH (NOLOCK)
        WHERE e.ACTIVE = 'A'
          AND e.DEPARTMENT_NAME IN ('INVESTMENT SOLUTIONS', 'FIXED DEPOSIT', 'INVESTMENT SOLUTIONS - DFB')
          AND (@Code IS NULL OR e.EMPCODE = @Code)
          AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
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
          AND (@Code IS NULL OR e.EMPCODE = @Code)
          AND (@Search IS NULL OR e.EMPCODE + ' ' + e.EMPNAME LIKE @Search)
        """;

    /// <summary>
    /// The bank branches, from the FD system's bank master: one by its IFSC (@Ifsc),
    /// or those found by what was typed (@Words: the words typed, a space between
    /// them). Must give back: Ifsc, Bank, Branch, Micr. A branch is found when every
    /// word typed is in its search key (f_Searchkey), in any order - the rule the FD
    /// system's own bank search goes by.
    /// </summary>
    public const string BankBranches = """
        SELECT DISTINCT b.NEFT_CODE AS Ifsc, b.BANK_NAME AS Bank, b.BANK_BRANCH AS Branch, b.MICR_CODE AS Micr
        FROM dbo.T_FD_RBI_BANK_MSTR b WITH (NOLOCK)
        WHERE (@Ifsc IS NULL OR b.NEFT_CODE = @Ifsc)
          AND (@Words IS NULL OR NOT EXISTS (
                SELECT 1 FROM STRING_SPLIT(@Words, ' ') word
                WHERE b.f_Searchkey NOT LIKE '%' + word.value + '%'))
        """;

    /// <summary>One PIN code (@Pin). Must give back: PinCode, District, State.</summary>
    public const string PinCodes = """
        SELECT p.f_Pin_Code AS PinCode, p.f_District AS District, p.f_State AS State
        FROM dbo.t_Unotp_Pincode_Mst p
        WHERE p.f_Active = 1 AND p.f_Pin_Code = @Pin
        """;

    /// <summary>
    /// The Axis CMS branches a cheque is presented at, from the FD system's Axis CMS
    /// branch master, as usp_FD_BT_Get_User_AxisCMSBranch reads them: one by its code
    /// (@Code, the branch's SOL id), or those whose label holds what was typed
    /// (@Search). Must give back: Code, Name, Label (the text the search looks in and
    /// shows: the branch's name, location and PIN code), State and District (the
    /// search lists them in that order).
    /// </summary>
    public const string CmsLocations = """
        SELECT CAST(c.f_Sol_Id AS VARCHAR(50)) AS Code, c.f_Branch_Name AS Name, c.f_LABEL AS Label,
               c.f_State AS State, c.f_District AS District
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_Axis_CMS_Branch_Mst c WITH (NOLOCK)
        WHERE c.f_Active = 1
          AND (@Code IS NULL OR c.f_Sol_Id = @Code)
          AND (@Search IS NULL OR c.f_LABEL LIKE @Search)
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
    // On both, a row is in effect while its TO_DATE is empty: a row with a TO_DATE
    // has been closed, and is not read.

    /// <summary>
    /// A branch user's rate card, one row per scheme code in effect. Must give back: Category, Mode (AF or R),
    /// Scheme, SchemeCode, InterestFreq, TenureMonths, Rate, MinAmount, MaxAmount,
    /// FromDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out.
    /// </summary>
    public const string RateCard = """
        SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme, RTRIM(r.SCHEME_CODE) AS SchemeCode,
               RTRIM(r.INTEREST_FREQ) AS InterestFreq, TRY_CAST(r.PERIOD AS INT) AS TenureMonths, TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
               CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
               CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
               r.FROM_DATE AS FromDate, r.SCHEME_ID AS SchemeId
        FROM dbo.t_FD_BOTC_SCHEME r
        WHERE r.TO_DATE IS NULL
        """;

    /// <summary>
    /// A partner's rate card: the same structure, without the employee and special
    /// schemes, one row per scheme code in effect. Must give back: Category, Mode (AF or R),
    /// Scheme, SchemeCode, InterestFreq, TenureMonths, Rate, MinAmount, MaxAmount,
    /// FromDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out.
    /// </summary>
    public const string RateCardForPartners = """
        SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme, RTRIM(r.SCHEME_CODE) AS SchemeCode,
               RTRIM(r.INTEREST_FREQ) AS InterestFreq, TRY_CAST(r.PERIOD AS INT) AS TenureMonths, TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
               CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
               CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
               r.FROM_DATE AS FromDate, r.SCHEME_ID AS SchemeId
        FROM dbo.FD_SCHEME r
        WHERE r.TO_DATE IS NULL
        """;
}
