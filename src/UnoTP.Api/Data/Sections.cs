using System.Data;
using System.Text.Json;
using Dapper;
using UnoTP.Backend;

namespace UnoTP.Api.Data;

/// <summary>Who wrote a set of rows, at which of the application's versions, and as what.</summary>
internal sealed record Stamp(string AppNo, int Version, string Status, string By);

/// <summary>
/// Writes each section of an application as rows. Nothing here updates or deletes:
/// every call inserts the section afresh at the stamp's version, so the rows it
/// replaces stay on record as the section's history.
/// </summary>
internal static class Sections
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ----- Upload Documents: t_Upload_State, t_Kyc_Documents ----------------

    public static async Task WriteUploadAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState upload)
    {
        await db.ExecuteAsync("""
            INSERT dbo.t_Upload_State (c_App_No, n_App_Version, c_Status, j_Upload, c_Created_By)
            VALUES (@AppNo, @Version, @Status, @Json, @By)
            """, new { at.AppNo, at.Version, at.Status, Json = JsonSerializer.Serialize(upload, Json), at.By }, tx);

        // One row per document on the application, under the holder it is filed with.
        foreach (var d in Documents(upload))
        {
            await db.ExecuteAsync("""
                INSERT dbo.t_Kyc_Documents (c_App_No, n_App_Version, c_Status, c_Holder_Type, c_Doc_Type, c_Doc_Sub_Type,
                    c_File_Name, c_File_Path, n_File_Size, c_Content_Type, c_Check, c_Result, f_On_Record, c_Created_By)
                VALUES (@AppNo, @Version, @Status, @HolderType, @DocType, @SubType,
                    @FileName, @FilePath, @Size, @ContentType, @Check, @Result, @OnRecord, @By)
                """, new
            {
                at.AppNo, at.Version, at.Status, d.HolderType, d.DocType, d.SubType,
                d.Doc.FileName, FilePath = d.Doc.Before ? null : Dms.DmsPaths.Of(at.AppNo, d.HolderType, d.DocType, d.Doc.FileName),
                d.Doc.Size, d.Doc.ContentType, d.Doc.Check, Result = d.Doc.CheckKind, OnRecord = d.Doc.Before, at.By,
            }, tx);
        }
    }

    // ----- Investor Information: t_Kyc_Dtls, t_Address_Dtls, t_Nominee_Dtls ---

    public static async Task WriteDetailsAsync(IDbConnection db, IDbTransaction tx, Stamp at, Holder investor, UploadState? upload, ApplicationDetails details)
    {
        foreach (var h in details.Holders)
        {
            // Who the holder is comes from their search; what they said, from this page.
            var who = WhoIs(h.Holder, investor, upload);
            var kyc = KycOf(h.Holder, upload);
            await db.ExecuteAsync("""
                INSERT dbo.t_Kyc_Dtls (c_App_No, n_App_Version, c_Status, c_Holder_Type, c_Pan, d_Dob, c_Name, c_Folio,
                    c_Gender, c_Name_Type, c_Parent_Name, c_Annual_Income, c_Occupation, c_Sub_Occupation, c_Marital_Status,
                    c_Mobile, c_Email, f_Fatca_Tax_Res, f_Fatca_Perm_Res, c_Pep, c_Pep_Related,
                    c_Nsdl_Status, c_Nsdl_Name, f_Ckyc, f_Mail_Different, c_Created_By)
                VALUES (@AppNo, @Version, @Status, @HolderType, @Pan, @Dob, @Name, @Folio,
                    @Gender, @NameType, @ParentName, @AnnualIncome, @Occupation, @SubOccupation, @MaritalStatus,
                    @Mobile, @Email, @FatcaTaxResident, @FatcaPermanentResident, @Pep, @PepRelated,
                    @Nsdl, @NsdlName, @Ckyc, @MailDifferent, @By)
                """, new
            {
                at.AppNo, at.Version, at.Status, HolderType = h.Holder, who.Pan, Dob = Dates.ToDb(who.Dob), who.Name, who.Folio,
                h.Gender, h.NameType, h.ParentName, h.AnnualIncome, h.Occupation, h.SubOccupation, h.MaritalStatus,
                h.Mobile, h.Email, h.FatcaTaxResident, h.FatcaPermanentResident, h.Pep, h.PepRelated,
                kyc.Nsdl, kyc.NsdlName, kyc.Ckyc, kyc.MailDifferent, at.By,
            }, tx);

            if (who.Address.Length > 0)
                await InsertAddressAsync(db, tx, at, h.Holder, "PER", new TypedAddress(Line1: who.Address));
            if (h.Communication is { } communication)
                await InsertAddressAsync(db, tx, at, h.Holder, "COR", communication);
        }

        if (details.Nominee is { } n)
        {
            await db.ExecuteAsync("""
                INSERT dbo.t_Nominee_Dtls (c_App_No, n_App_Version, c_Status, c_Name, d_Dob, c_Relation, c_Guardian_Name,
                    c_Guardian_Line1, c_Guardian_Line2, c_Guardian_Line3, c_Guardian_Pin_Code, c_Guardian_City, c_Created_By)
                VALUES (@AppNo, @Version, @Status, @Name, @Dob, @Relation, @GuardianName,
                    @GuardianLine1, @GuardianLine2, @GuardianLine3, @GuardianPinCode, @GuardianCity, @By)
                """, new
            {
                at.AppNo, at.Version, at.Status, n.Name, Dob = Dates.ToDb(n.Dob), n.Relation, n.GuardianName,
                n.GuardianLine1, n.GuardianLine2, n.GuardianLine3, n.GuardianPinCode, n.GuardianCity, at.By,
            }, tx);
        }
    }

    private static Task InsertAddressAsync(IDbConnection db, IDbTransaction tx, Stamp at, string holder, string type, TypedAddress a) =>
        db.ExecuteAsync("""
            INSERT dbo.t_Address_Dtls (c_App_No, n_App_Version, c_Status, c_Holder_Type, c_Addr_Type,
                c_Line1, c_Line2, c_Line3, c_City, c_Pin_Code, c_District, c_State, c_Created_By)
            VALUES (@AppNo, @Version, @Status, @HolderType, @AddrType,
                @Line1, @Line2, @Line3, @City, @PinCode, @District, @State, @By)
            """, new
        {
            at.AppNo, at.Version, at.Status, HolderType = holder, AddrType = type,
            a.Line1, a.Line2, a.Line3, a.City, a.PinCode, a.District, a.State, at.By,
        }, tx);

    // ----- Bank Details & Payment: t_Payment_Bank_Dtls, t_Bank_Dtls ----------

    /// <param name="branches">The branch each IFSC names, as looked up before the save.</param>
    public static async Task WritePaymentAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState? upload,
        PaymentDetails payment, IReadOnlyDictionary<string, BankBranch> branches)
    {
        var pay = Branch(payment.Payment, branches);
        await db.ExecuteAsync("""
            INSERT dbo.t_Payment_Bank_Dtls (c_App_No, n_App_Version, c_Status, c_Pay_Mode, c_Ifsc, c_Account_No,
                c_Bank_Name, c_Branch_Name, c_Micr, c_Cheque_No, d_Cheque_Date, c_Cms_Location, c_Created_By)
            VALUES (@AppNo, @Version, @Status, @PayMode, @Ifsc, @AccountNo,
                @Bank, @BranchName, @Micr, @ChequeNo, @ChequeDate, @CmsLocation, @By)
            """, new
        {
            at.AppNo, at.Version, at.Status, PayMode = upload?.PayMode ?? "",
            payment.Payment?.Ifsc, AccountNo = payment.Payment?.AccountNumber, pay?.Bank, BranchName = pay?.Branch, pay?.Micr,
            ChequeNo = payment.Cheque?.Number, ChequeDate = Dates.ToDb(payment.Cheque?.Date), payment.Cheque?.CmsLocation, at.By,
        }, tx);

        var repay = Branch(payment.Repayment, branches);
        await db.ExecuteAsync("""
            INSERT dbo.t_Bank_Dtls (c_App_No, n_App_Version, c_Status, f_Same_As_Payment, c_Ifsc, c_Account_No,
                c_Bank_Name, c_Branch_Name, c_Micr, c_Created_By)
            VALUES (@AppNo, @Version, @Status, @SameAsPayment, @Ifsc, @AccountNo, @Bank, @BranchName, @Micr, @By)
            """, new
        {
            at.AppNo, at.Version, at.Status, SameAsPayment = payment.RepaymentSameAsPayment,
            payment.Repayment?.Ifsc, AccountNo = payment.Repayment?.AccountNumber, repay?.Bank, BranchName = repay?.Branch, repay?.Micr, at.By,
        }, tx);
    }

    private static BankBranch? Branch(BankAccount? account, IReadOnlyDictionary<string, BankBranch> branches) =>
        account is null ? null : branches.GetValueOrDefault(account.Ifsc.Trim().ToUpperInvariant());

    // ----- FD Configuration: t_Investment_Dtls --------------------------------

    /// <param name="quote">The quote locked on submit; null on a step's save.</param>
    public static Task WriteDepositAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState? upload,
        DepositDetails deposit, string? renews, DepositQuote? quote)
    {
        var u = upload ?? new UploadState();
        return db.ExecuteAsync("""
            INSERT dbo.t_Investment_Dtls (c_App_No, n_App_Version, c_Status, n_Amount, n_Tenure_Months, c_Payout,
                f_Auto_Renewal, c_Renew_Instruction, f_No_Tds, c_Delivery_Type,
                c_App_Type, c_Form_No, c_Category, c_Sourcing, c_Source_Code, c_Sub_Broker,
                c_Emp_Code, c_Emp_Company, c_Emp_Holder, c_Emp_Relation, c_Emp_Proof_Type, c_Renew_Dep_No,
                n_Rate, n_Interest_Each, n_Maturity_Amount, d_Matures_On, d_Rate_As_On, c_Created_By)
            VALUES (@AppNo, @Version, @Status, @Amount, @TenureMonths, @Payout,
                @AutoRenewal, @RenewInstruction, @NoTds, @DeliveryType,
                @AppType, @FormNo, @Category, @Sourcing, @SourceCode, @SubBroker,
                @EmpCode, @EmpCompany, @EmpHolder, @EmpRelation, @EmpProofType, @Renews,
                @Rate, @InterestEach, @MaturityAmount, @MaturesOn, @RateAsOn, @By)
            """, new
        {
            at.AppNo, at.Version, at.Status, deposit.Amount, deposit.TenureMonths, deposit.Payout,
            deposit.AutoRenewal, deposit.RenewInstruction, deposit.NoTds, deposit.DeliveryType,
            u.AppType, u.FormNo, u.Category, u.Sourcing, u.SourceCode, u.SubBroker,
            u.EmpCode, u.EmpCompany, u.EmpHolder, u.EmpRelation, u.EmpProofType, Renews = renews,
            quote?.Rate, quote?.InterestEach, quote?.MaturityAmount,
            MaturesOn = quote?.MaturesOn.ToDateTime(TimeOnly.MinValue), RateAsOn = quote?.RateAsOn.ToDateTime(TimeOnly.MinValue), at.By,
        }, tx);
    }

    // ----- Holders -------------------------------------------------------------

    private sealed record Kyc(string Nsdl, string NsdlName, bool Ckyc, bool MailDifferent);

    // Where a holder's KYC stands on Upload Documents. CKYC is fetched for the investor only.
    private static Kyc KycOf(string code, UploadState? u) =>
        u is null ? new Kyc("", "", false, false)
        : code == HolderType.Investor ? new Kyc(u.Nsdl, u.NsdlName, u.Ckyc, u.MailDifferent)
        : u.Joint.GetValueOrDefault(code) is { } j ? new Kyc(j.Nsdl, j.NsdlName, false, j.MailDifferent)
        : new Kyc("", "", false, false);

    private sealed record Document(string HolderType, string DocType, string SubType, StoredDoc Doc);

    // A document's key says whose and which it is: the investor's "pan", "photo",
    // "poa", "mail"; a joint holder's "h02-pan"; anything else is the application's
    // own ("form", "payment", "empproof", "tdsform").
    private static IEnumerable<Document> Documents(UploadState u)
    {
        foreach (var (key, doc) in u.Docs.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            var (holder, type) = key.Length > 4 && key[0] == 'h' && key[3] == '-'
                ? (key[1..3], key[4..])
                : (key is "pan" or "photo" or "poa" or "mail" ? HolderType.Investor : HolderType.None, key);
            var joint = u.Joint.GetValueOrDefault(holder);
            var sub = type switch
            {
                "poa" => holder == HolderType.Investor ? u.PoaType : joint?.PoaType ?? "",
                "mail" => holder == HolderType.Investor ? u.MailPoaType : joint?.MailPoaType ?? "",
                "payment" => u.PayMode,
                "empproof" => u.EmpProofType,
                "form" => u.AppType,
                _ => "",
            };
            yield return new Document(holder, type, sub, doc);
        }
    }

    // The investor takes the name NSDL verified, when they came with no folio.
    private static Holder WhoIs(string code, Holder investor, UploadState? upload) => code == HolderType.Investor
        ? (upload is { Name.Length: > 0 } ? investor with { Name = upload.Name } : investor)
        : upload?.Joint.GetValueOrDefault(code)?.Holder ?? new Holder("", "", "", "", false);

}
