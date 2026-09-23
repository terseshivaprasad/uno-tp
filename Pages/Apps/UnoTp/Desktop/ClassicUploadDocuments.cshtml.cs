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

    /// <summary>
    /// The number the application is filed under. Investor Identification mints a
    /// new one every time a search is proceeded from - search the same PAN twice
    /// and there are two applications - and passes it here. Reached without one,
    /// the number is made from the PAN rather than being the same for everybody,
    /// because everything keyed to an application - the draft, the attempts, the
    /// history - would otherwise be shared by every investor opened directly.
    /// </summary>
    public string AppNo => string.IsNullOrWhiteSpace(App)
        ? "FBBMFL26F" + (Pan ?? ClassicSearchInvestorModel.Folios[0].Pan)[^5..]
        : App;

    // The same masking the register uses: a PAN keeps its first five and last
    // character, a date of birth only its year.
    private static string Mask(string pan) =>
        pan.Length == 10 ? pan[..5] + "••••" + pan[9..] : pan;

    private static string MaskDate(string dob) =>
        dob.Length == 10 ? "••-••-" + dob[6..] : dob;

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
        new("Online", null),
        new("RTGS", null),
        new("Cheque", "cheque"),
    };

    // The partner in the top bar, whose code fills the sourcing field.
    public const string PartnerCode = "100002225";

    public const string PartnerName = "Shivaprasad Terse";

    // An employee deposit is booked against a staff record, which is what the
    // block of employee fields is for.
    public const string EmployeeCategory = "EMPLOYEE";

    // ----- How the application is sourced --------------------------------------
    // One answer the rest of Additional Details hangs off: what the two code
    // fields are called, which of them is typed and which the mode fills itself,
    // whose register the typed one is searched against, and what the deposit may
    // be booked as. The old screen keys the modes by number and posts them that
    // way, so they keep their numbers here.

    /// <summary>What the second code field does under a mode.</summary>
    public static class SubField
    {
        /// <summary>Shut and empty: the mode has no sub-broker.</summary>
        public const string Shut = "shut";

        /// <summary>Shut, carrying the same house code as the field above it.</summary>
        public const string House = "house";

        /// <summary>There if there is one, empty if there is not.</summary>
        public const string Free = "free";

        /// <summary>The partner's own code, and theirs to change.</summary>
        public const string Employee = "employee";

        /// <summary>The partner's own code, and not theirs to change.</summary>
        public const string EmployeeShut = "employeeShut";
    }

    /// <summary>Which register a mode searches its typed code against.</summary>
    public static class Register
    {
        public const string None = "";
        public const string Brokers = "brokers";
        public const string Employees = "employees";
    }

    /// <summary>
    /// A way an application is sourced. <c>House</c> is the code the mode stands
    /// in the first field itself, where it does; <c>Search</c> names the field
    /// that is searched - "source" for the first, "sub" for the second - and
    /// <c>Register</c> what it is searched against.
    /// </summary>
    public record SourcingMode(
        string Code, string Name, string CodeLabel, string NameLabel,
        string House, string Search, string Register, string Sub, string[] Categories);

    private static readonly string[] RetailAndTrust = { "INDIVIDUAL", "SENIOR CITIZEN", "TRUST" };

    /// <summary>
    /// The four modes the old screen offers a partner, in its own order. The
    /// screen posts them by number and hangs everything off that number: BROKER
    /// is the one where the code is typed, and each of the other three stands its
    /// own house code in the field and asks for nothing there.
    /// </summary>
    public static readonly SourcingMode[] SourcingModes =
    {
        new("2", "BROKER", "Broker Code", "Broker Name",
            "", "source", Register.Brokers, SubField.Free, RetailAndTrust),
        new("1", "MMFSS - BRANCH", "Sourcing Employee Code", "Sourcing Employee Name",
            "MFL", "", Register.None, SubField.House, RetailAndTrust),
        new("5", "MFL-EX", "Sourcing Employee Code", "Sourcing Employee Name",
            "MFL-EX", "sub", Register.Employees, SubField.Employee, new[] { EmployeeCategory }),
        new("6", "MFIS/FD", "Sourcing Employee Code", "Sourcing Employee Name",
            "MIBS", "sub", Register.Employees, SubField.EmployeeShut, RetailAndTrust),
    };

    /// <summary>A code and the name the register holds against it.</summary>
    public record Party(string Code, string Name);

    /// <summary>The brokers a broker-sourced application can be filed under. The
    /// old screen asks the server for these three characters at a time; the mock
    /// searches the same way against what is here.</summary>
    public static readonly Party[] Brokers =
    {
        new("BR10021", "Sahyadri Investment Services"),
        new("BR10874", "Deccan Wealth Advisors"),
        new("BR11250", "Konkan Financial Services"),
        new("BR11903", "Nagpur Capital Partners"),
        new("BR12388", "Godavari Securities"),
    };

    /// <summary>The staff a sub-broker code is searched against, the partner at
    /// the keyboard among them: an employee-sourced application opens with their
    /// own code in the field.</summary>
    public static Party[] Staff =>
        new[] { new Party(PartnerCode, PartnerName) }
            .Concat(Employees.Select(e => new Party(e.Key, e.Value)))
            .ToArray();

    /// <summary>The modes and the two registers, as the script reads them.</summary>
    public string SourcingJson => System.Text.Json.JsonSerializer.Serialize(
        new
        {
            partner = new Party(PartnerCode, PartnerName),
            modes = SourcingModes,
            registers = new { brokers = Brokers, employees = Staff },
        },
        new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });

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

    /// <summary>
    /// A mailing address that differs from the permanent one is not in this
    /// release: the question, the proof it asks for and the card it fills are all
    /// off the page. Set this to true to put them back in front of partners
    /// again - the step, the script and the checks behind them are whole.
    /// </summary>
    public const bool MailingAddress = false;

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

    /// <summary>What the register already holds against the folio the application
    /// was opened on. A document on the folio is not asked for again: the step
    /// shows it as not applicable and says which folio carries it.</summary>
    public ClassicSearchInvestorModel.MockDocs? FolioDocs =>
        HasFolio
            ? ClassicSearchInvestorModel.Folios.FirstOrDefault(f => f.Folio == Folio)?.Docs
            : null;

    /// <summary>The same, as the script reads it.</summary>
    public string HeldJson => System.Text.Json.JsonSerializer.Serialize(
        new
        {
            folio = HasFolio ? Folio : "",
            pan = FolioDocs?.Pan ?? false,
            poa = FolioDocs?.Poa ?? false,
            photo = FolioDocs?.Photo ?? false,
        });

    /// <summary>Who answers for a PAN, and what the mock reads off the copy.</summary>
    public const string PanAuthority = "the Income Tax Department";

    /// <summary>The same, as the script reads them.</summary>
    public string AddressJson => System.Text.Json.JsonSerializer.Serialize(
        new
        {
            issuers = Issuers,
            bank = InstrumentBank,
            panAuthority = PanAuthority,
            read = new
            {
                pan = HolderPan,
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
    // They are what the history card opens with. They set no count: the three a
    // document is allowed are three in this session, so an earlier session is on
    // the record without spending anything the partner has now.
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
                    new PastStage("Copy kept for analysis as REJ-884199 until "
                        + DateTime.Today.AddDays(7).ToString("dd MMM yyyy") + ", and deleted after.", "warn"),
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
                new PastStage("A utility bill has no register behind it to put that address to.", "warn"),
            }));

            // Newest first, as the card reads.
            past.Reverse();
            return past.ToArray();
        }
    }

    public void OnGet()
    {
    }
}
