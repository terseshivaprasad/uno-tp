using System.Globalization;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// One row of the FD system's renewal transaction table, t_FD_BT_RenewalDtls
/// (database MMFSL_APP_FD_BTP): a renewal submitted here, written once its four
/// steps are complete, at submit. Each property is one column, named in its note;
/// what the application does not hold is left empty. The FD system's Renew FD list
/// leaves out a deposit with a live row here, so the deposit is listed again only
/// once the row goes out of use (RenewFdQueries.RetireRenewalRows).
/// </summary>
internal sealed record RenewalRow
{
    /// <summary>STEPS_COMPLETED: the four steps a renewal goes through here before submit.</summary>
    public const int StepsCompleted = 4;

    /// <summary>f_Source: this app.</summary>
    public const string Source = "UNO_TP_V2";

    /// <summary>APPLNO and NewAppNo: the renewal's application number.</summary>
    public required string AppNo { get; init; }

    /// <summary>F_Existing_FDR_No: the deposit being renewed.</summary>
    public required string DepositNumber { get; init; }

    /// <summary>FOLIO.</summary>
    public string Folio { get; init; } = "";

    /// <summary>HOLDER1_NAME, HOLDER2_NAME, HOLDER3_NAME.</summary>
    public string Holder1Name { get; init; } = "";
    public string? Holder2Name { get; init; }
    public string? Holder3Name { get; init; }

    /// <summary>ADDRESS1 to ADDRESS3, PINCODE, STATE_DES, DISTRICT_DESC and VILLA_CITY_DESC: the investor's permanent address.</summary>
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? Address3 { get; init; }
    public string? PinCode { get; init; }
    public string? State { get; init; }
    public string? District { get; init; }
    public string? City { get; init; }

    /// <summary>MOBILE and EMAIL: the investor's, from Investor Information.</summary>
    public string? Mobile { get; init; }
    public string? Email { get; init; }

    /// <summary>PAN, DOB and AGE (whole years today) of the investor.</summary>
    public string Pan { get; init; } = "";
    public DateTime? Dob { get; init; }
    public string? Age { get; init; }

    /// <summary>G_NAME: the father's, mother's or spouse's name given on Investor Information. TO CONFIRM: that G_NAME is this name.</summary>
    public string? ParentName { get; init; }

    /// <summary>SCHEME and SCHEME_CODE, from the rate card's row for the deposit; INT_FREQ (the payout code), TENURE (months), CATEGORY.</summary>
    public string? Scheme { get; init; }
    public string? SchemeCode { get; init; }
    public string PayoutCode { get; init; } = "";
    public string Tenure { get; init; } = "";
    public string? Category { get; init; }

    /// <summary>RENEWAL_OPTION: P, the principal; F, principal and interest - as f_ExistingFDRNoRenewalFor on the investment row.</summary>
    public string? RenewalOption { get; init; }

    /// <summary>AMOUNT and INT_RATE.</summary>
    public long Amount { get; init; }
    public decimal? Rate { get; init; }

    /// <summary>MICR, NEFT, BANK_NAME, BRANCH and ACCOUNT_NUMBER: the repayment account.</summary>
    public string? Micr { get; init; }
    public string? Ifsc { get; init; }
    public string? BankName { get; init; }
    public string? BranchName { get; init; }
    public string? AccountNumber { get; init; }

    /// <summary>NOM_NAME, NOM_DOB, NOM_RELATION, NOM_GUARDIAN and NOM_ADDRESS (the guardian's address, one line).</summary>
    public string? NomineeName { get; init; }
    public DateTime? NomineeDob { get; init; }
    public string? NomineeRelation { get; init; }
    public string? NomineeGuardian { get; init; }
    public string? NomineeAddress { get; init; }

    /// <summary>KYC_*_FILENAME and APPL_FORM: the file names of the copies filed here, by what each is.</summary>
    public string? ChequeFile { get; init; }
    public string? PassportFile { get; init; }
    public string? VoterIdFile { get; init; }
    public string? DrivingLicenceFile { get; init; }
    public string? AadhaarFile { get; init; }
    public string? PanFile { get; init; }
    public string? FormFile { get; init; }

    /// <summary>PAPERLESS_DISC: a digital application, accepted through the investor's own link.</summary>
    public bool Paperless { get; init; }

    /// <summary>BROKER_CODE: the sourcing code typed; OldBROKER_CODE: the deposit's broker on the register.</summary>
    public string? BrokerCode { get; init; }
    public string? OldBrokerCode { get; init; }

    /// <summary>f_Sourcing_Mode_Code and f_Sourcing_Mode_Desc, f_Sub_Broker_Code, f_Agency_Code.</summary>
    public string? SourcingModeCode { get; init; }
    public string? SourcingModeName { get; init; }
    public string? SubBrokerCode { get; init; }
    public string? AgencyCode { get; init; }

