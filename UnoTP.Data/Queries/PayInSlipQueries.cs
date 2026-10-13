namespace UnoTP.Data;

/// <summary>
/// The SQL for pay-in slips: t_FD_BT_Payinslip_Staging_Hdr, t_FD_BT_Payinslip_Hdr and
/// t_FD_BT_Payinslip_Dtl. Nothing but SQL is in this file. The rows written are in
/// Models/PayInSlipStaging.cs and Models/PayInSlip.cs: a column added to an INSERT here
/// needs its property there.
/// </summary>
internal static class PayInSlipQueries
{
    /// <summary>
    /// A cheque application put in the staging table on its final submit, to wait for its slip.
    /// </summary>
    public const string InsertStaging = """
        INSERT dbo.t_FD_BT_Payinslip_Staging_Hdr (f_Application_Number, f_Applicant_Name, f_Agency_Code,
            f_Cheque_Number, f_Cheque_Date, f_Cheque_Amt,
            f_Drawn_Bank_Name, f_Drawn_Branch_Name, f_Drawn_Bank_MICR, f_Drawn_Bank_IFSCCOde,
            f_CMS_Bank_Name, F_CMS_Location_Code, F_CMS_Location_Name,
            f_Queue_Status, f_CreatedByName, f_CreatedIP, f_CreatedDate, f_SessionID)
        VALUES (@ApplicationNumber, @ApplicantName, @AgencyCode,
            @ChequeNumber, @ChequeDate, @ChequeAmount,
            @DrawnBankName, @DrawnBranchName, @DrawnBankMicr, @DrawnBankIfsc,
            @CmsBankName, @CmsLocationCode, @CmsLocationName,
            @QueueStatus, @UserName, @Ip, GETDATE(), @SessionId)
        """;

    /// <summary>
    /// An application's row in the staging table, the newest should there be two, named after
    /// the properties of PayInSlipStagingRow (Models/PayInSlipStaging.cs).
    /// </summary>
    public const string FindStaging = """
        SELECT TOP (1) f_Application_Number AS ApplicationNumber, f_Applicant_Name AS ApplicantName, f_Agency_Code AS AgencyCode,
            f_Cheque_Number AS ChequeNumber, f_Cheque_Date AS ChequeDate, f_Cheque_Amt AS ChequeAmount,
            f_Drawn_Bank_Name AS DrawnBankName, f_Drawn_Branch_Name AS DrawnBranchName,
            f_Drawn_Bank_MICR AS DrawnBankMicr, f_Drawn_Bank_IFSCCOde AS DrawnBankIfsc,
            f_CMS_Bank_Name AS CmsBankName, F_CMS_Location_Code AS CmsLocationCode, F_CMS_Location_Name AS CmsLocationName,
            f_Queue_Status AS QueueStatus
        FROM dbo.t_FD_BT_Payinslip_Staging_Hdr
        WHERE f_Application_Number = @AppNo
        ORDER BY f_Pk_t_BT_Payinslip_Staging_Hdr_ID DESC
        """;

    /// <summary>
    /// A cheque application as Pay In Slip Generation lists it: its staging row, where the
    /// application has got to, and the number of its newest slip. Named after StagedSlipRow.
    /// </summary>
    public const string StagedSlip = $"""
        SELECT s.f_Application_Number AS AppNo, ISNULL(s.f_Applicant_Name, N'') AS Name,
            TRY_CONVERT(BIGINT, s.f_Cheque_Amt) AS Amount, s.f_Cheque_Number AS ChequeNo, s.f_Drawn_Bank_Name AS BankName,
            ISNULL(s.F_CMS_Location_Name, N'') AS Branch, p.f_Payment_Mode AS PayMode, i.AppType,
            m.f_Submitted_On AS SubmittedOn, {LinkQueries.AcceptedOn} AS AcceptedOn, m.f_Paid_On AS PaidOn, m.f_Cancelled_On AS CancelledOn,
            slip.SlipNo
        FROM dbo.t_FD_BT_Payinslip_Staging_Hdr s
        JOIN dbo.t_Unotp_Application_Mst m ON m.f_App_No = s.f_Application_Number
        {ApplicationTableQueries.CurrentDepositOf}
        LEFT JOIN dbo.t_FD_BT_Payment_Dtl p ON p.f_Appl_No = m.f_App_No AND p.f_Active = 1
        OUTER APPLY (
            SELECT TOP (1) d.f_Payinslip_No AS SlipNo
            FROM dbo.t_FD_BT_Payinslip_Dtl d
            WHERE d.f_Application_Number = s.f_Application_Number
            ORDER BY d.f_Pk_t_BT_Payinslip_Dtl_ID DESC) slip
        WHERE m.f_Partner_Id = @Partner AND m.f_Status = 'APR' AND m.f_Active = 1
          AND s.f_Pk_t_BT_Payinslip_Staging_Hdr_ID = (
              SELECT MAX(n.f_Pk_t_BT_Payinslip_Staging_Hdr_ID) FROM dbo.t_FD_BT_Payinslip_Staging_Hdr n
              WHERE n.f_Application_Number = s.f_Application_Number)
        """;

    /// <summary>
    /// The next slip number: the prefix and a running number of at least four digits.
    /// </summary>
    public const string NextSlipNo = "SELECT @Prefix + RIGHT('0000' + CAST(NEXT VALUE FOR dbo.s_Slip_No AS VARCHAR(20)), 4)";

    /// <summary>
    /// A pay-in slip generated: its row in t_FD_BT_Payinslip_Hdr.
    /// </summary>
    public const string InsertHeader = """
        INSERT dbo.t_FD_BT_Payinslip_Hdr (f_Payinslip_No, f_Cheque_Count, f_Agency_Code,
            f_CMS_Bank_Name, f_CMS_Location_Code, f_CMS_Location_Desc,
            f_CreatedByName, f_CreatedIP, f_CreatedDate, f_SessionID)
        VALUES (@SlipNo, @ChequeCount, @AgencyCode,
            @CmsBankName, @CmsLocationCode, @CmsLocationName,
            @CreatedByName, @CreatedIp, GETDATE(), @SessionId)
        """;

    /// <summary>
    /// A pay-in slip generated: the cheque on it, in t_FD_BT_Payinslip_Dtl.
    /// </summary>
    public const string InsertDetail = """
        INSERT dbo.t_FD_BT_Payinslip_Dtl (f_Payinslip_No, f_Application_Number, f_Applicant_Name,
            f_Cheque_Number, f_Cheque_Date, f_Cheque_Amt,
            f_Drawn_Bank_Name, f_Drawn_Branch_Name, f_Drawn_Bank_MICR, f_Drawn_Bank_IFSCCOde,
            f_CMS_Bank_Name, f_cms_location_code, F_CMS_Location,
            f_CreatedByName, f_CreatedIP, f_CreatedDate, f_SessionID)
        VALUES (@SlipNo, @ApplicationNumber, @ApplicantName,
            @ChequeNumber, @ChequeDate, @ChequeAmount,
            @DrawnBankName, @DrawnBranchName, @DrawnBankMicr, @DrawnBankIfsc,
            @CmsBankName, @CmsLocationCode, @CmsLocationName,
            @CreatedByName, @CreatedIp, GETDATE(), @SessionId)
        """;

    /// <summary>
    /// The partner's cheque applications for Pay In Slip Generation, the one submitted last first:
    /// all of them, or the one @AppNo names.
    /// </summary>
    public static string StagedSlips(bool oneApplication) =>
        StagedSlip + (oneApplication ? " AND s.f_Application_Number = @AppNo" : "") + " ORDER BY m.f_Submitted_On DESC";
}
