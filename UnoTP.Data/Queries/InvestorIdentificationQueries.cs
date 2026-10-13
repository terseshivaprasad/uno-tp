namespace UnoTP.Data;

/// <summary>
/// The SQL Investor Identification runs when a record is checked, in the order the
/// screen asks: is the PAN or the folio on a deposit, which holder of the folio is it,
/// and what the FD system already holds for that holder - their KYC details, address
/// and documents. Nothing but SQL is in this file.
///
/// A holder is looked up the same way wherever the app meets one, so Renew FD runs
/// these too for the holder of a deposit, and Investor Information runs LatestKyc to
/// show the details a holder need not type again.
///
/// What else the screen does is not here, because another screen runs it as well:
///   - the applications to continue: ApplicationQueries.Drafts (the dashboard's too);
///     Continue reads the one picked with ApplicationQueries.Header and
///     ApplicationTableQueries.WholeApplication, as every step does, and Cancel is
///     ApplicationQueries.CancelDraft;
///   - the holder's deposits to renew: RenewalQueries (Renew FD's too);
///   - Proceed, which opens the application: ApplicationQueries.NextApplicationNo and
///     InsertApplication (a renewal's too). A holder on a folio has their copies on
///     record read first, with DocumentCopies below;
///   - the lists and the settings: ListQueries.
///
///   - Keep the names after AS: they are what the app reads. A column may come from
///     any table or expression, so long as it comes back under the same name.
///   - A table in another database on the same server is written by its full name:
///     OtherDb.dbo.Table.
///   - A query carries its key as a parameter, written in its own WHERE (@Folio,
///     @Pan, @Dob). It is run as it stands, so the table is read by that key and
///     never whole. Keep the parameter names, and compare them with the table's own
///     column as it is (f.FOLIO_NO = @Folio): a column wrapped in a function cannot
///     be found by its index.
///   - No ORDER BY in a table's own query: the latest row is picked by the queries
///     at the end of this file. KycSource alone keeps its own.
/// </summary>
internal static class InvestorIdentificationQueries
{
    // ----- 1. Is the PAN or the folio on a deposit --------------------------------

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
    /// The folios a deposits query finds, with their first holder's PAN and date of birth.
    /// </summary>
    public static string FolioDeposits(string deposits) =>
        $"SELECT d.Folio, d.Pan, d.Dob FROM ({deposits}) d ORDER BY d.Folio";

    // ----- 2. A PAN with no deposit: is it the first holder of a folio ------------

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
    /// The folio a PAN is the first holder of, the lowest should there be more than one.
    /// </summary>
    public const string FolioOfFirstHolder = $"SELECT TOP (1) h.Folio FROM ({FirstHolders}) h ORDER BY h.Folio";

    // ----- 3. The holder found: their row on the folio, and where their KYC is ----

