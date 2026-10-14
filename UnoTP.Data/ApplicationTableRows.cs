namespace UnoTP.Data;

// The rows the application tables are read into. Each SELECT names its columns
// after these properties, so Dapper maps them by name.

internal sealed class HeaderRow
{
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
    public DateTime? CancelledOn { get; set; }

    public bool Submitted => Status == RowStatus.Approved;
    public bool Cancelled => CancelledOn is not null;
}

internal sealed class KycRow
{
    public string HolderType { get; set; } = "";
    public string NamePrefix { get; set; } = "";

    /// <summary>f_Kyc_Gender: M, F or T; null on a row written before the column was filled.</summary>
    public string? Gender { get; set; }
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

    /// <summary>Whether another row holds the same address: its lines, city, district, state and PIN code.</summary>
    public bool SameAddressAs(AddressRow other) =>
        (Line1, Line2, Line3, City, District, State, PinCode) == (other.Line1, other.Line2, other.Line3, other.City, other.District, other.State, other.PinCode);
}

internal sealed class NomineeRow
{
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
    public string? Ifsc { get; set; }
    public string? AccountNo { get; set; }
    public string? ChequeNo { get; set; }
    public DateTime? ChequeDate { get; set; }
    public string? CmsLocation { get; set; }
    public string? CmsLocationCode { get; set; }
}

internal sealed class RepaymentBankRow
{
    public bool SameAsPayment { get; set; }
    public string? Ifsc { get; set; }
    public string? AccountNo { get; set; }
}

internal sealed class InvestmentRow
{
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
