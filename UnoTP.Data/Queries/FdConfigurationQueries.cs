namespace UnoTP.Data;

/// <summary>
/// The SQL of FD Configuration: the two rate cards and the rates read off them, the
/// investor's active deposits (for the source-of-funds rule), and saving the step -
/// the deposit row and the source of funds log. Nothing but SQL is in this file.
///
/// A query on one of the FD system's own tables follows the rules in docs/CODE_GUIDE.md
/// ("A query on an FD table"): keep the names after AS, and keep each parameter in its WHERE.
/// </summary>
internal static class FdConfigurationQueries
{
    // ----- The rate cards -----------------------------------------------------------
    // Two tables of the same structure: a branch user's card, which also carries the
    // employee and special schemes (and their extra tenures), and a partner's, which
    // does not. Which one a deposit is quoted from follows who is signed in.
    // On both, a row is in effect while its TO_DATE is empty: a row with a TO_DATE
    // has been closed, and is not read.

    /// <summary>
    /// A branch user's rate card (t_FD_BOTC_SCHEME, which also carries the employee and special
    /// schemes): its rows in effect for a category (and the fallback category).
    /// The inner read is the rate card, one row per scheme code in effect (TO_DATE empty), which must
    /// give back: Category, Mode (AF or R), Scheme, SchemeCode, InterestFreq, TenureMonths, Rate,
    /// MinAmount, MaxAmount, FromDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out. The same read of the card is in RatesForBranchUsers and OnRateCardForBranchUsers.
    /// </summary>
    public const string RatesForBranchUsers = """
        SELECT *
        FROM (
            SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme,
                   RTRIM(r.SCHEME_CODE) AS SchemeCode, RTRIM(r.INTEREST_FREQ) AS InterestFreq,
                   TRY_CAST(r.PERIOD AS INT) AS TenureMonths,
                   TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
                   CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
                   CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
                   r.FROM_DATE AS FromDate, r.SCHEME_ID AS SchemeId
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BOTC_SCHEME r
            WHERE r.TO_DATE IS NULL
        ) r
        WHERE r.Category IN (@Category, @Fallback) AND r.Mode = @Mode
          AND r.FromDate <= @Today
        ORDER BY CASE WHEN r.Category = @Category THEN 0 ELSE 1 END, r.FromDate DESC, r.SchemeId DESC
        """;

    /// <summary>
    /// A partner's rate card (FD_SCHEME: the same structure, without the employee and special
    /// schemes): its rows in effect for a category (and the fallback category).
    /// The inner read is the rate card, one row per scheme code in effect (TO_DATE empty), which must
    /// give back: Category, Mode (AF or R), Scheme, SchemeCode, InterestFreq, TenureMonths, Rate,
    /// MinAmount, MaxAmount, FromDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out. The same read of the card is in RatesForPartners and OnRateCardForPartners.
    /// </summary>
    public const string RatesForPartners = """
        SELECT *
        FROM (
            SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme,
                   RTRIM(r.SCHEME_CODE) AS SchemeCode, RTRIM(r.INTEREST_FREQ) AS InterestFreq,
                   TRY_CAST(r.PERIOD AS INT) AS TenureMonths,
                   TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
                   CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
                   CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
                   r.FROM_DATE AS FromDate, r.SCHEME_ID AS SchemeId
            FROM dbo.FD_SCHEME r
            WHERE r.TO_DATE IS NULL
        ) r
        WHERE r.Category IN (@Category, @Fallback) AND r.Mode = @Mode
          AND r.FromDate <= @Today
        ORDER BY CASE WHEN r.Category = @Category THEN 0 ELSE 1 END, r.FromDate DESC, r.SchemeId DESC
        """;

    /// <summary>
    /// How many rows a branch user's rate card has for a deposit as configured: none means it is not
    /// offered.
    /// The inner read is the rate card, one row per scheme code in effect (TO_DATE empty), which must
    /// give back: Category, Mode (AF or R), Scheme, SchemeCode, InterestFreq, TenureMonths, Rate,
    /// MinAmount, MaxAmount, FromDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out. The same read of the card is in RatesForBranchUsers and OnRateCardForBranchUsers.
    /// </summary>
    public const string OnRateCardForBranchUsers = """
        SELECT COUNT(*)
        FROM (
            SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme,
                   RTRIM(r.SCHEME_CODE) AS SchemeCode, RTRIM(r.INTEREST_FREQ) AS InterestFreq,
                   TRY_CAST(r.PERIOD AS INT) AS TenureMonths,
                   TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
                   CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
                   CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
                   r.FROM_DATE AS FromDate, r.SCHEME_ID AS SchemeId
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BOTC_SCHEME r
            WHERE r.TO_DATE IS NULL
        ) r
        WHERE r.Category = @Category AND r.Mode = @Mode
          AND r.Scheme = @Scheme AND r.InterestFreq = @InterestFreq
          AND r.TenureMonths = @TenureMonths AND r.Rate = @Rate
          AND r.MinAmount <= @Amount AND (r.MaxAmount IS NULL OR r.MaxAmount >= @Amount)
          AND r.FromDate <= @Today
        """;

