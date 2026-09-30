namespace UnoTP.Backend.Mock;

/// <summary>
/// The lists the pages offer and the rules they keep, as the backend would send
/// them. Everything here stood in the app's own pages before it moved behind the
/// backend; the wording is the old site's.
/// </summary>
public sealed class MockReference : IReferenceApi
{
    public const string SourcingAgency = "1033";

    /// <summary>How long a payment link stays open.</summary>
    public const int PaymentLinkHours = 48;

    private static readonly string[] Public = ["PUBLIC/GENERAL", "WOMEN", "SR CITIZEN", "SR CITIZEN WOMEN"];

    private static readonly ReferenceData Data = new(
        ApplicationTypes: [new("DIGITAL", "Digital"), new("PHYSICAL", "Physical")],
        Categories:
        [
            // ExtraRate is the chart's "additional rates": senior citizens and employees 0.35, women 0.05.
            new("PUBLIC/GENERAL", "Public / General", Employee: false, Women: false, Senior: false),
            new("WOMEN", "Women", Employee: false, Women: true, Senior: false, ExtraRate: 0.05m),
            new("SR CITIZEN", "Senior citizen", Employee: false, Women: false, Senior: true, ExtraRate: 0.35m),
            new("SR CITIZEN WOMEN", "Senior citizen women", Employee: false, Women: true, Senior: true, ExtraRate: 0.40m),
            new("EMPLOYEE", "Employee", Employee: true, Women: false, Senior: false, ExtraRate: 0.35m),
            new("EMPLOYEE WOMEN", "Employee women", Employee: true, Women: true, Senior: false, ExtraRate: 0.40m),
        ],
        PaymentModes: [new("Online", null), new("RTGS", null), new("Cheque", "cheque")],
        // The four modes the old screen offers, in its own order and under its own numbers.
        SourcingModes:
        [
            new("2", "BROKER", "Broker Code", "Broker Name", "", "source", "brokers", "free", Public),
            new("1", "MMFSS - BRANCH", "Sourcing Employee Code", "Sourcing Employee Name", "MFL", "", "", "house", Public),
            new("5", "MFL-EX", "Sourcing Employee Code", "Sourcing Employee Name", "MFL-EX", "sub", "employees", "employee", ["EMPLOYEE", "EMPLOYEE WOMEN"]),
            new("6", "MFIS/FD", "Sourcing Employee Code", "Sourcing Employee Name", "MIBS", "sub", "employees", "employeeShut", Public),
        ],
        ProofsOfAddress:
        [
            new("Aadhaar", "UIDAI", HasPhoto: true),
            new("Passport", "Passport Seva", HasPhoto: true),
            new("Driving Licence", "Sarathi", HasPhoto: true),
            new("Voter ID", "the Election Commission", HasPhoto: true),
            new("Utility bill", "", HasPhoto: false),
        ],
        EmployeeHolders: ["First holder", "Second holder", "Third holder"],
        EmployeeRelations: ["Self", "Spouse", "Parent", "Child", "Sibling"],
        EmployeeProofs: ["Employee ID card", "Appointment letter", "Latest salary slip"],
        IncomeBands: ["Upto Rs.5,00,000", "Rs.5,00,000 - Rs.10,00,000", "Rs.10,00,000 - Rs.25,00,000", "Above Rs.25,00,000"],
        Occupations: ["Salaried", "Self-employed", "Business", "Retired", "Homemaker", "Student"],
        SubOccupations: ["MMFSL Employee", "Private sector", "Public sector", "Government service", "Professional"],
        MaritalStatuses: ["Married", "Single", "Widowed", "Divorced"],
        Genders: ["Male", "Female", "Transgender"],
        NameTypes: ["Father", "Mother", "Spouse"],
        NomineeRelations: ["Son", "Daughter", "Spouse", "Father", "Mother", "Brother", "Sister"],
        Tenures: [12, 18, 24, 30, 36, 42, 48, 60],
        Payouts:
        [
            new("maturity", "On maturity", 0, ""),
            new("yearly", "Yearly", 1, "year"),
            new("halfyearly", "Half yearly", 2, "half year"),
            new("quarterly", "Quarterly", 4, "quarter"),
            new("monthly", "Monthly", 12, "month"),
        ],
        RenewInstructions: [new("principal", "Principal only"), new("principal-interest", "Principal and interest")],
        DeliveryTypes: [new("ereceipt", "E-receipt"), new("physical", "Physical")],
        CmsLocations: MockBranches.Names,
        RequiredDocuments:
        [
            new("Individual KYC",
                [
                    "Passport",
                    "Driving license",
                    "Permanent Account Number (PAN) card",
                    "‘Election Commission of India’ Voter identity card",
                    "‘NREGA’ Job card, duly signed by an officer of the State Government",
                    "‘Unique Identification Authority of India’ letter containing details of name, address and Aadhaar Number, or any document as notified by the Central Government in consultation with the regulator",
                ],
                [
                    "The PAN copy is collected first, before any proof of address.",
                    "An Aadhaar is accepted masked or unmasked, when the name and date of birth on it match the PAN. Where its 12-digit number cannot be read, it is typed for the PAN–Aadhaar link.",
                ]),
            new("Sole Proprietorship",
                [
                    "ID & address proof of the proprietor with self attestation",
                    "PAN card of proprietor with self attestation",
                    "Proprietor Address proof with self attestation",
                    "Photograph",
                    "If the sole proprietorship is in a different name, the bank statement or registration certificate",
                    "GST & Udyam Certificate or Trade License required",
                    "Cancelled cheque leaf for bank account verification",
                ],
                []),
            new("NRI - Individual KYC",
                [
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
                ],
                []),
            new("HUF Deposits",
                [
                    "HUF Pan copy with self attestation",
                    "Latest HUF address proof required",
                    "Valid address proof & identity proof of Karta",
                    "Karta Photograph",
                    "Cancelled cheque leaf for bank account verification",
                    "HUF declaration required",
                ],
                []),
            new("Companies",
                [
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
                ],
                []),
            new("Partnership Firms",
                [
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
                ],
                []),
            new("Trust and Foundations", TrustItems(npo: true), []),
            new("Charitable Trust", TrustItems(npo: true), []),
            new("Family Trust", TrustItems(npo: false), []),
            new("Club, Association, Society",
                [
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
                ],
                []),
        ],
        IdentificationNotes:
        [
            "Only individual depositors 18 years and above are allowed to make investments.",
            "We strongly advice the depositor(s) to avail the nomination",
            "For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com",
        ],
        DashboardNotes:
        [
            "Currently Esarathi is enabled with individual and sole proprietorship investment only.",
            "Verification of the Fixed Deposit is subject to validation of the submitted documents by the Operations team.",
        ],
        Declarations:
        [
            "I have met every holder, seen the original documents, and the uploads are true copies of them.",
            "The deposit terms above, including the rate and that it locks on realisation, were read out to the investor.",
            "I have not received or promised any cash consideration outside this application.",
        ],
        NoticeKinds: ["Rate change", "Maintenance"],
        // The old RenewalDashboard's notes, word for word.
        RenewalNotes:
        [
            "Deposits due for renewal only will be displayed in this module.",
            "You are not allowed to make any changes of depositors in this module.",
            "Please keep the renewal documents ready to upload before proceeding.",
            "Details appearing in Renewal Application/FDR will be considered to renew the Deposits.",
            $"You will be able to make renewal entry in this module only from {MockRenewals.FromDays} days upto {MockRenewals.UntilDays} days prior to maturity.",
            "Changes in the Second holder/Third holder OR any other information shall be executed as per the details mentioned in Renewal Application/FDR.",
            "Any renewal of Deposit in advance of its maturity date, will be subject to the rate of interest and other terms & conditions prevailing on the date of said maturity.",
            $"The Auto renewal tag cases will be able to make renewal entry in this module only from {MockRenewals.FromDays} days up to {MockRenewals.UntilDaysAutoRenewal} days prior to maturity.",
        ],
        Features:
        [
            new("new-fd", "Create New FD", "Apply for a new FD", "The booking wizard end to end, from investor search to submission.", "Unavailable"),
            new("pis", "PIS Generation - Axis", "Apply for a new FD", "Making and reprinting Axis pay-in slips. A slip already printed stays valid.", "Unavailable"),
            new("view-app", "View existing application", "Apply for a new FD", "Looking an application up by number, folio or date.", "Unavailable"),
            new("short-url", "Short URL", "Apply for a new FD", "The payment and acceptance links sent to investors. A link already sent stops opening.", "Unavailable"),
            new("app-status", "Application status", "FD Services", "Where an application stands, holder by holder.", "Under revamp"),
            new("renew", "Renew FD", "FD Services", "Rolling a maturing deposit over into a new one.", "Coming soon"),
            new("admin", "Console Admin", "Administration", "Scheduling downtime windows and notices for the console.", "Unavailable", Tile: false),
        ],
        // Asked on FD Configuration once the investor's deposits with us pass the limit (AppConfig.SourceOfFundsFrom).
        SourcesOfFunds:
        [
            new("salary", "Salary"),
            new("business", "Business income"),
            new("savings", "Savings"),
            new("property", "Sale of property"),
            new("inheritance", "Inheritance or gift"),
            new("investments", "Maturity of investments"),
            new("loan", "Loan"),
            new("other", "Other"),
        ],
        // Which sub occupations go with which occupation; none listed means any.
        SubOccupationRules:
        [
            new("MMFSL Employee", ["Salaried"]),
            new("Private sector", ["Salaried"]),
            new("Public sector", ["Salaried"]),
            new("Government service", ["Salaried"]),
            new("Professional", ["Self-employed", "Business"]),
        ]);