    /// <summary>
    /// One holder of a folio, from the FD system's folio master. The master has a row
    /// for every holder of a folio - the first holder (HOLD_TYPE 1) and each joint
    /// holder - all under the same folio number, so a folio number alone is not one
    /// row. This reads the row of the holder whose PAN is given (@Folio, @Pan); with
    /// no PAN given (@Pan empty), the first holder's. Must give back: Pan, Dob,
    /// Folio, Name, Gender, Address, Line1, Line2, Line3, City, District, State, PinCode,
    /// DocPhoto, DocPoa, Note, Source.
    /// The address is its parts with a comma between them; a part the folio does not
    /// hold is left out. The parts are given apart as well: they are what is written
    /// against a new application of the holder's, and the first line, the city and
    /// the PIN code are what an address on record must have.
    /// DocPhoto and DocPoa say whether the master itself holds the holder's
    /// photograph and proof of address (1 or 0). There is no DocPan: a holder on a
    /// folio is never asked for the PAN copy again.
    /// </summary>
    public const string Folios = """
        SELECT f.PAN_NO AS Pan, f.f_DOB AS Dob, f.FOLIO_NO AS Folio, f.NAME AS Name, f.F_Gender AS Gender,
               CONCAT(f.f_Address1 + ', ', f.f_Address2 + ', ', f.f_Address3 + ', ', f.F_City + ', ',
                      f.f_stateName + ', ', f.F_District + ', ', f.f_Pincode) AS Address,
               f.f_Address1 AS Line1, f.f_Address2 AS Line2, f.f_Address3 AS Line3,
               f.F_City AS City, f.F_District AS District, f.f_stateName AS State, f.f_Pincode AS PinCode,
               0 AS DocPhoto, 0 AS DocPoa, '' AS Note, 'FHLD' AS Source
        FROM FD.dbo.t_Fd_Folio_holding f
        WHERE f.f_Active = 1 AND f.FOLIO_NO = @Folio
          AND (f.PAN_NO = @Pan OR (@Pan = '' AND f.HOLD_TYPE = '1'))
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
    /// latest row for the holder is the one used), NamePrefix (MR, MRS or MS, in any case; Miss on older rows: the holder's
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
    /// out; and the parts apart - Line1, Line2, Line3, City, District, State, PinCode.
    /// </summary>
    public const string AddressOnCommon = """
        SELECT a.f_CreatedOn AS SavedOn,
               CONCAT(NULLIF(a.f_Add1, '') + ', ', NULLIF(a.f_Add2, '') + ', ', NULLIF(a.f_Add3, '') + ', ',
                      NULLIF(a.f_AddCity_Desc, '') + ', ', NULLIF(a.f_AddState_Desc, '') + ', ',
                      NULLIF(a.f_AddDistrict_Desc, '') + ', ', a.f_AddPin) AS Address,
               a.f_Add1 AS Line1, a.f_Add2 AS Line2, a.f_Add3 AS Line3,
               a.f_AddCity_Desc AS City, a.f_AddDistrict_Desc AS District, a.f_AddState_Desc AS State, a.f_AddPin AS PinCode
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
               a.f_Add1 AS Line1, a.f_Add2 AS Line2, a.f_Add3 AS Line3,
               a.f_AddCity_Desc AS City, a.f_AddDistrict_Desc AS District, a.f_AddState_Desc AS State, a.f_AddPin AS PinCode
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
    /// 'documentSubTypes' codes it), FileName and FilePath (the copy on record, written
    /// against a new application of the holder's where they upload none), SavedOn (the
    /// newest row of a kind is the one used; here the KYC row's date). There the type is
    /// in f_Category and the sub-type in f_Doc_Type. Only the photograph and the proofs of address are looked for among
    /// them, and one that is there counts whether it was verified or not. A row with
    /// no file name or no file path is not a document on record: there is no copy to
    /// show for it, so the holder is asked to upload it.
    /// </summary>
    public const string DocumentsOnCommon = """
        SELECT d.f_Doc_Type AS SubTypeCode, d.f_Doc_FileName AS FileName, d.f_Doc_Filepath AS FilePath, k.f_CreatedOn AS SavedOn
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
    /// Must give back the same names.
    /// </summary>
    public const string DocumentsOnBt = """
        SELECT d.f_Doc_Sub_Type_Code AS SubTypeCode, d.f_Doc_FileName AS FileName, d.f_Doc_Filepath AS FilePath, d.f_CreatedDate AS SavedOn
        FROM dbo.t_FD_BT_KYC_document d
        JOIN dbo.t_FD_BT_Kyc_Data_Dtl k
            ON k.f_Appl_No = d.f_Appl_No AND k.f_Holder_Type = d.f_Holder_Type_Code AND k.f_Status = 'APR' AND k.f_Active = 1
        WHERE d.f_Status = 'APR' AND d.f_Active = 1
          AND ISNULL(d.f_Doc_FileName, '') <> '' AND ISNULL(d.f_Doc_Filepath, '') <> ''
          AND d.f_FolioNo = @Folio AND k.f_Kyc_PAN = @Pan AND k.f_Kyc_DOB = @Dob
        """;

    // ----- 4. How the queries above are asked: the latest row, the copies on record

    /// <summary>
    /// The name prefix on a holder's latest KYC row. kycDetails is the source's KYC details query.
    /// </summary>
    public static string LatestNamePrefix(string kycDetails) =>
        $"SELECT TOP (1) k.NamePrefix FROM ({kycDetails}) k ORDER BY k.SavedOn DESC";

    /// <summary>
    /// A holder's latest KYC row. kycDetails is the source's KYC details query.
    /// </summary>
    public static string LatestKyc(string kycDetails) =>
        $"SELECT TOP (1) * FROM ({kycDetails}) k ORDER BY k.SavedOn DESC";

    /// <summary>
    /// A holder's latest address. addresses is the source's address query.
    /// </summary>
    public static string LatestAddress(string addresses) =>
        $"SELECT TOP (1) a.Address, a.Line1, a.Line2, a.Line3, a.City, a.District, a.State, a.PinCode FROM ({addresses}) a ORDER BY a.SavedOn DESC";

    /// <summary>
    /// The sub types of the documents a holder has on record. documents is the source's documents query.
    /// </summary>
    public static string DocumentSubTypes(string documents) =>
        $"SELECT DISTINCT d.SubTypeCode FROM ({documents}) d";

    /// <summary>
    /// The copies a holder has on record, the newest first. documents is the source's documents query.
    /// </summary>
    public static string DocumentCopies(string documents) =>
        $"SELECT d.SubTypeCode, d.FileName, d.FilePath FROM ({documents}) d ORDER BY d.SavedOn DESC";
}
