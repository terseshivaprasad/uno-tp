using System.Globalization;
using UnoTP.Data;

namespace UnoTP.Models;

/// <summary>
/// One row of t_FD_BT_Payinslip_Hdr: a pay-in slip as it was generated. Each
/// property is one column of the table, named in its note. A slip here carries one
/// application's cheque. What the staging row does not hold is left empty.
/// </summary>
public sealed record PayInSlipHeaderRow
{
    /// <summary>f_Payinslip_No: the slip's number, which its detail row carries too.</summary>
    public required string SlipNo { get; init; }

    /// <summary>f_Cheque_Count: the cheques on the slip.</summary>
    public int ChequeCount { get; init; } = 1;

    /// <summary>f_Agency_Code.</summary>
    public string? AgencyCode { get; init; }

    /// <summary>f_CMS_Bank_Name.</summary>
    public string? CmsBankName { get; init; }

    /// <summary>f_CMS_Location_Code: the Axis CMS branch the cheque is presented at.</summary>
    public string? CmsLocationCode { get; init; }

    /// <summary>f_CMS_Location_Desc.</summary>
    public string? CmsLocationName { get; init; }

    /// <summary>f_CreatedByName: the signed-in user who generated the slip.</summary>
    public string? CreatedByName { get; init; }

    /// <summary>f_CreatedIP.</summary>
    public string? CreatedIp { get; init; }

    /// <summary>f_SessionID: the user's backend session, when it is a number.</summary>
    public long? SessionId { get; init; }

    /// <summary>The slip made, under <paramref name="slipNo"/>, for an application waiting in the staging table.</summary>
    public static PayInSlipHeaderRow For(PayInSlipStagingRow staged, string slipNo, IPartner by) => new()
    {
        SlipNo = slipNo,
        AgencyCode = staged.AgencyCode,
        CmsBankName = staged.CmsBankName,
        CmsLocationCode = staged.CmsLocationCode,
        CmsLocationName = staged.CmsLocationName,
        CreatedByName = PayInSlipDetailRow.EmptyAsNull(by.UserName),
        CreatedIp = PayInSlipDetailRow.EmptyAsNull(by.IpAddress),
        SessionId = PayInSlipDetailRow.SessionNumber(by),
    };
}

/// <summary>
/// One row of t_FD_BT_Payinslip_Dtl: a cheque on a pay-in slip. Each property is
/// one column of the table, named in its note. The cheque's date and amount, kept
/// as text in the staging table, are a date and a number here.
/// </summary>
public sealed record PayInSlipDetailRow
{
    /// <summary>f_Payinslip_No: the number of the slip the cheque is on.</summary>
    public required string SlipNo { get; init; }

    /// <summary>f_Application_Number.</summary>
    public required string ApplicationNumber { get; init; }

    /// <summary>f_Applicant_Name.</summary>
    public string? ApplicantName { get; init; }

    /// <summary>f_Cheque_Number.</summary>
    public string? ChequeNumber { get; init; }

    /// <summary>f_Cheque_Date.</summary>
    public DateTime? ChequeDate { get; init; }

    /// <summary>f_Cheque_Amt: whole rupees.</summary>
    public long? ChequeAmount { get; init; }

    /// <summary>f_Drawn_Bank_Name.</summary>
    public string? DrawnBankName { get; init; }

    /// <summary>f_Drawn_Branch_Name.</summary>
    public string? DrawnBranchName { get; init; }

    /// <summary>f_Drawn_Bank_MICR.</summary>
    public string? DrawnBankMicr { get; init; }

    /// <summary>f_Drawn_Bank_IFSCCOde.</summary>
    public string? DrawnBankIfsc { get; init; }

    /// <summary>f_CMS_Bank_Name.</summary>
    public string? CmsBankName { get; init; }

    /// <summary>f_cms_location_code.</summary>
    public string? CmsLocationCode { get; init; }

    /// <summary>F_CMS_Location: the location's name.</summary>
    public string? CmsLocationName { get; init; }

    /// <summary>f_CreatedByName.</summary>
    public string? CreatedByName { get; init; }

    /// <summary>f_CreatedIP.</summary>
    public string? CreatedIp { get; init; }

    /// <summary>f_SessionID.</summary>
    public long? SessionId { get; init; }

    /// <summary>The cheque of an application waiting in the staging table, on the slip <paramref name="slipNo"/>.</summary>
    public static PayInSlipDetailRow For(PayInSlipStagingRow staged, string slipNo, IPartner by) => new()
    {
        SlipNo = slipNo,
        ApplicationNumber = staged.ApplicationNumber,
        ApplicantName = staged.ApplicantName,
        ChequeNumber = staged.ChequeNumber,
        ChequeDate = Dates.ParseDdMmYyyy(staged.ChequeDate),
        ChequeAmount = WholeRupees(staged.ChequeAmount),
        DrawnBankName = staged.DrawnBankName,
        DrawnBranchName = staged.DrawnBranchName,
        DrawnBankMicr = staged.DrawnBankMicr,
        DrawnBankIfsc = staged.DrawnBankIfsc,
        CmsBankName = staged.CmsBankName,
        CmsLocationCode = staged.CmsLocationCode,
        CmsLocationName = staged.CmsLocationName,
        CreatedByName = EmptyAsNull(by.UserName),
        CreatedIp = EmptyAsNull(by.IpAddress),
        SessionId = SessionNumber(by),
    };

    // The amount the staging table holds as digits; null when it is not a whole number.
    private static long? WholeRupees(string? digits)
    {
        if (long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)) return amount;
        return null;
    }

    // The user's backend session, when it is a number; null otherwise.
    internal static long? SessionNumber(IPartner by)
    {
        if (long.TryParse(by.SessionId, out var number)) return number;
        return null;
    }

    // Nothing to write leaves the column empty.
    internal static string? EmptyAsNull(string value)
    {
        if (value.Length == 0) return null;
        return value;
    }
}