    private static readonly AppConfig Config = new(
        SourcingAgency: SourcingAgency,
        MinAge: 18,
        SeniorAge: 60,
        MaxJointHolders: 2,
        MaxAttempts: 3,
        MinAmount: 5_000,
        MaxAmount: 5_00_00_000,
        AmountStep: 1,
        CancellationDays: MockWindow.Days,
        DraftDays: 14,
        LinkValidityHours: new Dictionary<string, int> { ["payment"] = PaymentLinkHours, ["acceptance"] = 72 },
        RenewFromDays: MockRenewals.FromDays, RenewUntilDays: MockRenewals.UntilDays, RenewUntilDaysAutoRenewal: MockRenewals.UntilDaysAutoRenewal,
        QuoteAmount: 50_000,
        CloseToCancelDays: 3,
        SourceOfFundsFrom: 1_00_00_000,
        SourceOfFundsOccupations: ["Homemaker", "Student", "Retired"],
        SourceOfFundsIncomeBands: ["Upto Rs.5,00,000"],
        OverMaxAmountMessage: "For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com");

    public Task<ReferenceData> ReferenceAsync(CancellationToken ct = default) => Task.FromResult(Data);

    public Task<AppConfig> ConfigAsync(CancellationToken ct = default) => Task.FromResult(Config);

