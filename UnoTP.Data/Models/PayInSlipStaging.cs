using System.Globalization;

namespace UnoTP.Models;

/// <summary>
/// One row of t_FD_BT_Payinslip_Staging_Hdr: a cheque application waiting for its
/// pay-in slip. Each property is one column of the table, named in its note. What
/// the application does not hold is left empty.
/// </summary>
public sealed record PayInSlipStagingRow
{
    /// <summary>What f_Queue_Status says of a row that has just been put in the queue.</summary>
    public const string Waiting = "PEN";

    /// <summary>The bank a cheque is presented at (f_CMS_Bank_Name), as t_FD_BT_Payment_Dtl names it.</summary>
    public const string CmsBank = "Axis Bank";

    /// <summary>f_Application_Number.</summary>
    public required string ApplicationNumber { get; init; }

    /// <summary>f_Applicant_Name: the first holder's name, as their KYC row has it.</summary>
    public required string ApplicantName { get; init; }

    /// <summary>f_Agency_Code: the signed-in user's agency code.</summary>
    public string? AgencyCode { get; init; }

    /// <summary>f_Cheque_Number.</summary>
    public string? ChequeNumber { get; init; }

    /// <summary>f_Cheque_Date, as Bank Details &amp; Payment holds it: dd-MM-yyyy.</summary>
    public string? ChequeDate { get; init; }

    /// <summary>f_Cheque_Amt: the deposit's amount in whole rupees, digits only.</summary>
    public string? ChequeAmount { get; init; }

    /// <summary>f_Drawn_Bank_Name: the bank of the account the cheque is drawn on.</summary>
    public string? DrawnBankName { get; init; }

    /// <summary>f_Drawn_Branch_Name.</summary>
    public string? DrawnBranchName { get; init; }

    /// <summary>f_Drawn_Bank_MICR.</summary>
    public string? DrawnBankMicr { get; init; }

    /// <summary>f_Drawn_Bank_IFSCCOde.</summary>
    public string? DrawnBankIfsc { get; init; }

    /// <summary>f_CMS_Bank_Name.</summary>
    public string? CmsBankName { get; init; }

    /// <summary>F_CMS_Location_Code: the Axis CMS branch the cheque is presented at.</summary>
    public string? CmsLocationCode { get; init; }

    /// <summary>F_CMS_Location_Name.</summary>
    public string? CmsLocationName { get; init; }

    /// <summary>f_Queue_Status.</summary>
    public string QueueStatus { get; init; } = Waiting;

    /// <summary>
    /// The row for an application as it was submitted.
    /// </summary>
    /// <param name="branches">The branch each IFSC names, as looked up before the save.</param>
    /// <param name="agencyCode">The signed-in user's agency code; empty when not known.</param>
    public static PayInSlipStagingRow For(Application app, IReadOnlyDictionary<string, BankBranch> branches, string agencyCode)
    {
        var account = app.Payment?.Payment;
        var cheque = app.Payment?.Cheque;
        BankBranch? drawnOn = null;
        if (account is not null) drawnOn = branches.GetValueOrDefault(account.Ifsc.Trim().ToUpperInvariant());

        // The investor takes the name NSDL verified, when they came with no folio:
        // the name their KYC row is written with.
        var applicant = app.Holder.Name;
        if (app.Upload is { Name.Length: > 0 } upload) applicant = upload.Name;

        return new PayInSlipStagingRow
        {
            ApplicationNumber = app.AppNo,
            ApplicantName = applicant,
            AgencyCode = EmptyAsNull(agencyCode),
            ChequeNumber = EmptyAsNull(cheque?.Number),
            ChequeDate = EmptyAsNull(cheque?.Date),
            ChequeAmount = app.Deposit?.Amount.ToString(CultureInfo.InvariantCulture),
            DrawnBankName = EmptyAsNull(drawnOn?.Bank),
            DrawnBranchName = EmptyAsNull(drawnOn?.Branch),
            DrawnBankMicr = EmptyAsNull(drawnOn?.Micr),
            DrawnBankIfsc = EmptyAsNull(account?.Ifsc),
            CmsBankName = cheque is null ? null : CmsBank,
            CmsLocationCode = EmptyAsNull(cheque?.CmsLocationCode),
            CmsLocationName = EmptyAsNull(cheque?.CmsLocation),
        };
    }

    // Nothing to write leaves the column empty.
    private static string? EmptyAsNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }
}
