namespace UnoTp.Models;

public record RequiredDocGroup(string Title, string[] Items);

/// <summary>
/// The old site's FdDocModal, word for word: what each kind of investor has to
/// hand over to book an FD. Both classic pages that offer "Click here to see the
/// list of required documents" - the console and the Uno TP dashboard - show this
/// one list.
/// </summary>
public static class RequiredDocs
{
    public static readonly RequiredDocGroup[] Groups =
    {
        new("Individual KYC", new[]
        {
            "Passport",
            "Driving license",
            "Permanent Account Number (PAN) card",
            "‘Election Commission of India’ Voter identity card",
            "‘NREGA’ Job card, duly signed by an officer of the State Government",
            "‘Unique Identification Authority of India’ letter containing details of name, address and Aadhaar",
            "Number or any document as notified by the Central Government in consultation with the regulator",
            "Note: eSarathi only supports Individual Deposits.",
        }),
        new("Sole Proprietorship", new[]
        {
            "ID & address proof of the proprietor with self attestation",
            "PAN card of proprietor with self attestation",
            "Proprietor Address proof with self attestation",
            "Photograph",
            "If the sole proprietorship is in a different name, the bank statement or registration certificate",
            "GST & Udyam Certificate or Trade License required",
            "Cancelled cheque leaf for bank account verification",
        }),
        new("NRI - Individual KYC", new[]
        {
            "Passport with valid visa/ OCI",
            "Overseas employment letter (optional for confirmation of residential status and overseas address)",
            "PIO cards",
            "PAN card",
            "Local proof of address, if different from the passport address",
            "Bank account statement or passbook",
            "Local Property papers with registration deed",
            "EB Bill card",
            "Voter ID or driving license",
            "Tax Residency Certificate from the Income Tax department of the country of which the investor is a resident",
            "Copy of the passport as of the beginning of the current financial year and end of the financial year",
        }),
        new("HUF Deposits", new[]
        {
            "HUF Pan copy with self attestation",
            "Latest HUF address proof required",
            "Valid address proof & identity proof of Karta",
            "Karta Photograph",
            "Cancelled cheque leaf for bank account verification",
            "HUF declaration required",
        }),
        new("Companies", new[]
        {
            "PAN card",
            "Address proof (Valid GST Certificate/bank statement/telephone bill)",
            "Certificate of incorporation",
            "Memorandum of article & association with latest board resolution and specimen signatures",
            "Authorised signatory list",
            "Photograph of the signatories",
            "ID & address proof of authorised signatories",
            "Cancelled cheque leaf for bank account verification",
            "FATCA Declaration",
            "Beneficial Owner form along with BO Owners self attested KYC documents",
        }),
        new("Partnership Firms", new[]
        {
            "PAN card",
            "Address proof (bank statement, telephone bill)",
            "Firm Registration certificate",
            "Partnership deed",
            "Resolution copy",
            "ID & address proof of all authorised signatories",
            "Photograph of the signatories",
            "Cancelled cheque leaf for bank account verification",
            "FATCA Declaration",
            "Beneficial Owner form along with BO Owners self attested KYC documents",
        }),
        new("Trust and Foundations", TrustItems(npo: true)),
        new("Charitable Trust", TrustItems(npo: true)),
        new("Family Trust", TrustItems(npo: false)),
        new("Club, Association, Society", new[]
        {
            "Copy of the Registration Certificate, if registered.",
            "Acknowledgment of registration application, if applied for.",
            "PAN card copy",
            "Address proof",
            "List of signatories",
            "MOA & Resolution",
            "Individual KYC of all signatories",
            "Photograph of the Trustees & signatories",
            "Cancelled cheque leaf for bank account verification",
            "FATCA Declaration",
            "Beneficial Owner form along with BO Owners self attested KYC documents",
        }),
    };

    // The three trust lists are one list; only a family trust skips the two NPO lines.
    private static string[] TrustItems(bool npo)
    {
        var items = new List<string>
        {
            "PAN card",
            "Address proof (Valid GST Certificate/bank statement/telephone bill)",
        };
        if (npo)
        {
            items.Add("Non Profit Organisation(Yes/No)");
            items.Add("Darpan Registration mandatory for NPO's");
        }
        items.AddRange(new[]
        {
            "Registration certificate of the Trust/Charitable/Family & Foundation",
            "Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures",
            "Authorised signatory list",
            "ID & address proof of authorised signatories with self attestation",
            "Photograph of the Trustees & signatories",
            "Cancelled cheque leaf for bank account verification",
            "FATCA Declaration",
            "Beneficial Owner form along with BO Owners self attested KYC documents",
        });
        return items.ToArray();
    }
}
