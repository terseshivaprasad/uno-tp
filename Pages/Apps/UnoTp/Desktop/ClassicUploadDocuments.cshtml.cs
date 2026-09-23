using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

/// <summary>
/// The old WA_FD_UNOTP/UploadInvestorDocuments: the documents an application
/// carries, and the details it is sourced under. Step two of the classic wizard.
/// </summary>
public class ClassicUploadDocumentsModel : PageModel
{
    // Who the step is for. The identification step hands these over, and the page
    // falls back to the first mock folio when it is opened on its own.
    [BindProperty(SupportsGet = true)]
    public string? Pan { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Dob { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? App { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Folio { get; set; }

    /// <summary>Set when the step before already put a PAN copy on the
    /// application, either uploaded there or held against the folio.</summary>
    [BindProperty(SupportsGet = true)]
    public bool PanFiled { get; set; }

    public string HolderName =>
        string.IsNullOrWhiteSpace(Name) ? ClassicSearchInvestorModel.Folios[0].Name : Name;

    // A PAN established at the step before has no folio yet: one opens with the
    // application, and the head of the page says so rather than leaving a blank.
    public bool HasFolio => !string.IsNullOrWhiteSpace(Folio);

    public string HolderFolio => HasFolio ? Folio! : "Opens with this application";

    public string HolderPan => Mask(Pan ?? ClassicSearchInvestorModel.Folios[0].Pan);

    public string HolderDob => MaskDate(Dob ?? ClassicSearchInvestorModel.DemoDob);

    public string AppNo => string.IsNullOrWhiteSpace(App) ? "FBBMFL26F1CA025" : App;

    // The same masking the register uses: a PAN keeps its first five and last
    // character, a date of birth only its year.
    private static string Mask(string pan) =>
        pan.Length == 10 ? pan[..5] + "••••" + pan[9..] : pan;

    private static string MaskDate(string dob) =>
        dob.Length == 10 ? "••/••/" + dob[6..] : dob;

    /// <summary>What a drop zone takes, and what it says it takes.</summary>
    public record Accepts(string Mime, string Label, int MaxMb);

    public static readonly Accepts Form = new("application/pdf,image/jpeg", "PDF/JPG/JPEG", 4);
    public static readonly Accepts Image = new("image/jpeg", "JPG/JPEG", 2);

    // An application made on paper carries a signed form; a digital one is
    // accepted through the investor's own link instead, so that slot is not used.
    public static readonly string[] ApplicationTypes = { "DIGITAL", "PHYSICAL" };

    public static readonly string[] ProofsOfAddress =
    {
        "Aadhaar", "Passport", "Driving Licence", "Voter ID", "Utility bill",
    };

    // Only the modes settled by an instrument carry a document; the rest are
    // settled electronically and have nothing to file.
    public record PaymentMode(string Name, string? Document);

    public static readonly PaymentMode[] PaymentModes =
    {
        new("Cheque", "cheque"),
        new("Demand Draft", "demand draft"),
        new("NEFT / RTGS", null),
        new("UPI", null),
        new("Net banking", null),
    };

    // Who the application is sourced through. MFL-EX is sourced by the partner
    // themselves, so the code beside it is their own and is not typed; a broker
    // application carries the broker's code instead.
    public const string EmployeeSourcing = "MFL-EX";

    public static readonly string[] SourcingModes = { EmployeeSourcing, "BROKER", "DIRECT" };

    // The partner in the top bar, whose code fills the sourcing field.
    public const string PartnerCode = "100002225";

    public const string PartnerName = "Shivaprasad Terse";

    // An employee deposit is booked against a staff record, which is what the
    // block of employee fields is for.
    public const string EmployeeCategory = "EMPLOYEE";

    public static readonly string[] DepositCategories =
        { "INDIVIDUAL", "SENIOR CITIZEN", EmployeeCategory, "TRUST" };

    public static readonly string[] EmployeeHolders = { "First holder", "Second holder", "Third holder" };

    public static readonly string[] EmployeeRelations = { "Self", "Spouse", "Parent", "Child", "Sibling" };

    public static readonly string[] EmployeeProofs =
        { "Employee ID card", "Appointment letter", "Latest salary slip" };

    public static readonly Accepts Proof = new("application/pdf,image/jpeg", "PDF/JPG/JPEG", 2);

    // The staff the mock can put a name to. Anything else stays unresolved, the
    // way the old screen leaves the line with nothing after its dash.
    public static readonly Dictionary<string, string> Employees = new()
    {
        ["E10428"] = "Nikhil Ramesh Bhosale",
        ["E20915"] = "Sneha Arun Kulkarni",
        ["E31077"] = "Farhan Iqbal Shaikh",
    };

    // ----- The address the application carries ---------------------------------
    // A proof of address is not filed on its own: OCR reads the address off it and
    // the issuer is asked whether that is the address they hold. Only an answer
    // that comes back clean replaces what the application already carries, which
    // is why the step shows both addresses where the pickers can change them.

    /// <summary>The address the folio was opened with.</summary>
    public string PermanentAddress =>
        ClassicSearchInvestorModel.Folios[0].Address;

    /// <summary>Who stands behind each proof. A bill has no register to ask, so
    /// it is Operations who settle it and the address is left as it stands.</summary>
    public static readonly Dictionary<string, string> Issuers = new()
    {
        ["Aadhaar"] = "UIDAI",
        ["Passport"] = "Passport Seva",
        ["Driving Licence"] = "Sarathi",
        ["Voter ID"] = "the Election Commission",
        ["Utility bill"] = "",
    };

    /// <summary>What the mock OCR reads off a proof: a corrected permanent
    /// address, and the separate address a mailing proof carries.</summary>
    public const string ReadPermanentAddress =
        "Flat 12B, Shantiniketan CHS, Baner Road, Balewadi, Pune, Maharashtra 411045";

    public const string ReadMailingAddress =
        "704 Silverstone Apartments, Kalyani Nagar, Pune, Maharashtra 411006";

    /// <summary>What the mock OCR reads off a cheque or a draft, and the bank
    /// asked to stand behind it. The account is masked the way the register masks
    /// it, since nothing here is a real one.</summary>
    public const string ReadInstrument =
        "A/c \u2022\u2022\u2022\u2022\u2022\u20227742 \u00b7 IFSC HDFC0000123 \u00b7 HDFC Bank, Baner, Pune";

    public const string InstrumentBank = "HDFC Bank";

    /// <summary>The same, as the script reads them.</summary>
    public string AddressJson => System.Text.Json.JsonSerializer.Serialize(
        new
        {
            issuers = Issuers,
            bank = InstrumentBank,
            read = new
            {
                poa = ReadPermanentAddress,
                mailing = ReadMailingAddress,
                payment = ReadInstrument,
            },
        },
        new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });

    // ----- What this application has already been through ----------------------
    // The attempts made before this page was opened: the PAN that came back as
    // someone else's, the copy that stood, and a bill with no register behind it.
    // They are what the history card opens with, and the count they leave is what
    // the pickers carry on from, so the three are three against the application
    // rather than three against this visit to the page.
    public record PastStage(string Text, string Kind);

    public record PastAttempt(
        string When, string Document, string Slot, int Attempt, string File,
        string Mark, string Kind, PastStage[] Stages);

    public PastAttempt[] PastAttempts
    {
        get
        {
            var past = new List<PastAttempt>
            {
                new("10:42:05", "PAN copy", "pan", 1, "scan_0417.jpg \u00b7 386 KB", "Refused", "bad", new[]
                {
                    new PastStage("Not identified as a PAN card.", "bad"),
                }),
            };

            if (PanFiled)
            {
                past.Add(new PastAttempt("10:43:18", "PAN copy", "pan", 2, "PAN_front.jpg \u00b7 412 KB", "Filed", "ok", new[]
                {
                    new PastStage("Identified as a PAN card.", "ok"),
                    new PastStage("OCR read: PAN XXXXA1001A \u00b7 SHIVAPRASAD SUBHASH TERSE", ""),
                    new PastStage("NSDL confirmed the PAN, the name and the date of birth.", "ok"),
                }));
            }

            past.Add(new PastAttempt("10:47:51", "POA", "poa", 1, "electricity_bill_aug.pdf \u00b7 1.2 MB", "Filed, address unchanged", "warn", new[]
            {
                new PastStage("Identified as a proof of address.", "ok"),
                new PastStage("OCR read: " + ReadPermanentAddress, ""),
                new PastStage("A utility bill has no issuer to check with.", "warn"),
            }));

            // Newest first, as the card reads.
            past.Reverse();
            return past.ToArray();
        }
    }

    /// <summary>How many of the three each document has already used.</summary>
    public string AttemptsJson
    {
        get
        {
            var used = new Dictionary<string, int>();
            foreach (var attempt in PastAttempts)
            {
                used[attempt.Slot] = Math.Max(
                    used.TryGetValue(attempt.Slot, out var n) ? n : 0, attempt.Attempt);
            }
            return System.Text.Json.JsonSerializer.Serialize(used);
        }
    }

    public void OnGet()
    {
    }
}
