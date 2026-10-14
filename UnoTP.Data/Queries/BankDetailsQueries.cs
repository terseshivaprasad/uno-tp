namespace UnoTP.Data;

/// <summary>
/// The SQL of Bank Details and Payment: the bank branches and the Axis CMS branches,
/// one by its code or those found as they are typed, the account a renewal opens
/// with, and saving the step - the payment row and the repayment account.
/// Nothing but SQL is in this file.
///
/// A query on one of the FD system's own tables follows the rules in docs/CODE_GUIDE.md
/// ("A query on an FD table"): keep the names after AS, and keep each parameter in its WHERE.
/// A search's query takes two parameters, both in its own WHERE: the key of one row
/// (@Ifsc or @Code), and what was typed (@Words or @Search). The app gives one and
/// leaves the other NULL, so neither ever brings back the whole table.
/// </summary>
internal static class BankDetailsQueries
{
    /// <summary>
    /// The bank branch an IFSC names.
    /// The branches are read from the FD system's bank master, which must give back Ifsc, Bank,
    /// Branch and Micr: one by its IFSC (@Ifsc), or those found by what was typed (@Words: the words
    /// typed, a space between them). A branch is found when every word typed is in its search key
    /// (f_Searchkey), in any order - the rule the FD system's own bank search goes by.
    /// The same read of the master is in BankBranch and in SearchBankBranches.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole master to find one row.
    /// </summary>
    public const string BankBranch = """
        SELECT TOP (1) b.Ifsc, b.Bank, b.Branch, b.Micr
        FROM (
            SELECT DISTINCT b.NEFT_CODE AS Ifsc, b.BANK_NAME AS Bank, b.BANK_BRANCH AS Branch,
                b.MICR_CODE AS Micr
            FROM dbo.T_FD_RBI_BANK_MSTR b WITH (NOLOCK)
            WHERE (@Ifsc IS NULL OR b.NEFT_CODE = @Ifsc)
              AND (@Words IS NULL OR NOT EXISTS (
                    SELECT 1 FROM STRING_SPLIT(@Words, ' ') word
                    WHERE b.f_Searchkey NOT LIKE '%' + word.value + '%'))
        ) b
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The bank branches a search finds, by MICR code: at most 15 of them, as the FD system's own
    /// search gives.
    /// The branches are read from the FD system's bank master, which must give back Ifsc, Bank,
    /// Branch and Micr: one by its IFSC (@Ifsc), or those found by what was typed (@Words: the words
    /// typed, a space between them). A branch is found when every word typed is in its search key
    /// (f_Searchkey), in any order - the rule the FD system's own bank search goes by.
    /// The same read of the master is in BankBranch and in SearchBankBranches.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole master to find one row.
    /// </summary>
    public const string SearchBankBranches = """
        SELECT TOP (15) b.Ifsc, b.Bank, b.Branch, b.Micr
        FROM (
            SELECT DISTINCT b.NEFT_CODE AS Ifsc, b.BANK_NAME AS Bank, b.BANK_BRANCH AS Branch,
                b.MICR_CODE AS Micr
            FROM dbo.T_FD_RBI_BANK_MSTR b WITH (NOLOCK)
            WHERE (@Ifsc IS NULL OR b.NEFT_CODE = @Ifsc)
              AND (@Words IS NULL OR NOT EXISTS (
                    SELECT 1 FROM STRING_SPLIT(@Words, ' ') word
                    WHERE b.f_Searchkey NOT LIKE '%' + word.value + '%'))
        ) b
        ORDER BY b.Micr
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The Axis CMS branches a search finds, by state and district: at most 20 of them.
    /// The branches are read from the FD system's Axis CMS branch master, as
    /// usp_FD_BT_Get_User_AxisCMSBranch reads them, which must give back Code, Name, Label (the text
    /// the search looks in and shows: the branch's name, location and PIN code), State and District:
    /// one by its code (@Code, the branch's SOL id), or those whose label holds what was typed (@Search).
    /// The same read of the master is in CmsLocation and in SearchCmsLocations.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole master to find one row.
    /// </summary>
    public const string SearchCmsLocations = """
        SELECT TOP (20) c.Code, c.Name, c.Label
        FROM (
            SELECT CAST(c.f_Sol_Id AS VARCHAR(50)) AS Code, c.f_Branch_Name AS Name, c.f_LABEL AS Label,
                   c.f_State AS State, c.f_District AS District
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_Axis_CMS_Branch_Mst c WITH (NOLOCK)
            WHERE c.f_Active = 1
              AND (@Code IS NULL OR c.f_Sol_Id = @Code)
              AND (@Search IS NULL OR c.f_LABEL LIKE @Search)
        ) c
        ORDER BY c.State, c.District
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The Axis CMS branch a code names.
    /// The branches are read from the FD system's Axis CMS branch master, as
    /// usp_FD_BT_Get_User_AxisCMSBranch reads them, which must give back Code, Name, Label (the text
    /// the search looks in and shows: the branch's name, location and PIN code), State and District:
    /// one by its code (@Code, the branch's SOL id), or those whose label holds what was typed (@Search).
    /// The same read of the master is in CmsLocation and in SearchCmsLocations.
    /// SQL Server is told to plan the read for this call's own values (OPTION (RECOMPILE)): the read
    /// takes both its key and what was typed, and the app gives one and leaves the other empty, so a
    /// plan kept from the other kind would read the whole master to find one row.
    /// </summary>
    public const string CmsLocation = """
        SELECT TOP (1) c.Code, c.Name, c.Label
        FROM (
            SELECT CAST(c.f_Sol_Id AS VARCHAR(50)) AS Code, c.f_Branch_Name AS Name, c.f_LABEL AS Label,
                   c.f_State AS State, c.f_District AS District
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_Axis_CMS_Branch_Mst c WITH (NOLOCK)
            WHERE c.f_Active = 1
              AND (@Code IS NULL OR c.f_Sol_Id = @Code)
              AND (@Search IS NULL OR c.f_LABEL LIKE @Search)
        ) c
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// The repayment account on a booked deposit's application, for a renewal to open with.
    /// </summary>
    public const string AccountsOnDeposit = """
        SELECT b.f_NEFTCode, b.f_BankAccountNo, b.f_BankName, b.f_BranchName, m.f_App_No
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl b
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = b.f_Appl_No AND m.f_Active = 1
        WHERE m.f_Folio = @Folio AND m.f_Fdr_No = @DepositNumber AND b.f_Status IN ('APR', 'PEN_E')
          AND b.f_Active = 1
          AND b.f_NEFTCode <> '' AND b.f_BankAccountNo <> ''
        ORDER BY m.f_Submitted_On DESC
        """;