    /// <summary>
    /// How many rows a partner's rate card has for a deposit as configured: none means it is not
    /// offered.
    /// The inner read is the rate card, one row per scheme code in effect (TO_DATE empty), which must
    /// give back: Category, Mode (AF or R), Scheme, SchemeCode, InterestFreq, TenureMonths, Rate,
    /// MinAmount, MaxAmount, FromDate, SchemeId. The numbers are read through a cast: a row whose
    /// number does not read is left out. The same read of the card is in RatesForPartners and OnRateCardForPartners.
    /// </summary>
    public const string OnRateCardForPartners = """
        SELECT COUNT(*)
        FROM (
            SELECT RTRIM(r.CATEGORY) AS Category, RTRIM(r.MODE_STATUS) AS Mode, RTRIM(r.SCHEME) AS Scheme,
                   RTRIM(r.SCHEME_CODE) AS SchemeCode, RTRIM(r.INTEREST_FREQ) AS InterestFreq,
                   TRY_CAST(r.PERIOD AS INT) AS TenureMonths,
                   TRY_CAST(r.INTEREST_RATES AS DECIMAL(9,4)) AS Rate,
                   CAST(TRY_CAST(r.MINIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MinAmount,
                   CAST(TRY_CAST(r.MAXIMUM_AMOUNT AS DECIMAL(18,2)) AS BIGINT) AS MaxAmount,
                   r.FROM_DATE AS FromDate, r.SCHEME_ID AS SchemeId
            FROM dbo.FD_SCHEME r
            WHERE r.TO_DATE IS NULL
        ) r
        WHERE r.Category = @Category AND r.Mode = @Mode
          AND r.Scheme = @Scheme AND r.InterestFreq = @InterestFreq
          AND r.TenureMonths = @TenureMonths AND r.Rate = @Rate
          AND r.MinAmount <= @Amount AND (r.MaxAmount IS NULL OR r.MaxAmount >= @Amount)
          AND r.FromDate <= @Today
        """;

    /// <summary>
    /// The deposits a PAN (@Pan) holds with us that are active, each with its amount, for the
    /// source-of-funds rule: their amounts are added to the new deposit's. The inner read is the
    /// deposit register, which must give back: Number (the FDR number, so a renewal can leave out the
    /// deposit it renews) and Amount. DEP_STATUS 'L' is an active deposit. PRINC_AMT is kept as text:
    /// one that is not a number counts as nothing.
    /// </summary>
    public const string ActiveDepositAmounts = """
        SELECT d.Number, d.Amount
        FROM (
            SELECT a.FDR_NO AS Number, TRY_CAST(a.PRINC_AMT AS DECIMAL(18, 2)) AS Amount
            FROM FD.dbo.FXFDREXP a WITH (NOLOCK)
            WHERE a.DEP_STATUS = 'L' AND a.PAN1 = @Pan
        ) d
        """;

    /// <summary>
    /// FD Configuration saved again: the deposit rows it replaces go out of use, so the application's current deposit row is the active one.
    /// </summary>
    public const string RetireInvestment = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(),
            f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// FD Configuration: the deposit row. The Axis CMS location is copied from the active payment row.
    /// </summary>
    public const string InsertInvestment = """
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl (f_Appl_No, f_Status, f_Amount, f_Tenure, f_Int_Freq, f_Scheme, f_Scheme_Code, f_Is_Auto_Renewal, f_Renewal_For, f_TDS_Flag, f_FDR_Dispatch_Mode, f_AML_Source_Of_Funds, f_AML_Source_Of_Funds_Remarks, f_AML_Source_Of_Funds_reason, f_ApplicationDeclarationType, f_Category, f_Source, f_Broker_Code, f_Employee_Code, f_EmpCompanyName, f_EmpHolder, f_EmpRelation, f_ExistingFDRNo, f_ExistingFDRNoRenewalFor, f_Depositor_Status_Code, f_Ind_Nind, f_HNG, f_FolioNo, f_Cms_Location_Code, f_Cms_Location_Name, f_Int_Rate, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @Status, @Amount, @Tenure, @Payout, @Scheme, @SchemeCode, @AutoRenewal, @RenewInstruction, @TdsFlag, @DeliveryType, @SourceOfFunds, @SourceOfFundsRemark, @SourceOfFundsReason, @AppType, @Category, @Source, @SourceCode, @EmpCode, @EmpCompany, @EmpHolder, @EmpRelation, @Renews, @ExistingRenewalFor, @Individual, @Individual, @Hng, @Folio,
            (SELECT TOP (1) f_CMS_Loc_CD FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC),
            (SELECT TOP (1) f_CMS_Loc_Desc FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl WHERE f_Appl_No = @AppNo AND f_Active = 1 ORDER BY f_Pk_t_FD_BT_Other_Dtl_Id DESC),
            @Rate, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// The source of funds log: the application's earlier entry goes out of use.
    /// </summary>
    public const string RetireSourceOfFunds = """
        UPDATE MMFSL_APP_FD_Common.dbo.t_FD_CMN_AML_Source_Of_Funds_Log SET f_Active = 0 WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// The source of funds log: the source the investor gave, with what it was asked for.
    /// </summary>
    public const string InsertSourceOfFunds = """
        INSERT MMFSL_APP_FD_Common.dbo.t_FD_CMN_AML_Source_Of_Funds_Log (f_Appl_No, f_Holder_Type, f_Investment_Amt, f_AnnualIncome_Desc, f_AML_Source_Of_Funds, f_AML_Source_Of_Funds_Remarks, f_AML_Source_Of_Funds_reason, f_FormCode, f_Source, f_Folio_No, f_Occupation_Desc, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @HolderType, @Amount, @AnnualIncome, @SourceOfFunds, @SourceOfFundsRemark, @SourceOfFundsReason, @FormNo, 'UNO_TP', @Folio, @Occupation, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;
}
