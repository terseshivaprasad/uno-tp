using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>The kinds of address t_FD_BT_Address_Dtl holds for a holder (f_AddType_Code).</summary>
internal static class AddressType
{
    /// <summary>As on record for the holder. Its row also carries the holder's mobile and e-mail.</summary>
    public const string Permanent = "PER";

    /// <summary>The mailing address, typed on Investor Information when post goes elsewhere.</summary>
    public const string Communication = "MAIL";
}

/// <summary>
/// f_Kyc_NamePrefix: t_FD_BT_Kyc_Data_Dtl keeps a holder's gender as the prefix to
/// their name, worked out from the gender and marital status, and it is read back
/// off the prefix.
/// </summary>
internal static class NamePrefixes
{
    /// <param name="gender">As a folio, an Aadhaar or Investor Information gives it: "F", "FEMALE" or "Female".</param>
    public static string Of(string gender, string maritalStatus)
    {
        var known = Genders.Of(gender);
        if (known == Genders.Male) return "Mr";
        if (known == Genders.Female && maritalStatus == "Married") return "Mrs";
        if (known == Genders.Female) return "Miss";
        if (known == Genders.Transgender) return "Mx";
        return "";
    }

    public static string GenderOf(string prefix)
    {
        if (prefix == "Mr") return Genders.Male;
        if (prefix == "Mrs" || prefix == "Miss") return Genders.Female;
        if (prefix == "Mx") return Genders.Transgender;
        return "";
    }
}

// Investor Information: t_FD_BT_Kyc_Data_Dtl, t_FD_BT_Address_Dtl, t_FD_BT_Nominee_Dtl.
// The FATCA answers have no column in them: the page keeps them with its typed
// fields (t_Unotp_Page_State), and a "yes" stops the application going on online.
internal static partial class Sections
{
    /// <param name="minorUnder">The age under which a nominee is a minor (the minimum age).</param>
    /// <param name="masters">The FD system's masters, for the code it gives a marital status and a nominee's relation.</param>
    public static async Task WriteDetailsAsync(IDbConnection db, IDbTransaction tx, Stamp at, Holder investor, UploadState? upload,
        ApplicationDetails details, int minorUnder, MasterLists masters)
    {
        await RetireAsync(db, tx, at, "t_FD_BT_Kyc_Data_Dtl");
        await RetireAsync(db, tx, at, "t_FD_BT_Address_Dtl");
        await RetireAsync(db, tx, at, "t_FD_BT_Nominee_Dtl");

        foreach (var h in details.Holders)
        {
            // Who the holder is comes from their search; what they said, from this page.
            var who = WhoIs(h.Holder, investor, upload);
            // Their gender is the one on record; Investor Information asks it only when none is.
            var gender = GenderOnRecord(h.Holder, investor, upload);
            if (gender.Length == 0) gender = h.Gender;
            await InsertKycAsync(db, tx, at, h, who, gender, KycOf(h.Holder, upload), masters, minorUnder);

            // The permanent address is written even when blank: its row carries the
            // holder's mobile and e-mail.
            await InsertAddressAsync(db, tx, at, h, who.Folio, AddressType.Permanent, "Permanent", InThreeLines(who.Address, 100));
            if (h.Communication is { } communication)
                await InsertAddressAsync(db, tx, at, h, who.Folio, AddressType.Communication, "Mailing", communication);
        }

        if (details.Nominee is { } nominee)
            await InsertNomineeAsync(db, tx, at, nominee, minorUnder, MasterLists.CodeOf(masters.NomineeRelations, nominee.Relation));
    }