    /// <summary>
    /// Bank Details and Payment saved again: the payment rows it replaces go out of use, so the application's current payment row is the active one.
    /// </summary>
    public const string RetirePayment = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(),
            f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Bank Details and Payment: how the deposit is paid, and the account it is paid from.
    /// </summary>
    public const string InsertPayment = """
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl (f_Appl_No, f_Payment_Mode, f_Cheque_DD_No, f_Cheque_DD_Date, f_Drawn_Bank_Name, f_Bank_Branch_Name, f_Bank_MICR, f_Bank_NEFT, f_BankAccountNo, f_CMS_Loc_CD, f_CMS_Loc_Desc, f_CMS_BANK_NAME, f_FolioNo, f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @PayMode, @ChequeNo, @ChequeDate, @Bank, @BranchName, @Micr, @Ifsc, @AccountNo, @CmsLocationCode, @CmsLocation, @CmsBank, @Folio, @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;

    /// <summary>
    /// Bank Details and Payment saved again: the repayment account rows it replaces go out of use, so the application's current repayment account is the active one.
    /// </summary>
    public const string RetireRepaymentAccount = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl
        SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedOn = GETDATE(),
            f_UpdatedIP = @Ip
        WHERE f_Appl_No = @AppNo AND f_Active = 1
        """;

    /// <summary>
    /// Bank Details and Payment: the repayment account.
    /// </summary>
    public const string InsertRepaymentAccount = """
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investor_Bank_Dtl (f_Appl_No, f_MICRCode, f_NEFTCode, f_BankName, f_BranchName, f_BankAccountNo, f_sameAsCheque, f_FolioNo, f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
        VALUES (@AppNo, @Micr, @Ifsc, @Bank, @BranchName, @AccountNo, @SameAsPayment, @Folio, @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
        """;
}
