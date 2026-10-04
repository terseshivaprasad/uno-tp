namespace UnoTP.Data;

/// <summary>
/// The SQL that reads each master, in one place. Change a query here when a master
/// comes from another table, needs a join, or needs a condition.
///
///   - Keep the names after AS: they are what the app reads. A column may come from
///     any table or expression, so long as it comes back under the same name.
///   - A table in another database on the same server is written by its full name:
///     OtherDb.dbo.Table.
///   - No ORDER BY here. The app reads each query as
///     SELECT ... FROM ( the query ) x WHERE ... ORDER BY ...
///     to find one row, to search and to sort.
///   - Each query says which connection it is read on. To change that, change
///     db.OpenAsync (main) to db.OpenMastersAsync (masters), or back, where it is read.
/// </summary>
internal static class MasterQueries
{
    /// <summary>The investor folios. Must give back: Pan, Dob, Folio, Name, Gender, Address, DocPan, DocPhoto, DocPoa, Note, Source.</summary>
    // Connection: masters, in SqlMasters.cs.
    public const string Folios = """
        SELECT f.c_Pan AS Pan, f.d_Dob AS Dob, f.c_Folio AS Folio, f.c_Name AS Name, f.c_Gender AS Gender, f.c_Address AS Address,
               f.f_Doc_Pan AS DocPan, f.f_Doc_Photo AS DocPhoto, f.f_Doc_Poa AS DocPoa, f.c_Note AS Note, f.c_Source AS Source
        FROM dbo.t_Unotp_Investor_Folio f
        WHERE f.f_Active = 1
        """;

    /// <summary>The brokers an application can be sourced under. Must give back: Code, Name.</summary>
    // Connection: masters, in SqlMasters.cs.
    public const string Brokers = """
        SELECT b.c_Code AS Code, b.c_Name AS Name
        FROM dbo.t_Unotp_Broker_Mst b
        WHERE b.f_Active = 1
        """;

    /// <summary>The staff a sub-broker or employee code is searched against. Must give back: Code, Name.</summary>
    // Connection: masters, in SqlMasters.cs.
    public const string Staff = """
        SELECT s.c_Code AS Code, s.c_Name AS Name
        FROM dbo.t_Unotp_Staff_Mst s
        WHERE s.f_Active = 1
        """;

    /// <summary>The bank branches, by IFSC. Must give back: Ifsc, Bank, Branch, Micr.</summary>
    // Connection: main, in SqlMasters.cs.
    public const string BankBranches = """
        SELECT b.c_Ifsc AS Ifsc, b.c_Bank AS Bank, b.c_Branch AS Branch, b.c_Micr AS Micr
        FROM dbo.t_Unotp_Ifsc_Mst b
        WHERE b.f_Active = 1
        """;

    /// <summary>The PIN codes. Must give back: PinCode, District, State.</summary>
    // Connection: masters, in SqlMasters.cs.
    public const string PinCodes = """
        SELECT p.c_Pin_Code AS PinCode, p.c_District AS District, p.c_State AS State
        FROM dbo.t_Unotp_Pincode_Mst p
        WHERE p.f_Active = 1
        """;

    /// <summary>
    /// The rate card, one row per scheme code. Must give back: Category, Mode (AF or R),
    /// Scheme, SchemeCode, InterestFreq, TenureMonths, Rate, MinAmount, MaxAmount,
    /// FromDate, ToDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out.
    /// </summary>
    // Connection: main, in SqlMasters.RateCard.cs.
    public const string RateCard = """
        SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme, RTRIM(r.SCHEME_CODE) AS SchemeCode,
               RTRIM(r.INTEREST_FREQ) AS InterestFreq, TRY_CAST(r.PERIOD AS INT) AS TenureMonths, TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
               CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
               CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
               r.FROM_DATE AS FromDate, r.TO_DATE AS ToDate, r.SCHEME_ID AS SchemeId
        FROM dbo.t_FD_BOTC_SCHEME r
        """;