    /// <summary>
    /// The row for a renewal as it is submitted.
    /// </summary>
    /// <param name="app">The application, as assembled at submit.</param>
    /// <param name="depositNumber">The deposit it renews.</param>
    /// <param name="oldBrokerCode">The deposit's broker code on the register.</param>
    /// <param name="branches">The branch each IFSC names, as looked up before the save.</param>
    /// <param name="line">The rate card's row for the deposit; null where the card offers none.</param>
    /// <param name="addresses">The permanent address each holder on a folio has on record, by holder.</param>
    /// <param name="lists">The reference lists: the sourcing modes, for the mode's name.</param>
    public static RenewalRow For(Application app, string depositNumber, string? oldBrokerCode, IReadOnlyDictionary<string, BankBranch> branches,
        RateOption? line, IReadOnlyDictionary<string, TypedAddress> addresses, ReferenceData lists, IPartner by)
    {
        var upload = app.Upload ?? new UploadState();
        var holder = app.Holder;
        var investor = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
        var nominee = app.Details?.Nominee;
        var deposit = app.Deposit;
        var repayment = app.Payment?.Repayment;
        var branch = repayment is null ? null : branches.GetValueOrDefault(repayment.Ifsc.Trim().ToUpperInvariant());
        var born = Dates.ParseDdMmYyyy(holder.Dob);
        var address = PermanentAddressOf(holder, upload, addresses);
        var mode = lists.SourcingModes.FirstOrDefault(m => m.Code == upload.Sourcing);

        return new RenewalRow
        {
            AppNo = app.AppNo, DepositNumber = depositNumber, Folio = holder.Folio,
            Holder1Name = upload.Name.Length > 0 ? upload.Name : holder.Name,
            Holder2Name = upload.Joint.GetValueOrDefault("02")?.Holder.Name, Holder3Name = upload.Joint.GetValueOrDefault("03")?.Holder.Name,
            Address1 = address.Line1, Address2 = EmptyAsNull(address.Line2), Address3 = EmptyAsNull(address.Line3),
            PinCode = EmptyAsNull(address.PinCode), State = EmptyAsNull(address.State), District = EmptyAsNull(address.District), City = EmptyAsNull(address.City),
            Mobile = EmptyAsNull(investor?.Mobile ?? ""), Email = EmptyAsNull(investor?.Email ?? ""),
            Pan = holder.Pan, Dob = born, Age = born is null ? null : AgeToday(born.Value).ToString(CultureInfo.InvariantCulture),
            ParentName = EmptyAsNull(investor?.ParentName ?? ""),
            Scheme = Cut(line?.Scheme, 20), SchemeCode = EmptyAsNull(line?.SchemeCode ?? ""),
            PayoutCode = deposit?.Payout ?? "", Tenure = (deposit?.TenureMonths ?? 0).ToString(CultureInfo.InvariantCulture), Category = EmptyAsNull(upload.Category),
            RenewalOption = PrincipalOrFull(deposit?.RenewalFor ?? ""),
            Amount = deposit?.Amount ?? 0, Rate = line?.Rate,
            Micr = branch?.Micr, Ifsc = repayment?.Ifsc, BankName = Cut(branch?.Bank, 100), BranchName = Cut(branch?.Branch, 200), AccountNumber = repayment?.AccountNumber,
            NomineeName = EmptyAsNull(nominee?.Name ?? ""), NomineeDob = Dates.ParseDdMmYyyy(nominee?.Dob), NomineeRelation = EmptyAsNull(nominee?.Relation ?? ""),
            NomineeGuardian = EmptyAsNull(nominee?.GuardianName ?? ""), NomineeAddress = GuardianAddress(nominee),
            ChequeFile = FileOf(upload, "payment"), PanFile = FileOf(upload, "pan"), FormFile = FileOf(upload, "form"),
            PassportFile = PoaFileOf(upload, "Passport"), VoterIdFile = PoaFileOf(upload, "Voter ID"),
            DrivingLicenceFile = PoaFileOf(upload, "Driving Licence"), AadhaarFile = PoaFileOf(upload, "Aadhaar"),
            Paperless = upload.AppType == ApplicationType.Digital,
            BrokerCode = EmptyAsNull(upload.SourceCode), OldBrokerCode = EmptyAsNull(oldBrokerCode?.Trim() ?? ""),
            SourcingModeCode = EmptyAsNull(upload.Sourcing), SourcingModeName = mode?.Name, SubBrokerCode = EmptyAsNull(upload.SubBroker),
            AgencyCode = EmptyAsNull(by.AgencyCode),
        };
    }

    // The investor's permanent address: the one on record for a folio holder, else the
    // one read off the proof filed here, its PIN code at the end.
    private static TypedAddress PermanentAddressOf(Holder holder, UploadState upload, IReadOnlyDictionary<string, TypedAddress> addresses)
    {
        if (addresses.TryGetValue(HolderType.Investor, out var onRecord)) return onRecord;
        var (body, pin) = Addresses.SplitPin(upload.Address.Length > 0 ? upload.Address : holder.Address);
        return new TypedAddress(body, "", "", "", pin, upload.District, upload.State);
    }

    // The file name of the copy filed under a key ("pan", "payment", "form"), if one is.
    private static string? FileOf(UploadState upload, string key) => upload.Docs.GetValueOrDefault(key)?.FileName;

    // The investor's proof of address, when it is of the type given.
    private static string? PoaFileOf(UploadState upload, string type) => upload.PoaType == type ? FileOf(upload, "poa") : null;

    private static string? GuardianAddress(NomineeDetails? nominee)
    {
        if (nominee is null) return null;
        var parts = new[] { nominee.GuardianLine1, nominee.GuardianLine2, nominee.GuardianLine3, nominee.GuardianCity, nominee.GuardianPinCode }
            .Where(p => p.Length > 0);
        return EmptyAsNull(string.Join(", ", parts));
    }

    // RENEWAL_OPTION as the investment row codes it: P, the principal; F, principal and interest.
    private static string? PrincipalOrFull(string renewalFor)
    {
        if (renewalFor == RenewalChoice.Principal) return "P";
        if (renewalFor == RenewalChoice.PrincipalInterest) return "F";
        return null;
    }

    private static int AgeToday(DateTime born)
    {
        var today = DateTime.Today;
        var age = today.Year - born.Year;
        if (born.Date > today.AddYears(-age)) age--;
        return age;
    }

    private static string? Cut(string? value, int length)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length <= length ? value : value[..length];
    }

    private static string? EmptyAsNull(string value) => value.Length == 0 ? null : value;
}
