namespace UnoTP.Data;

/// <summary>
/// The SQL of Pay In Slip Generation: the cheque applications listed from the staging
/// table, and generating a slip as the FD system's own procedure does - its number from
/// USP_FD_BTP_GetPayinSlipNo, its header and detail rows copied from the staging row, the
/// staging row and the header marked as done - and the slip as it is printed. Not taken from
/// the procedure: Usp_Get_FD_BANK_CLIENT_ID and the T_FD_PAYINSLIP_COMM update, neither of
/// which this app knows. The staging row itself is
/// written on Submit (ReviewSummaryQueries.InsertStaging). Nothing but SQL is in this file.
/// </summary>
internal static class PayInSlipQueries
{
    /// <summary>
    /// The partner's cheque applications a search reaches, as Pay In Slip Generation lists them: each
    /// with its staging row, where the application has got to, and the number of its newest slip.
    /// Named after StagedSlipRow. Only a purchase is paid by cheque, so the acceptance read (the four flags
    /// on a link row) looks at the purchase link table alone. An application is cancelled when the app cancelled it (f_Cancelled_On)
    /// or the FD system did (an active row in t_FD_BT_Cancelled_Application_Dtl). The newest @Most of them, the one submitted last first. One query
    /// for every search: the app gives the search's own values and leaves the others NULL, and a
    /// condition whose value is NULL lets every row through.
    /// - by dates: @From and @Until - submitted from @From up to, and not on, @Until.
    /// - by application number: @Number, a LIKE pattern - the number holds what was typed.
    /// - the one application: @AppNo, its whole number (for generating its slip).
    /// OPTION (RECOMPILE) has SQL Server plan the read for the values this call gives, so a search by
    /// number is not run on a plan kept from a search by dates.
    /// </summary>
    public const string StagedSlips = """
        SELECT TOP (@Most) s.f_Application_Number AS AppNo, ISNULL(s.f_Applicant_Name, N'') AS Name,
            TRY_CONVERT(BIGINT, s.f_Cheque_Amt) AS Amount, s.f_Cheque_Number AS ChequeNo,
            s.f_Drawn_Bank_Name AS BankName, ISNULL(s.F_CMS_Location_Name, N'') AS Branch,
            p.f_Payment_Mode AS PayMode, i.AppType, m.f_Submitted_On AS SubmittedOn,
            (SELECT MAX(COALESCE(d.f_UpdatedOn, d.f_CreatedOn))
             FROM MMFSL_APP_FDPGW.dbo.t_FD_PGW_PaymentURLRequest_dtl d WITH (NOLOCK)
             WHERE d.f_AppNo = m.f_App_No
               AND d.f_Disclaimer1 = 1 AND d.f_Disclaimer2 = 1 AND d.f_TermsCondition = 1
               AND d.f_KycConfirmation = 1) AS AcceptedOn,
            m.f_Paid_On AS PaidOn, COALESCE(m.f_Cancelled_On,
                     (SELECT MAX(COALESCE(c.f_Cancelled_Date, c.f_CreatedOn, GETDATE()))
                      FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Cancelled_Application_Dtl c WITH (NOLOCK)
                      WHERE c.f_Appl_No = m.f_App_No AND c.f_Active = 1)) AS CancelledOn,
            slip.SlipNo
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr s
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = s.f_Application_Number
        OUTER APPLY (
            SELECT TOP (1) CAST(d.f_Amount AS BIGINT) AS Amount, TRY_CAST(d.f_Tenure AS INT) AS TenureMonths,
                d.f_Int_Freq AS Payout, d.f_ApplicationDeclarationType AS AppType
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl d
            WHERE d.f_Appl_No = m.f_App_No AND d.f_Active = 1
            ORDER BY d.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) i
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        OUTER APPLY (
            SELECT TOP (1) d.f_Payinslip_No AS SlipNo
            FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Dtl d
            WHERE d.f_Application_Number = s.f_Application_Number
            ORDER BY d.f_Pk_t_BT_Payinslip_Dtl_ID DESC) slip
        WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'APR' AND m.f_Active = 1
          AND s.f_Pk_t_BT_Payinslip_Staging_Hdr_ID = (
              SELECT MAX(n.f_Pk_t_BT_Payinslip_Staging_Hdr_ID) FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr n
              WHERE n.f_Application_Number = s.f_Application_Number)
          AND (@From IS NULL OR m.f_Submitted_On >= @From)
          AND (@Until IS NULL OR m.f_Submitted_On < @Until)
          AND (@Number IS NULL OR s.f_Application_Number LIKE @Number)
          AND (@AppNo IS NULL OR s.f_Application_Number = @AppNo)
        ORDER BY m.f_Submitted_On DESC
        OPTION (RECOMPILE)
        """;

