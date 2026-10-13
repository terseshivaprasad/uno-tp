namespace UnoTP.Data;

/// <summary>
/// The SQL that reads each big table the app only looks rows up in: a holder's active
/// deposits, brokers, the staff each sourcing mode takes,
/// staff, bank branches, PIN codes, the Axis CMS locations, the payment gateway's
/// banks and the rate card. Change a query here when a table
/// has another name, needs a join, or needs a condition. The small lists (categories,
/// payouts, relations, occupations and the rest) are not here: they are rows of
/// t_Unotp_Ref_List, read in SqlReference. The folio check and what the FD system
/// holds for a holder are not here either: they are Investor Identification's, in
/// InvestorIdentificationQueries.cs.
///
///   - Keep the names after AS: they are what the app reads. A column may come from
///     any table or expression, so long as it comes back under the same name.
///   - A table in another database on the same server is written by its full name:
///     OtherDb.dbo.Table.
///   - A query that finds rows by a key carries that key as a parameter, written in
///     its own WHERE (@Pan, @Pin). It is run as it stands, so the table
///     is read by that key and never whole. Keep the parameter names, and compare
///     them with the table's own column as it is (a.PAN1 = @Pan): a column
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
    /// The deposits a PAN (@Pan) holds with us that are active, for the source-of-funds
    /// rule: their amounts are added to the new deposit's. Must give back: Number (the
    /// FDR number, so a renewal can leave out the deposit it renews) and Amount.
    /// DEP_STATUS 'L' is an active deposit. PRINC_AMT is kept as text: one that is not a
    /// number counts as nothing.
    /// </summary>
    public const string ActiveDeposits = """
        SELECT a.FDR_NO AS Number, TRY_CAST(a.PRINC_AMT AS DECIMAL(18, 2)) AS Amount
        FROM FD.dbo.FDR_MST a WITH (NOLOCK)
        WHERE a.DEP_STATUS = 'L' AND a.PAN1 = @Pan
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
