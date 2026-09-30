namespace UnoTP.Data;

// The rows the application tables are read into. Each SELECT names its columns
// after these properties, so Dapper maps them by name.

internal sealed class HeaderRow
{
    public const string Columns = """
        c_App_No AS AppNo, c_Status AS Status, n_Version AS Version,
        n_Upload_Ver AS UploadVer, n_Details_Ver AS DetailsVer, n_Payment_Ver AS PaymentVer, n_Deposit_Ver AS DepositVer,
        c_Pan AS Pan, d_Dob AS Dob, c_Name AS Name, c_Folio AS Folio, c_Gender AS Gender, c_Address AS Address,
        f_Pan_Filed AS PanFiled, f_Rec_Pan AS RecPan, f_Rec_Photo AS RecPhoto, f_Rec_Poa AS RecPoa,
        c_Renew_Dep_No AS RenewDepNo, n_Renew_Amount AS RenewAmount, d_Renew_Matures_On AS RenewMaturesOn,
        n_Renew_Rate AS RenewRate, n_Renew_Tenure AS RenewTenure, c_Renew_Payout AS RenewPayout,
        d_Submitted_On AS SubmittedOn, c_Sub_Status AS SubStatus, c_Link_Sent_To AS LinkSentTo,
        c_Link_Emailed_To AS LinkEmailedTo, d_Link_Valid_Until AS LinkValidUntil, n_Resends_Left AS ResendsLeft,
        c_Short_Url AS ShortUrl, d_Created_On AS CreatedOn, d_Updated_On AS UpdatedOn
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
    public DateTime? SubmittedOn { get; set; }
    public string? SubStatus { get; set; }
    public string? LinkSentTo { get; set; }
    public string? LinkEmailedTo { get; set; }
    public DateTime? LinkValidUntil { get; set; }
    public int? ResendsLeft { get; set; }
    public string? ShortUrl { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }

    public bool Submitted => Status == RowStatus.Approved;
}

internal sealed class KycRow
{
    public const string Columns = """
        c_Holder_Type AS HolderType, c_Pan AS Pan, d_Dob AS Dob, c_Name AS Name, c_Folio AS Folio,
        c_Gender AS Gender, c_Name_Type AS NameType, c_Parent_Name AS ParentName, c_Annual_Income AS AnnualIncome,
        c_Occupation AS Occupation, c_Sub_Occupation AS SubOccupation, c_Marital_Status AS MaritalStatus,
        c_Mobile AS Mobile, c_Email AS Email, f_Fatca_Tax_Res AS FatcaTaxResident, f_Fatca_Perm_Res AS FatcaPermanentResident,
        c_Pep AS Pep, c_Pep_Related AS PepRelated
        """;

    public string HolderType { get; set; } = "";
    public string Pan { get; set; } = "";
    public DateTime? Dob { get; set; }
    public string Name { get; set; } = "";
    public string Folio { get; set; } = "";
    public string Gender { get; set; } = "";
    public string NameType { get; set; } = "";
    public string ParentName { get; set; } = "";
    public string AnnualIncome { get; set; } = "";
    public string Occupation { get; set; } = "";
    public string SubOccupation { get; set; } = "";
    public string MaritalStatus { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Email { get; set; } = "";
    public bool FatcaTaxResident { get; set; }
    public bool FatcaPermanentResident { get; set; }
    public string Pep { get; set; } = "";
    public string PepRelated { get; set; } = "";
}

internal sealed class AddressRow
{
    public const string Columns = """
        c_Holder_Type AS HolderType, c_Addr_Type AS AddrType, c_Line1 AS Line1, c_Line2 AS Line2, c_Line3 AS Line3,
        c_City AS City, c_Pin_Code AS PinCode, c_District AS District, c_State AS State
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
}

internal sealed class NomineeRow
{
    public const string Columns = """
        c_Name AS Name, d_Dob AS Dob, c_Relation AS Relation, c_Guardian_Name AS GuardianName,
        c_Guardian_Line1 AS GuardianLine1, c_Guardian_Line2 AS GuardianLine2, c_Guardian_Line3 AS GuardianLine3,
        c_Guardian_Pin_Code AS GuardianPinCode, c_Guardian_City AS GuardianCity
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
        c_Ifsc AS Ifsc, c_Account_No AS AccountNo, c_Cheque_No AS ChequeNo, d_Cheque_Date AS ChequeDate,
        c_Cms_Location AS CmsLocation
        """;

    public string? Ifsc { get; set; }
    public string? AccountNo { get; set; }
    public string? ChequeNo { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? CmsLocation { get; set; }
}

internal sealed class RepaymentBankRow
{
    public const string Columns = "f_Same_As_Payment AS SameAsPayment, c_Ifsc AS Ifsc, c_Account_No AS AccountNo";

    public bool SameAsPayment { get; set; }
    public string? Ifsc { get; set; }
    public string? AccountNo { get; set; }
}

internal sealed class InvestmentRow
{
    public const string Columns = """
        n_Amount AS Amount, n_Tenure_Months AS TenureMonths, c_Payout AS Payout, f_Auto_Renewal AS AutoRenewal,
        c_Renew_Instruction AS RenewInstruction, f_No_Tds AS NoTds, c_Delivery_Type AS DeliveryType
        """;

    public long Amount { get; set; }
    public int TenureMonths { get; set; }
    public string Payout { get; set; } = "";
    public bool AutoRenewal { get; set; }
    public string RenewInstruction { get; set; } = "";
    public bool NoTds { get; set; }
    public string DeliveryType { get; set; } = "";
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
}