    /// <summary>
    /// An application's row in the staging table, the newest should there be two, named after
    /// the properties of PayInSlipStagingRow (Models/PayInSlipStaging.cs): what the slip is made from,
    /// and who wrote the row, from where, in which session and under which form code - what the slip
    /// number is asked with.
    /// </summary>
    public const string FindStaging = """
        SELECT TOP (1) f_Application_Number AS ApplicationNumber, f_Applicant_Name AS ApplicantName,
            f_Agency_Code AS AgencyCode, f_Cheque_Number AS ChequeNumber, f_Cheque_Date AS ChequeDate,
            f_Cheque_Amt AS ChequeAmount, f_Drawn_Bank_Name AS DrawnBankName,
            f_Drawn_Branch_Name AS DrawnBranchName, f_Drawn_Bank_MICR AS DrawnBankMicr,
            f_Drawn_Bank_IFSCCOde AS DrawnBankIfsc, f_CMS_Bank_Name AS CmsBankName,
            F_CMS_Location_Code AS CmsLocationCode, F_CMS_Location_Name AS CmsLocationName,
            f_Queue_Status AS QueueStatus,
            f_CreatedByName AS CreatedByName, f_CreatedIP AS CreatedIp, f_SessionID AS SessionId, f_FormCode AS FormCode
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr
        WHERE f_Application_Number = @AppNo
        ORDER BY f_Pk_t_BT_Payinslip_Staging_Hdr_ID DESC
        """;

    /// <summary>
    /// The next slip number, asked of the FD system's own procedure as usp_BT_Save_axis_payinslip_hdr_details_v3_1
    /// asks it: with the staging row's Axis CMS branch, who wrote it (as user id and user name both), from which
    /// address and session, its form code as it stands (NULL when it has none), and the source B. The number
    /// comes back in @SlipNo.
    /// </summary>
    public const string NextSlipNo = """
        EXEC MMFSL_APP_FD_BTP.dbo.USP_FD_BTP_GetPayinSlipNo
            @CMS_Loc_Code = @CmsLocationCode, @CreatedIP = @Ip, @CreatedUserId = @UserName, @CreatedUserName = @UserName,
            @SessionId = @SessionId, @FormCode = @FormCode, @DSource = N'B', @PayinSlip_No = @SlipNo OUTPUT
        """;