    // The names and codes the app knows go in the description columns; the FD
    // system's own codes beside them stay empty until the lists are read off its masters.
    private static Task InsertKycAsync(IDbConnection db, IDbTransaction tx, Stamp at, HolderDetails h, Holder who, string gender, Kyc kyc,
        MasterLists masters, int minorUnder)
    {
        // The occupation and sub occupation chosen are a row of the FD system's master:
        // its codes go beside the names. One it does not hold is kept by name alone.
        var occupation = masters.OccupationOf(h.Occupation, h.SubOccupation);
        var born = Dates.ParseDdMmYyyy(who.Dob);
        return db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_Kyc_Data_Dtl (f_Holder_Type, f_Appl_No, f_Kyc_ConstiType, f_Kyc_NamePrefix, f_Kyc_FirstName, f_Kyc_FullName,
                f_Kyc_FatherFirstName, f_Kyc_FatherFullName, f_Kyc_SpouseFirstName, f_Kyc_SpouseFullName,
                f_Kyc_MotherFirstName, f_Kyc_MotherFullName, f_Kyc_MaritalStatus, f_Kyc_Nationality_Code, f_Kyc_Nationality_Desc,
                f_Kyc_DOB, f_IsMinor, f_Kyc_PAN, f_Kyc_Number, f_FolioNo, f_Kyc_AnnualIncome_Desc, f_CustSeg_Type_desc, f_CustSeg_Subtype_Desc,
                f_CustSeg_Type_Code, f_CustSeg_Subtype_Code, f_Kyc_Occupation_Code, f_Kyc_Occupation_Desc,
                f_NSA_Response, f_NSA_Date, f_IsPEP, f_IsPEP_Relative, f_IsPanVerified, f_Data_Source,
                f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionID)
            VALUES (@HolderType, @AppNo, '01', @NamePrefix, @FirstName, @Name,
                @Father, @Father, @Spouse, @Spouse,
                @Mother, @Mother, @MaritalStatus, 'IN', 'India',
                @Dob, @Minor, @Pan, @CkycNumber, @Folio, @AnnualIncome, @Occupation, @SubOccupation,
                @TypeCode, @SubTypeCode, @OccupationCode, @OccupationName,
                @ScreeningStatus, @ScreenedOn, @Pep, @PepRelated, @PanVerified, @DataSource,
                @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            HolderType = h.Holder, at.AppNo, NamePrefix = NamePrefixes.Of(gender, h.MaritalStatus),
            // The whole name, as the table's own note asks, in both columns.
            FirstName = who.Name, who.Name,
            Father = ParentNamed("Father", h), Spouse = ParentNamed("Spouse", h), Mother = ParentNamed("Mother", h),
            MaritalStatus = MasterLists.CodeOf(masters.MaritalStatuses, h.MaritalStatus),
            Dob = born, Minor = IsMinor(born, DateTime.Today, minorUnder), who.Pan, CkycNumber = EmptyAsNull(kyc.CkycReference), who.Folio,
            h.AnnualIncome, h.Occupation, h.SubOccupation,
            occupation?.TypeCode, occupation?.SubTypeCode, occupation?.OccupationCode, occupation?.OccupationName,
            kyc.ScreeningStatus, kyc.ScreenedOn, Pep = YesOrNo(h.Pep), PepRelated = YesOrNo(h.PepRelated),
            PanVerified = kyc.Nsdl == "verified" ? "Y" : "N", DataSource = DataSourceOf(who, kyc),
            Source, at.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);
    }

    private static string? EmptyAsNull(string value)
    {
        if (value.Length == 0) return null;
        return value;
    }

    // f_Data_Source: where the holder's KYC came from. A holder on a folio: the
    // source their folio's record names. Otherwise CKYC once it is fetched for
    // them, and FRESH for one whose details are given here.
    private static string? DataSourceOf(Holder who, Kyc kyc)
    {
        if (who.Folio.Length > 0 && who.Source.Length > 0) return who.Source;
        if (who.Folio.Length > 0) return null;
        if (kyc.Ckyc) return "CKYC";
        return "FRESH";
    }

    private static Task InsertAddressAsync(IDbConnection db, IDbTransaction tx, Stamp at, HolderDetails h, string folio, string type, string typeName, TypedAddress a) =>
        db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_Address_Dtl (f_Holder_Type, f_Appl_No, f_AddType_Code, f_AddType_Desc, f_Add1, f_Add2, f_Add3,
                f_AddCity_Desc, f_AddDistrict_Desc, f_AddState_Desc, f_AddPin, f_MobileNumber, f_EmailAdd, f_FolioNo,
                f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionID)
            VALUES (@HolderType, @AppNo, @Type, @TypeName, @Line1, @Line2, @Line3,
                @City, @District, @State, @PinCode, @Mobile, @Email, @Folio,
                @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            HolderType = h.Holder, at.AppNo, Type = type, TypeName = typeName,
            Line1 = Cut(a.Line1, 100), Line2 = Cut(a.Line2, 100), Line3 = Cut(a.Line3, 100),
            City = Cut(a.City, 50), District = Cut(a.District, 50), State = Cut(a.State, 50), a.PinCode, h.Mobile, h.Email, Folio = folio,
            Source, at.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);

