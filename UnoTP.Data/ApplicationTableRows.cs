namespace UnoTP.Data;

// The rows the application tables are read into. Each SELECT names its columns
// after these properties, so Dapper maps them by name.

internal sealed class HeaderRow
{
    public const string Columns = """
        f_App_No AS AppNo, f_Status AS Status, f_Version AS Version,
        f_Upload_Ver AS UploadVer, f_Details_Ver AS DetailsVer, f_Payment_Ver AS PaymentVer, f_Deposit_Ver AS DepositVer,
        f_Pan AS Pan, f_Dob AS Dob, f_Name AS Name, f_Folio AS Folio, f_Gender AS Gender, f_Address AS Address, f_Data_Source AS DataSource,
        f_Pan_Filed AS PanFiled, f_Rec_Pan AS RecPan, f_Rec_Photo AS RecPhoto, f_Rec_Poa AS RecPoa,
        f_Renew_Dep_No AS RenewDepNo, f_Renew_Amount AS RenewAmount, f_Renew_Matures_On AS RenewMaturesOn,
        f_Renew_Rate AS RenewRate, f_Renew_Tenure AS RenewTenure, f_Renew_Payout AS RenewPayout, f_Renew_Principal AS RenewPrincipal,
        f_Submitted_On AS SubmittedOn, f_Sub_Status AS SubStatus, f_Link_Sent_To AS LinkSentTo,
        f_Link_Emailed_To AS LinkEmailedTo, f_Link_Valid_Until AS LinkValidUntil, f_Resends_Left AS ResendsLeft,
        f_Short_Url AS ShortUrl, f_Created_On AS CreatedOn
        """;

    public string AppNo { get; set; } = "";
    public string Status { get; set; } = "";
    public int Version { get; set; }
    public int? UploadVer { get; set; }
    public int? DetailsVer { get; set; }
    public int? PaymentVer { get; set; }
    public int? DepositVer { get; set; }
    public string Pan { get; set; } = "";
    public DateTime? Dob { get; set; }
    public string Name { get; set; } = "";
    public string Folio { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Address { get; set; } = "";
    public string? DataSource { get; set; }
    public bool PanFiled { get; set; }
    public bool? RecPan { get; set; }
    public bool? RecPhoto { get; set; }
    public bool? RecPoa { get; set; }
    public string? RenewDepNo { get; set; }
    public long? RenewAmount { get; set; }
    public DateTime? RenewMaturesOn { get; set; }
    public decimal? RenewRate { get; set; }
    public int? RenewTenure { get; set; }
    public string? RenewPayout { get; set; }
    public long? RenewPrincipal { get; set; }
    public DateTime? SubmittedOn { get; set; }
    public string? SubStatus { get; set; }
    public string? LinkSentTo { get; set; }
    public string? LinkEmailedTo { get; set; }
    public DateTime? LinkValidUntil { get; set; }
    public int? ResendsLeft { get; set; }
    public string? ShortUrl { get; set; }
    public DateTime CreatedOn { get; set; }

    public bool Submitted => Status == RowStatus.Approved;
}

internal sealed class KycRow
{
    // What t_FD_BT_Kyc_Data_Dtl holds of a holder. The name given is the father's,
    // the mother's or the spouse's: whichever was filled. The gender is read off
    // the name prefix (NamePrefixes).
    public const string Columns = """
        f_Holder_Type AS HolderType, f_Kyc_NamePrefix AS NamePrefix,
        CASE WHEN f_Kyc_FatherFullName IS NOT NULL THEN 'Father' WHEN f_Kyc_MotherFullName IS NOT NULL THEN 'Mother'
             WHEN f_Kyc_SpouseFullName IS NOT NULL THEN 'Spouse' ELSE '' END AS NameType,
        COALESCE(f_Kyc_FatherFullName, f_Kyc_MotherFullName, f_Kyc_SpouseFullName, N'') AS ParentName,
        f_Kyc_AnnualIncome_Desc AS AnnualIncome, f_CustSeg_Type_desc AS Occupation, f_CustSeg_Subtype_Desc AS SubOccupation,
        f_Kyc_MaritalStatus AS MaritalStatus,
        CASE f_IsPEP WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS Pep,
        CASE f_IsPEP_Relative WHEN 1 THEN 'yes' WHEN 0 THEN 'no' ELSE '' END AS PepRelated
        """;