    /// <summary>
    /// The slip's header row, copied from the application's staging row under the slip number @SlipNo, then
    /// numbered: FPIH and its own id. Gives back the header's sequence (FPIH + id), or NULL when no row was
    /// written - the application has no staging row, or no deposit row standing as APR.
    /// The form code is the staging row's, or MFESBM00002 where it has none; the Axis CMS branch code
    /// (f_CMS_Branch_Code) is the branch's SOL id from the Axis CMS branch master, as
    /// USP_BT_GET_USER_CMSBRANCH_V3_1 names it (Agency_CMS_Branch_Code); the slip is a CMS one with no
    /// deposit by a third party.
    /// The FD system's own procedure, usp_BT_Save_axis_payinslip_hdr_details_v3_1, as it was given, written
    /// as this page's queries. Every value is copied from the application's staging row, as the procedure copies
    /// it; the deposit row must stand as APR. Two things are not as the procedure has them: it joins
    /// t_BTP_Front_Office_Inward_Dtl, which this app never writes, so that join is left out (no column came
    /// from it); and it takes every staging row of the application, where this takes the newest.
    /// </summary>
    public const string InsertHeader = """
        DECLARE @HdrId BIGINT;
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Hdr (f_Payinslip_Stg_HdrSequence, f_Payinslip_SourceName, f_PortalSourceCode, f_MCP_Code, f_SCP_Code,
            f_MCP_Broker_Code, f_SCP_Broker_Code, f_Employee_Code, f_ADO_Indicator, f_CMS_Type, f_CMS_Bank_Name, f_CMS_Branch_Name,
            f_CMS_Bank_IFSC_Code, f_CMS_Bank_MICR_Code, f_MF_Branch_Code, f_MF_Branch_Name, f_SCP_Location_Code, f_SCP_Location_Name,
            f_Entry_Date, f_Pickup_Date, f_Remarks, f_Req_State, f_Req_Status, f_Req_Date, f_Payinslip_No, f_Req_Remarks,
            f_CreatedType, f_CreatedByName, f_CreatedIP, f_CreatedDate, f_SessionID, f_FormCode, f_Dummy, f_Cheque_Count,
            f_CMS_Location_Desc, f_CMS_Location_Code, f_BTP_Location_Code, f_BTP_Location_Desc, f_Agency_Code, f_Agency_Name,
            f_CMS_Branch_Code, f_CMS_Bank_Sr_No, f_Is_CMS_NCMS, f_ChequeDepositedBy, f_NONCMSCheque_Deposit_Date)
        SELECT hdr.f_Payinslip_Stg_HdrSequence, hdr.f_Payinslip_SourceName, hdr.f_PortalSourceCode, hdr.f_MCP_Code, hdr.f_SCP_Code,
               hdr.f_MCP_Broker_Code, hdr.f_SCP_Broker_Code, hdr.f_Employee_Code, hdr.f_ADO_Indicator,
               ISNULL(hdr.f_CMS_Type, ''), hdr.f_CMS_Bank_Name, ISNULL(hdr.f_CMS_Branch_Name, ''),
               ISNULL(hdr.f_CMS_Bank_IFSC_Code, ''), ISNULL(hdr.f_CMS_Bank_MICR_Code, ''), ISNULL(hdr.f_MF_Branch_Code, ''),
               ISNULL(hdr.f_MF_Branch_Name, ''), hdr.f_SCP_Location_Code, hdr.f_SCP_Location_Name,
               FORMAT(CONVERT(DATETIME, hdr.f_Entry_Date), 'dd-MMM-yyyy'),
               CASE WHEN CHARINDEX('HDFC', hdr.f_Drawn_Bank_Name) > 0 THEN REPLACE(CONVERT(VARCHAR, CONVERT(DATETIME, hdr.f_Entry_Date), 106), ' ', '-')
                    WHEN CHARINDEX('AXIS', hdr.f_Drawn_Bank_Name) > 0 THEN REPLACE(CONVERT(VARCHAR, CONVERT(DATETIME, hdr.f_Entry_Date), 106), ' ', '-')
                    ELSE REPLACE(CONVERT(VARCHAR, CONVERT(DATETIME, hdr.f_Entry_Date) + 1, 106), ' ', '-') END,
               hdr.f_Remarks, hdr.f_Queue_State, hdr.f_Queue_Status, GETDATE(), @SlipNo, hdr.f_Remarks,
               hdr.f_CreatedType, hdr.f_CreatedByName, hdr.f_CreatedIP, GETDATE(), hdr.f_SessionID, ISNULL(hdr.f_FormCode, 'MFESBM00002'), hdr.f_Dummy, '1',
               hdr.F_CMS_Location_Name, hdr.F_CMS_Location_Code, '', '', hdr.f_Agency_Code, hdr.f_Agency_Name,
               CAST(cms.f_Sol_Id AS NVARCHAR(50)), hdr.f_CMS_Bank_Sr_No, 'CMS', NULL, NULL
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr hdr WITH (NOLOCK)
        JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl inv WITH (NOLOCK)
            ON hdr.f_Application_Number = inv.f_Appl_No AND inv.f_Status = 'APR' AND inv.f_Active = 1
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_Axis_CMS_Branch_Mst cms WITH (NOLOCK)
            ON CAST(cms.f_Sol_Id AS NVARCHAR(100)) = hdr.F_CMS_Location_Code AND cms.f_Active = 1
        WHERE hdr.f_Application_Number = @AppNo
          AND hdr.f_Pk_t_BT_Payinslip_Staging_Hdr_ID = (SELECT MAX(n.f_Pk_t_BT_Payinslip_Staging_Hdr_ID)
                                                        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr n
                                                        WHERE n.f_Application_Number = @AppNo);
        SET @HdrId = SCOPE_IDENTITY();
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Hdr
        SET f_Payinslip_Hdr_Abr = 'FPIH', f_Payinslip_Hdr_Seq = CAST(@HdrId AS NVARCHAR(50)),
            f_Payinslip_HdrSequence = 'FPIH' + CAST(@HdrId AS NVARCHAR(50))
        WHERE f_Pk_t_BT_Payinslip_Hdr_ID = @HdrId;
        SELECT 'FPIH' + CAST(@HdrId AS NVARCHAR(50));
        """;