    // The three trust lists are one list; only a family trust skips the two NPO lines.
    private static string[] TrustItems(bool npo) =>
    [
        "PAN card",
        "Address proof (Valid GST Certificate/bank statement/telephone bill)",
        .. npo ? new[] { "Non Profit Organisation(Yes/No)", "Darpan Registration mandatory for NPO's" } : [],
        "Registration certificate of the Trust/Charitable/Family & Foundation",
        "Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures",
        "Authorised signatory list",
        "ID & address proof of authorised signatories with self attestation",
        "Photograph of the Trustees & signatories",
        "Cancelled cheque leaf for bank account verification",
        "FATCA Declaration",
        "Beneficial Owner form along with BO Owners self attested KYC documents",
    ];
}

/// <summary>The partner signed in, as the backend would know them from their sign-in.</summary>
public sealed class MockPartner(IPartner partner) : IPartnerApi
{
    // The users MockSessions starts sessions for: a 1033 partner, and two brokers.
    private static readonly Dictionary<string, PartnerProfile> Profiles = new()
    {
        ["100002225"] = new("Shivaprasad Terse", "100002225", MockReference.SourcingAgency, "BR10021"),
        ["100002226"] = new("Rohan Deshmukh", "100002226", "2001", "BR10874"),
        ["100002227"] = new("Kavita Rao", "100002227", "2001", "BR10877"),
    };

    public Task<PartnerProfile> MeAsync(CancellationToken ct = default) =>
        Task.FromResult(Profiles.GetValueOrDefault(partner.Id) ?? Profiles["100002225"]);
}

/// <summary>
/// The Samruddhi rate chart and a handful of bank branches. A cumulative deposit
/// compounds yearly, as the chart's "amount payable" column does; one that pays out
/// pays simple interest each period.
/// </summary>
public sealed class MockDeposits : IDepositApi
{
    /// <summary>The day the chart took effect.</summary>
    public static readonly DateOnly ChartFrom = new(2026, 8, 3);

    // The chart's public rates by tenure: cumulative, then monthly, quarterly,
    // half-yearly and yearly payouts.
    private static readonly (int Months, decimal Maturity, decimal Monthly, decimal Quarterly, decimal HalfYearly, decimal Yearly)[] Chart =
    [
        (12, 6.60m, 6.40m, 6.45m, 6.50m, 6.60m),
        (18, 6.60m, 6.40m, 6.45m, 6.50m, 6.60m),
        (24, 6.85m, 6.65m, 6.70m, 6.75m, 6.85m),
        (30, 6.85m, 6.65m, 6.70m, 6.75m, 6.85m),
        (36, 7.40m, 7.15m, 7.20m, 7.25m, 7.40m),
        (42, 7.40m, 7.15m, 7.20m, 7.25m, 7.40m),
        (48, 7.45m, 7.20m, 7.25m, 7.30m, 7.45m),
        (60, 7.45m, 7.20m, 7.25m, 7.30m, 7.45m),
    ];

    // The smallest deposit each payout is offered for; the chart applies up to 5 crore.
    private static readonly (string Payout, long AmountFrom)[] MinimumAmounts =
    [
        ("maturity", 5_000),
        ("monthly", 50_000),
        ("quarterly", 50_000),
        ("halfyearly", 25_000),
        ("yearly", 25_000),
    ];
    private const long ChartCeiling = 5_00_00_000;