    public string HolderType { get; set; } = "";
    public string NamePrefix { get; set; } = "";
    public string NameType { get; set; } = "";
    public string ParentName { get; set; } = "";
    public string AnnualIncome { get; set; } = "";
    public string Occupation { get; set; } = "";
    public string SubOccupation { get; set; } = "";
    public string MaritalStatus { get; set; } = "";
    public string Pep { get; set; } = "";
    public string PepRelated { get; set; } = "";
}

internal sealed class AddressRow
{
    // The permanent address's row also carries the holder's mobile and e-mail.
    public const string Columns = """
        f_Holder_Type AS HolderType, f_AddType_Code AS AddrType, f_Add1 AS Line1, f_Add2 AS Line2, f_Add3 AS Line3,
        f_AddCity_Desc AS City, f_AddPin AS PinCode, f_AddDistrict_Desc AS District, f_AddState_Desc AS State,
        f_MobileNumber AS Mobile, f_EmailAdd AS Email
        """;

    public string HolderType { get; set; } = "";
    public string AddrType { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";
    public string Line3 { get; set; } = "";
    public string City { get; set; } = "";
    public string PinCode { get; set; } = "";
    public string District { get; set; } = "";
    public string State { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Email { get; set; } = "";
}

internal sealed class NomineeRow
{
    public const string Columns = """
        f_Nominee_Name AS Name, f_Nominee_DOB AS Dob, f_Nominee_Relations AS Relation, f_GuardianName AS GuardianName,
        f_Address1 AS GuardianLine1, f_Address2 AS GuardianLine2, f_Address3 AS GuardianLine3,
        f_PIN AS GuardianPinCode, f_City AS GuardianCity
        """;

    public string Name { get; set; } = "";
    public DateTime? Dob { get; set; }
    public string Relation { get; set; } = "";
    public string GuardianName { get; set; } = "";
    public string GuardianLine1 { get; set; } = "";
    public string GuardianLine2 { get; set; } = "";
    public string GuardianLine3 { get; set; } = "";
    public string GuardianPinCode { get; set; } = "";
    public string GuardianCity { get; set; } = "";
}

internal sealed class PaymentBankRow
{
    public const string Columns = """
        f_Bank_NEFT AS Ifsc, f_BankAccountNo AS AccountNo, f_Cheque_DD_No AS ChequeNo, f_Cheque_DD_Date AS ChequeDate,
        f_CMS_Loc_Desc AS CmsLocation, f_CMS_Loc_CD AS CmsLocationCode
        """;

    public string? Ifsc { get; set; }
    public string? AccountNo { get; set; }
    public string? ChequeNo { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? CmsLocation { get; set; }
    public string? CmsLocationCode { get; set; }
}

internal sealed class RepaymentBankRow
{
    // The account's columns take no NULL: one not given is blank there, and NULL here.
    public const string Columns = """
        ISNULL(f_sameAsCheque, 0) AS SameAsPayment, NULLIF(f_NEFTCode, N'') AS Ifsc, NULLIF(f_BankAccountNo, N'') AS AccountNo
        """;

    public bool SameAsPayment { get; set; }
    public string? Ifsc { get; set; }
    public string? AccountNo { get; set; }
}

internal sealed class InvestmentRow
{
    public const string Columns = """
        CAST(f_Amount AS BIGINT) AS Amount, TRY_CAST(f_Tenure AS INT) AS TenureMonths, f_Int_Freq AS Payout,
        f_Is_Auto_Renewal AS AutoRenewal,
        CASE f_Renewal_For WHEN 'P' THEN 'principal' WHEN 'F' THEN 'principal-interest' ELSE '' END AS RenewInstruction,
        CASE f_ExistingFDRNoRenewalFor WHEN 'P' THEN 'principal' WHEN 'F' THEN 'principal-interest' ELSE '' END AS RenewalFor,
        CAST(CASE WHEN f_TDS_Flag = 'N' THEN 1 ELSE 0 END AS BIT) AS NoTds, f_FDR_Dispatch_Mode AS DeliveryType,
        f_AML_Source_Of_Funds AS SourceOfFunds, f_AML_Source_Of_Funds_Remarks AS SourceOfFundsRemark,
        f_AML_Source_Of_Funds_reason AS SourceOfFundsReason
        """;

    /// <summary>
    /// The application's current deposit row as i, for a query over t_Unotp_Application_Mst m:
    /// its active row. The newest is taken, should there ever be two.
    /// </summary>
    public const string CurrentOf = """
        OUTER APPLY (
            SELECT TOP (1) CAST(d.f_Amount AS BIGINT) AS Amount, TRY_CAST(d.f_Tenure AS INT) AS TenureMonths,
                d.f_Int_Freq AS Payout, d.f_ApplicationDeclarationType AS AppType
            FROM dbo.t_FD_BT_Investment_Dtl d
            WHERE d.f_Appl_No = m.f_App_No AND d.f_Active = 1
            ORDER BY d.f_Pk_t_FD_BT_Investment_Dtl_Id DESC) i
        """;

    public long Amount { get; set; }
    public int TenureMonths { get; set; }
    public string Payout { get; set; } = "";
    public bool AutoRenewal { get; set; }
    public string RenewInstruction { get; set; } = "";
    public bool NoTds { get; set; }
    public string DeliveryType { get; set; } = "";
    public string SourceOfFunds { get; set; } = "";
    public string SourceOfFundsRemark { get; set; } = "";
    public string SourceOfFundsReason { get; set; } = "";
    public string RenewalFor { get; set; } = "";
}

/// <summary>One application as View Application lists it, before it is masked.</summary>
internal sealed class ListRow
{
    public string AppNo { get; set; } = "";
    public string Status { get; set; } = "";
    public string Folio { get; set; } = "";
    public string Name { get; set; } = "";
    public string Pan { get; set; } = "";
    public DateTime? Dob { get; set; }
    public long? Amount { get; set; }
    public int? TenureMonths { get; set; }
    public string? Payout { get; set; }
    public string? AppType { get; set; }
    public string? PayMode { get; set; }
    public int JointHolders { get; set; }
    public int? UploadVer { get; set; }
    public int? DetailsVer { get; set; }
    public int? PaymentVer { get; set; }
    public int? DepositVer { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? UploadedOn { get; set; }
    public DateTime? SubmittedOn { get; set; }
    public DateTime? AcceptedOn { get; set; }
    public DateTime? PaidOn { get; set; }
    public DateTime? BookedOn { get; set; }
    public string? FdrNo { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string Branch { get; set; } = "";
    public DateTime? LinkSentOn { get; set; }
    public DateTime? SlipOn { get; set; }
    public DateTime? PennyDropOn { get; set; }
    public string PennyDropStatus { get; set; } = "";
    public DateTime? KycVerifiedOn { get; set; }
    public string KycStatus { get; set; } = "";
}

internal sealed class DraftRow
{
    public string AppNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string Pan { get; set; } = "";
    public DateTime? Dob { get; set; }
    public long Amount { get; set; }
    public int? UploadVer { get; set; }
    public int? DetailsVer { get; set; }
    public int? PaymentVer { get; set; }
    public int? DepositVer { get; set; }
    public int MinutesAgo { get; set; }
    public string? Renews { get; set; }
}