    /// <summary>
    /// The slip's detail row - the one cheque on it - copied from the application's staging row under the
    /// slip number @SlipNo and the header's sequence @HdrSequence, then numbered: FPID and its own id.
    /// TO CONFIRM: the procedure as photographed writes the staging row's own sequence into
    /// f_Payinslip_HdrSequence, and then finds the header by it, which cannot match; the header's sequence
    /// is written here so the two rows are tied.
    /// The FD system's own procedure, usp_BT_Save_axis_payinslip_hdr_details_v3_1, as it was given, written
    /// as this page's queries. Every value is copied from the application's staging row, as the procedure copies
    /// it; the deposit row must stand as APR. Two things are not as the procedure has them: it joins
    /// t_BTP_Front_Office_Inward_Dtl, which this app never writes, so that join is left out (no column came
    /// from it); and it takes every staging row of the application, where this takes the newest.
    /// </summary>
    public const string InsertDetail = """
        DECLARE @DtlId BIGINT;
        INSERT MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Dtl (f_Payinslip_HdrSequence, f_Payinslip_SourceName, f_PortalSourceCode, f_MCP_Code, f_SCP_Code,
            f_MCP_Broker_Code, f_SCP_Broker_Code, f_Employee_Code, f_ADO_Indicator, f_CP_Trans_Ref_No, f_CMS_Type, f_CMS_Bank_Name,
            f_CMS_Branch_Name, f_CMS_Bank_IFSC_Code, f_CMS_Bank_MICR_Code, f_MF_Branch_Code, f_MF_Branch_Name, f_SCP_Location_Code,
            f_SCP_Location_Name, f_Payinslip_No, f_Cheque_Number, f_Cheque_Date, f_Cheque_Amt, f_Drawn_Bank_Name, f_Drawn_Branch_Name,
            f_Applicant_Name, f_Application_Number, f_Entry_Date, f_Pickup_Date, f_Remarks, f_Process_Flg, f_Process_Date,
            f_Process_Remarks, f_Active_Flg, f_CreatedType, f_CreatedByName, f_CreatedIP, f_CreatedDate, f_SessionID, f_FormCode,
            f_Dummy, F_CMS_Location, f_cms_location_code, f_Drawn_Bank_MICR, f_Drawn_Bank_IFSCCOde, f_BTP_Location_Code,
            f_BTP_Location_Desc, f_CMS_Branch_Code, f_CMS_Bank_Sr_No, f_Is_CMS_NCMS, f_ChequeDepositedBy, f_NONCMSCheque_Deposit_Date)
        SELECT @HdrSequence, hdr.f_Payinslip_SourceName, hdr.f_PortalSourceCode, hdr.f_MCP_Code, hdr.f_SCP_Code,
               hdr.f_MCP_Broker_Code, hdr.f_SCP_Broker_Code, hdr.f_Employee_Code, hdr.f_ADO_Indicator, '',
               ISNULL(hdr.f_CMS_Type, ''), hdr.f_CMS_Bank_Name, ISNULL(hdr.f_CMS_Branch_Name, ''),
               ISNULL(hdr.f_CMS_Bank_IFSC_Code, ''), ISNULL(hdr.f_CMS_Bank_MICR_Code, ''), ISNULL(hdr.f_MF_Branch_Code, ''),
               ISNULL(hdr.f_MF_Branch_Name, ''), hdr.f_SCP_Location_Code, hdr.f_SCP_Location_Name,
               @SlipNo, hdr.f_Cheque_Number, hdr.f_Cheque_Date, hdr.f_Cheque_Amt, hdr.f_Drawn_Bank_Name, hdr.f_Drawn_Branch_Name,
               hdr.f_Applicant_Name, hdr.f_Application_Number,
               FORMAT(CONVERT(DATETIME, hdr.f_Entry_Date), 'dd-MMM-yyyy'),
               CASE WHEN CHARINDEX('HDFC', hdr.f_Drawn_Bank_Name) > 0 THEN REPLACE(CONVERT(VARCHAR, CONVERT(DATETIME, hdr.f_Entry_Date), 106), ' ', '-')
                    WHEN CHARINDEX('AXIS', hdr.f_Drawn_Bank_Name) > 0 THEN REPLACE(CONVERT(VARCHAR, CONVERT(DATETIME, hdr.f_Entry_Date), 106), ' ', '-')
                    ELSE REPLACE(CONVERT(VARCHAR, CONVERT(DATETIME, hdr.f_Entry_Date) + 1, 106), ' ', '-') END,
               hdr.f_Remarks, 0, GETDATE(), hdr.f_Remarks, 1,
               hdr.f_CreatedType, hdr.f_CreatedByName, hdr.f_CreatedIP, GETDATE(), hdr.f_SessionID, ISNULL(hdr.f_FormCode, 'MFESBM00002'),
               hdr.f_Dummy, hdr.F_CMS_Location_Name, hdr.F_CMS_Location_Code, hdr.f_Drawn_Bank_MICR, hdr.f_Drawn_Bank_IFSCCOde, '',
               '', CAST(cms.f_Sol_Id AS NVARCHAR(50)), hdr.f_CMS_Bank_Sr_No, 'CMS', NULL, NULL
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr hdr WITH (NOLOCK)
        JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Investment_Dtl inv WITH (NOLOCK)
            ON hdr.f_Application_Number = inv.f_Appl_No AND inv.f_Status = 'APR' AND inv.f_Active = 1
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_Axis_CMS_Branch_Mst cms WITH (NOLOCK)
            ON CAST(cms.f_Sol_Id AS NVARCHAR(100)) = hdr.F_CMS_Location_Code AND cms.f_Active = 1
        WHERE hdr.f_Application_Number = @AppNo
          AND hdr.f_Pk_t_BT_Payinslip_Staging_Hdr_ID = (SELECT MAX(n.f_Pk_t_BT_Payinslip_Staging_Hdr_ID)
                                                        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr n
                                                        WHERE n.f_Application_Number = @AppNo);
        SET @DtlId = SCOPE_IDENTITY();
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Dtl
        SET f_Payinslip_Dtl_Abr = 'FPID', f_Payinslip_DtlSequence = 'FPID' + CAST(@DtlId AS NVARCHAR(50)),
            f_Payinslip_Dtl_Seq = @DtlId, f_Payinslip_No = @SlipNo
        WHERE f_Pk_t_BT_Payinslip_Dtl_ID = @DtlId;
        """;