    // The address is the guardian's: a nominee has none of their own on Investor
    // Information. A guardian is asked for only when the date of birth makes the
    // nominee a minor; whatever was given of one is kept either way.
    private static Task InsertNomineeAsync(IDbConnection db, IDbTransaction tx, Stamp at, NomineeDetails n, int minorUnder, string relationCode)
    {
        var born = Dates.ParseDdMmYyyy(n.Dob);
        return db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_Nominee_Dtl (f_Appl_No, f_Nominee_Name, f_Nominee_Relations, f_Nominee_DOB, f_Is_Nominee_Minor,
                f_GuardianName, f_Address1, f_Address2, f_Address3, f_City, f_PIN, f_FolioNo,
                f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedOn, f_CreatedIP, f_SessionId)
            VALUES (@AppNo, @Name, @Relation, @Dob, @Minor,
                @GuardianName, @GuardianLine1, @GuardianLine2, @GuardianLine3, @GuardianCity, @GuardianPinCode, @Folio,
                @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            at.AppNo, n.Name, Relation = Cut(relationCode, 20), Dob = born, Minor = IsMinor(born, DateTime.Today, minorUnder),
            n.GuardianName, n.GuardianLine1, n.GuardianLine2, n.GuardianLine3, n.GuardianCity, n.GuardianPinCode, at.Folio,
            Source, at.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);
    }

    // f_IsMinor and f_Is_Nominee_Minor, from the date of birth: under the minimum age today.
    // Empty while there is no date of birth to go by.
    private static bool? IsMinor(DateTime? born, DateTime today, int minorUnder)
    {
        if (born is null) return null;
        return born.Value <= today && born.Value.AddYears(minorUnder) > today;
    }

    /// <summary>
    /// A holder's gender as their folio or their search gave it, or - for the
    /// investor - as read off an Aadhaar on Upload Documents; empty when neither has it.
    /// </summary>
    public static string GenderOnRecord(string code, Holder investor, UploadState? upload)
    {
        var who = WhoIs(code, investor, upload);
        if (who.Gender.Length > 0) return who.Gender;
        if (code == HolderType.Investor && upload is not null) return upload.Gender;
        return "";
    }

    // The name given goes under whose it is - the father's, the mother's or the spouse's.
    private static string? ParentNamed(string nameType, HolderDetails h)
    {
        if (h.NameType == nameType) return h.ParentName;
        return null;
    }

    // f_IsPEP and f_IsPEP_Relative: empty until the question is answered.
    private static bool? YesOrNo(string answer)
    {
        if (answer == "yes") return true;
        if (answer == "no") return false;
        return null;
    }

    // An address kept as one line, broken at its spaces into three of the column's width.
    private static TypedAddress InThreeLines(string address, int width)
    {
        var lines = new[] { "", "", "" };
        var line = 0;
        foreach (var word in address.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var withWord = lines[line].Length == 0 ? word : lines[line] + " " + word;
            if (withWord.Length > width && lines[line].Length > 0)
            {
                line++;
                if (line == lines.Length) break;
                withWord = word;
            }
            lines[line] = Cut(withWord, width);
        }
        return new TypedAddress(lines[0], lines[1], lines[2]);
    }

    // ----- Holders -------------------------------------------------------------

    /// <summary>Where a holder's KYC stands: the NSDL result, whether CKYC was fetched and its reference number, and what name screening said and when.</summary>
    private sealed record Kyc(string Nsdl, bool Ckyc, string ScreeningStatus, DateTime? ScreenedOn, string CkycReference = "");

    // Where a holder's KYC stands on Upload Documents. CKYC is fetched for the investor only.
    // Where a holder's KYC stands, off the upload step: the NSDL result and name, CKYC,
    // the mailing address, and what name screening said of them.
    private static Kyc KycOf(string code, UploadState? u)
    {
        if (u is null) return new Kyc("", false, "", null);

        var screeningStatus = "";
        DateTime? screenedOn = null;
        if (u.Screening.TryGetValue(code, out var screening))
        {
            screeningStatus = screening.Allowed ? "allowed" : "blocked";
            if (screening.Reference == NameScreeningResult.Skipped) screeningStatus = "skipped";
            screenedOn = screening.At;
        }

        if (code == HolderType.Investor) return new Kyc(u.Nsdl, u.Ckyc, screeningStatus, screenedOn, u.CkycReference);
        if (u.Joint.GetValueOrDefault(code) is { } j) return new Kyc(j.Nsdl, false, screeningStatus, screenedOn);
        return new Kyc("", false, screeningStatus, screenedOn);
    }

    // The investor takes the name NSDL verified, when they came with no folio.
    private static Holder WhoIs(string code, Holder investor, UploadState? upload) => code == HolderType.Investor
        ? (upload is { Name.Length: > 0 } ? investor with { Name = upload.Name } : investor)
        : upload?.Joint.GetValueOrDefault(code)?.Holder ?? new Holder("", "", "", "", false);
}