    /// <summary>The marital statuses offered. Must give back: Code, Name.</summary>
    // Connection: masters, in SqlReference.Masters.cs.
    public const string MaritalStatuses = """
        SELECT RTRIM(m.f_MaritalStatus_Code) AS Code, RTRIM(m.f_MaritalStatus_Name) AS Name
        FROM dbo.t_FD_BT_Marital_Status_Mst m
        WHERE m.f_Active = 1
        """;

    /// <summary>The relations a nominee can be to the primary holder. Must give back: Code, Name.</summary>
    // Connection: masters, in SqlReference.Masters.cs.
    public const string NomineeRelations = """
        SELECT RTRIM(r.f_Relation_Code) AS Code, RTRIM(r.f_Relation_Name) AS Name
        FROM dbo.t_FD_CMN_Relation_Mst r
        WHERE r.f_Active = 1
        """;

    /// <summary>
    /// The relations an employee can be to the primary holder: every row, active or not,
    /// because the employee's own relation is taken though it is not offered. Must give
    /// back: Code, Name, Active.
    /// </summary>
    // Connection: masters, in SqlReference.Masters.cs.
    public const string EmployeeRelations = """
        SELECT RTRIM(r.f_Relation_Code) AS Code, RTRIM(r.f_Relation_Name) AS Name, CAST(ISNULL(r.f_Active, 0) AS BIT) AS Active
        FROM dbo.t_FD_MMFSL_Employee_Relation_Mst r
        """;

    /// <summary>
    /// The occupations: a type (the occupation offered), a sub-type under it (the sub
    /// occupation) and the CKYC occupation they stand for. Must give back: TypeCode,
    /// TypeName, SubTypeCode, SubTypeName, OccupationCode, OccupationName.
    /// </summary>
    // Connection: masters, in SqlReference.Masters.cs.
    public const string Occupations = """
        SELECT RTRIM(ISNULL(o.f_Ckyc_CustSeg_Type_Code, '')) AS TypeCode, RTRIM(ISNULL(o.f_Ckyc_CustSeg_Type_Desc, '')) AS TypeName,
               RTRIM(ISNULL(o.f_Ckyc_CustSeg_SubType_Code, '')) AS SubTypeCode, RTRIM(ISNULL(o.f_Ckyc_CustSeg_SubType_Desc, '')) AS SubTypeName,
               RTRIM(ISNULL(o.f_Ckyc_Occupation_Code, '')) AS OccupationCode, RTRIM(ISNULL(o.f_Ckyc_Occupation_Desc, '')) AS OccupationName
        FROM dbo.t_FD_CMN_Ckyc_CustSeg_Mst o
        """;

    /// <summary>The sources of funds offered. Must give back: Code, Name.</summary>
    // Connection: masters, in SqlReference.Masters.cs.
    public const string SourcesOfFunds = """
        SELECT RTRIM(s.f_AML_Source_Of_Funds_Code) AS Code, RTRIM(s.f_AML_Source_Of_Funds_Desc) AS Name
        FROM dbo.t_FD_CMN_AML_Source_Of_Funds_Mst s
        WHERE s.f_Active = 1
        """;

    /// <summary>
    /// The document master: each sub-type with the type it is under. Must give back:
    /// DepositorStatus, TypeCode, TypeName, SubTypeCode, SubTypeName.
    /// </summary>
    // Connection: masters, in SqlReference.Documents.cs.
    public const string Documents = """
        SELECT s.f_Depositor_Status_Code AS DepositorStatus,
               s.f_KYC_Document_Type_Code AS TypeCode, t.f_KYC_Document_Type_Desc AS TypeName,
               s.f_KYC_Document_Sub_Type_Code AS SubTypeCode, s.f_KYC_Document_Sub_Type_Desc AS SubTypeName
        FROM dbo.T_FD_CMN_KYC_Document_Sub_Type_Mst s
        LEFT JOIN dbo.T_FD_CMN_KYC_Document_Type_Mst t
            ON t.f_KYC_Document_Type_Code = s.f_KYC_Document_Type_Code AND t.f_Depositor_Status_Code = s.f_Depositor_Status_Code
        WHERE s.f_IsActive = 1
        """;
}