    /// <summary>
    /// The application's staging row once its slip is generated: f_Queue_State PRC and f_Queue_Status GEN
    /// (@QueueState, @QueueStatus), as the procedure marks it, and f_Queue_Process_Date now. The newest
    /// row, should there be two.
    /// </summary>
    public const string SetStagingStatus = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr
        SET f_Queue_State = @QueueState, f_Queue_Status = @QueueStatus, f_Queue_Process_Date = GETDATE()
        WHERE f_Pk_t_BT_Payinslip_Staging_Hdr_ID = (
            SELECT MAX(n.f_Pk_t_BT_Payinslip_Staging_Hdr_ID) FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Staging_Hdr n
            WHERE n.f_Application_Number = @AppNo)
        """;

    /// <summary>
    /// Whether the application has a slip already: 1 when a detail row holds its number. The procedure
    /// makes a slip once; after that it only reads the slip back, so a reprint writes nothing.
    /// </summary>
    public const string SlipAlreadyMade = """
        SELECT COUNT(*) FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Dtl WITH (NOLOCK) WHERE f_Application_Number = @AppNo
        """;

    /// <summary>
    /// The slip's header once the slip is made: f_Req_State PRC and f_Req_Status GEN, as the procedure
    /// marks it at its end.
    /// </summary>
    public const string MarkHeaderGenerated = """
        UPDATE MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Hdr SET f_Req_State = 'PRC', f_Req_Status = 'GEN'
        WHERE f_Payinslip_HdrSequence = @HdrSequence
        """;

    /// <summary>
    /// The account the cheque is drawn on comes from the application's active payment row, as the
    /// FD system's own print read joins it.
    /// The newest pay-in slip of one of the partner's applications, as it is printed. Named after
    /// the properties of SlipPrint.
    /// </summary>
    public const string SlipToPrint = """
        SELECT TOP (1) d.f_Payinslip_No AS SlipNo, d.f_Application_Number AS AppNo,
            m.f_Submitted_On AS ApplicationDate, ISNULL(d.f_Applicant_Name, N'') AS CustomerName,
            ISNULL(TRY_CONVERT(BIGINT, d.f_Cheque_Amt), 0) AS Amount,
            ISNULL(d.f_Cheque_Number, N'') AS ChequeNumber, ISNULL(d.f_Drawn_Bank_Name, N'') AS BankName,
            ISNULL(d.f_Drawn_Bank_IFSCCOde, N'') AS BankIfsc, ISNULL(d.f_Drawn_Branch_Name, N'') AS BranchName,
            ISNULL(d.f_cms_location_code, N'') AS SolId, ISNULL(p.f_BankAccountNo, N'') AS AccountNumber
        FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payinslip_Dtl d
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = d.f_Application_Number
        LEFT JOIN MMFSL_APP_FD_BTP.dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = d.f_Application_Number AND p.f_Active = 1
        WHERE d.f_Application_Number = @AppNo AND m.f_Partner_Id = @Partner
        ORDER BY d.f_Pk_t_BT_Payinslip_Dtl_ID DESC
        """;
}
