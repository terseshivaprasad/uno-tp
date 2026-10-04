namespace UnoTP.Data;

/// <summary>
/// The SQL that reads each big table the app only looks rows up in: folios, brokers,
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
    /// The investor folios. Must give back: Pan, Dob, Folio, Name, Gender, Address, DocPan,
    /// DocPhoto, DocPoa, Note, Source, Compliant. Compliant is 1 for a folio whose KYC is
    /// complete: Investor Information then opens with the details FolioKyc gives for it.
    /// </summary>
    public const string Folios = """
        SELECT f.c_Pan AS Pan, f.d_Dob AS Dob, f.c_Folio AS Folio, f.c_Name AS Name, f.c_Gender AS Gender, f.c_Address AS Address,
               f.f_Doc_Pan AS DocPan, f.f_Doc_Photo AS DocPhoto, f.f_Doc_Poa AS DocPoa, f.c_Note AS Note, f.c_Source AS Source,
               f.f_Kyc_Compliant AS Compliant
        FROM dbo.t_Unotp_Investor_Folio f
        WHERE f.f_Active = 1
        """;

    /// <summary>
    /// The KYC details held against a folio, shown on Investor Information for a
    /// compliant folio so they are not typed again. Must give back: Folio, SavedOn (the
    /// latest row for a folio is the one used), NameType (Father, Mother or Spouse),
    /// ParentName, AnnualIncome, Occupation, SubOccupation (the names the lists show),
    /// MaritalStatus (a code of the 'maritalStatuses' list), Pep and PepRelated
    /// (yes, no or empty), Mobile, Email. This reads the holder's rows on the
    /// applications already submitted for the folio; put the folio's own KYC tables
    /// here where they are others.
    /// </summary>
    public const string FolioKyc = """
        SELECT k.f_FolioNo AS Folio, k.f_CreatedOn AS SavedOn,
               CASE WHEN k.f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN k.f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
                    WHEN k.f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
               COALESCE(k.f_Kyc_FatherFullName, k.f_Kyc_MotherFullName, k.f_Kyc_SpouseFullName, N'') AS ParentName,
               k.f_Kyc_AnnualIncome_Desc AS AnnualIncome, k.f_CustSeg_Type_desc AS Occupation, k.f_CustSeg_Subtype_Desc AS SubOccupation,
               k.f_Kyc_MaritalStatus AS MaritalStatus,
               CASE k.f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
               CASE k.f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated,
               a.f_MobileNumber AS Mobile, a.f_EmailAdd AS Email
        FROM dbo.t_FD_BT_Kyc_Data_Dtl k
        LEFT JOIN dbo.t_FD_BT_Address_Dtl a
            ON a.f_Appl_No = k.f_Appl_No AND a.f_Holder_Type = k.f_Holder_Type
           AND a.f_AddType_Code = 'PER' AND a.f_Status = 'APR' AND a.f_Active = 1
        WHERE k.f_Status = 'APR' AND k.f_Active = 1
        """;

    /// <summary>The brokers an application can be sourced under. Must give back: Code, Name.</summary>
    public const string Brokers = """
        SELECT b.c_Code AS Code, b.c_Name AS Name
        FROM dbo.t_Unotp_Broker_Mst b
        WHERE b.f_Active = 1
        """;

    /// <summary>
    /// The staff a sub-broker or employee code is searched against. Must give back:
    /// Code, Name, Department. A sourcing mode takes the employees of the departments
    /// the 'sourcingModeDepartments' list in t_Unotp_Ref_List names for it, so
    /// Department must come back spelled as it is there.
    /// </summary>
    public const string Staff = """
        SELECT s.c_Code AS Code, s.c_Name AS Name, s.c_Department AS Department
        FROM dbo.t_Unotp_Staff_Mst s
        WHERE s.f_Active = 1
        """;

    /// <summary>The bank branches, by IFSC. Must give back: Ifsc, Bank, Branch, Micr.</summary>
    public const string BankBranches = """
        SELECT b.c_Ifsc AS Ifsc, b.c_Bank AS Bank, b.c_Branch AS Branch, b.c_Micr AS Micr
        FROM dbo.t_Unotp_Ifsc_Mst b
        WHERE b.f_Active = 1
        """;

    /// <summary>The PIN codes. Must give back: PinCode, District, State.</summary>
    public const string PinCodes = """
        SELECT p.c_Pin_Code AS PinCode, p.c_District AS District, p.c_State AS State
        FROM dbo.t_Unotp_Pincode_Mst p
        WHERE p.f_Active = 1
        """;

    /// <summary>
    /// The Axis CMS locations a cheque is presented at. Must give back: Code, Name.
    /// Put the Axis CMS master's own table and columns here. Until then this reads
    /// the development rows kept under 'cmsLocations' in t_Unotp_Ref_List.
    /// </summary>
    public const string CmsLocations = """
        SELECT c.c_Code AS Code, c.c_Name AS Name
        FROM dbo.t_Unotp_Ref_List c
        WHERE c.c_List = 'cmsLocations' AND c.f_Active = 1
        """;

    /// <summary>
    /// The banks the payment gateway takes for online payment. Must give back: Code
    /// (the bank's code, the first four letters of its IFSCs), Name. Put the gateway
    /// bank master's own table and columns here. Until then this reads the
    /// development rows kept under 'gatewayBanks' in t_Unotp_Ref_List.
    /// </summary>
    public const string GatewayBanks = """
        SELECT g.c_Code AS Code, g.c_Name AS Name
        FROM dbo.t_Unotp_Ref_List g
        WHERE g.c_List = 'gatewayBanks' AND g.f_Active = 1
        """;

    /// <summary>
    /// The rate card, one row per scheme code. Must give back: Category, Mode (AF or R),
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
}