    // What a category earns over the public rate: senior citizens and employees 0.35, women 0.05.
    private static readonly Dictionary<string, decimal> ExtraRate = new()
    {
        ["PUBLIC/GENERAL"] = 0m,
        ["WOMEN"] = 0.05m,
        ["SR CITIZEN"] = 0.35m,
        ["SR CITIZEN WOMEN"] = 0.40m,
        ["EMPLOYEE"] = 0.35m,
        ["EMPLOYEE WOMEN"] = 0.40m,
    };

    private const int CompoundingPerYear = 1;

    private static readonly Dictionary<string, int> PerYear = new()
    {
        ["maturity"] = 0, ["yearly"] = 1, ["halfyearly"] = 2, ["quarterly"] = 4, ["monthly"] = 12,
    };

    internal static readonly BankBranch[] Branches =
    [
        new("HDFC0000521", "HDFC Bank", "Andheri East, Mumbai", "400240015"),
        new("HDFC0000123", "HDFC Bank", "Baner, Pune", "411240012"),
        new("ICIC0000104", "ICICI Bank", "Fort, Mumbai", "400229002"),
        new("SBIN0000575", "State Bank of India", "Shivajinagar, Pune", "411002003"),
        new("UTIB0000014", "Axis Bank", "Naupada, Thane", "400211003"),
    ];

    /// <summary>
    /// The chart for a category: the public rate plus what the category earns over
    /// it, the same for either gender and for a purchase or a renewal. An unknown
    /// category earns the public rate.
    /// </summary>
    public Task<IReadOnlyList<RateOption>> RatesAsync(RatesRequest request, CancellationToken ct = default)
    {
        var extra = ExtraRate.GetValueOrDefault(request.Category, 0m);
        var lines = new List<RateOption>();
        foreach (var row in Chart)
        {
            foreach (var (payout, minAmount) in MinimumAmounts)
            {
                var scheme = RateCard.NonCumulative;
                var rate = row.Yearly;
                if (payout == "maturity")
                {
                    scheme = RateCard.Cumulative;
                    rate = row.Maturity;
                }
                if (payout == "monthly") rate = row.Monthly;
                if (payout == "quarterly") rate = row.Quarterly;
                if (payout == "halfyearly") rate = row.HalfYearly;
                lines.Add(new RateOption(row.Months, scheme, payout, rate + extra, minAmount, ChartCeiling, ChartFrom));
            }
        }
        return Task.FromResult<IReadOnlyList<RateOption>>(lines);
    }

    public async Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var starts = request.Card.StartsOn ?? today;
        var perYear = PerYear.TryGetValue(request.Payout, out var p) ? p : throw new ArgumentException($"No payout called {request.Payout}.");

        RateOption? line = null;
        foreach (var option in await RatesAsync(request.Card, ct))
        {
            if (option.TenureMonths != request.TenureMonths) continue;
            if (option.Payout != request.Payout) continue;
            if (!option.Offers(request.Amount)) continue;
            line = option;
            break;
        }
        if (line is null) throw new ArgumentException($"The rate chart offers no {request.Payout} payout for {request.TenureMonths} months on a deposit of {request.Amount}.");

        decimal amount = request.Amount;
        var maturity = MaturityOf(amount, line.Rate, request.TenureMonths, perYear);
        var each = DepositMaths.InterestEach(amount, line.Rate, perYear);
        return new DepositQuote(line.Rate, each, maturity, starts.AddMonths(request.TenureMonths), line.AsOn);
    }

    /// <summary>What a deposit comes to at maturity: compounded yearly for a cumulative one, the amount itself otherwise.</summary>
    internal static decimal MaturityOf(decimal amount, decimal rate, int months, int perYear)
    {
        if (perYear != 0) return amount;
        return DepositMaths.MaturityAmount(amount, rate, months, CompoundingPerYear);
    }

    public Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default) =>
        Task.FromResult(Branches.FirstOrDefault(b => b.Ifsc.Equals(ifsc.Trim(), StringComparison.OrdinalIgnoreCase)));

    // Every word typed must be in the bank, the branch, the IFSC or the MICR; a
    // branch whose IFSC or MICR starts with what was typed comes first.
    public Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default)
    {
        var words = query.Split(' ', ',', '—', '-').Select(w => w.Trim()).Where(w => w.Length > 0).ToArray();
        IReadOnlyList<BankBranch> found = words.Length == 0 ? [] : Branches
            .Where(b => words.All(w => $"{b.Bank} {b.Branch} {b.Ifsc} {b.Micr}".Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(b => b.Ifsc.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase) || b.Micr.StartsWith(query.Trim()) ? 0 : 1)
            .ThenBy(b => b.Bank).ThenBy(b => b.Branch)
            .Take(20).ToList();
        return Task.FromResult(found);
    }
}
