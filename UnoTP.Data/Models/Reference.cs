namespace UnoTP.Models;

/// <summary>
/// What the pages offer and the rules they keep, from the backend: every drop-down
/// list, the required documents, the standing notes, and the limits and ages the
/// pages check against. None of it is written into the app, so a list or a limit
/// changes on the backend alone.
/// </summary>
public interface IReferenceApi
{
    /// <summary>GET reference: every list the pages offer.</summary>
    Task<ReferenceData> ReferenceAsync(CancellationToken ct = default);

    /// <summary>GET config: the limits and rules the pages check against.</summary>
    Task<AppConfig> ConfigAsync(CancellationToken ct = default);
}

/// <summary>A choice as it is posted, and as it is shown.</summary>
public sealed record Option(string Code, string Name);

/// <summary>
/// The FD system's own masters behind the lists whose choice is saved as the master's
/// code: each entry's code and name, as the master has them. The pages offer the names
/// (<see cref="ReferenceData.MaritalStatuses"/>, <see cref="ReferenceData.NomineeRelations"/>,
/// <see cref="ReferenceData.EmployeeRelations"/>); a save writes the code.
/// </summary>
/// <param name="EmployeeRelations">The employee's own relation - the employee is the holder - comes first.</param>
/// <param name="Occupations">The occupation master's rows: the pages offer its types as occupations and, under each, its sub-types.</param>
public sealed record MasterLists(
    IReadOnlyList<Option> MaritalStatuses, IReadOnlyList<Option> NomineeRelations, IReadOnlyList<Option> EmployeeRelations,
    IReadOnlyList<OccupationRow> Occupations)
{
    public static readonly MasterLists None = new([], [], [], []);

    /// <summary>The occupation master's row for an occupation and sub occupation as the pages name them; null where it has none.</summary>
    public OccupationRow? OccupationOf(string occupation, string subOccupation) =>
        Occupations.FirstOrDefault(o => o.TypeName == occupation && o.SubTypeName == subOccupation);

    /// <summary>The master's code for a name on a list; the name itself where the list does not hold it.</summary>
    public static string CodeOf(IReadOnlyList<Option> list, string name)
    {
        var entry = list.FirstOrDefault(o => o.Name == name);
        if (entry is null) return name;
        return entry.Code;
    }

    /// <summary>The name a list gives a code; the code itself where the list does not hold it.</summary>
    public static string NameOf(IReadOnlyList<Option> list, string code)
    {
        var entry = list.FirstOrDefault(o => o.Code == code);
        if (entry is null) return code;
        return entry.Name;
    }
}

/// <summary>
/// One row of the FD system's occupation master: a customer segment type (the
/// occupation) and sub-type (the sub occupation), and the CKYC occupation they stand for.
/// </summary>
public sealed record OccupationRow(
    string TypeCode, string TypeName, string SubTypeCode, string SubTypeName, string OccupationCode, string OccupationName);

/// <summary>A deposit category, and what booking under it takes.</summary>
/// <param name="Employee">Booked against a staff record, and open only to the sourcing agency.</param>
/// <param name="Women">For a woman holder.</param>
/// <param name="Senior">For a holder at the senior citizen age or over.</param>
/// <param name="ExtraRate">What the category earns over the public rate, % a year (the chart's "additional rates"); 0 for the public category.</param>
/// <param name="RateCategory">The category as the FD system's rate card names it (CATEGORY); empty when the list does not say.</param>
public sealed record CategoryOption(string Code, string Name, bool Employee, bool Women, bool Senior, decimal ExtraRate = 0, string RateCategory = "");

/// <summary>A payment mode, and the instrument a copy of is filed for it, if any.</summary>
public sealed record PaymentModeOption(string Name, string? Document);

/// <summary>
/// A way an application is sourced. <c>House</c> is the code the mode stands in the
/// first field itself, where it does; <c>Search</c> names the field that is searched
/// ("source" or "sub"), <c>Register</c> what it is searched against ("brokers",
/// "employees" or ""), <c>Sub</c> what the second field does ("shut", "house",
/// "free", "employee" or "employeeShut"), and <c>Categories</c> the category codes
/// a deposit under it may be booked as.
/// </summary>
public sealed record SourcingModeOption(
    string Code, string Name, string CodeLabel, string NameLabel,
    string House, string Search, string Register, string Sub, IReadOnlyList<string> Categories);

/// <summary>A proof of address, who confirms it, and whether it carries a photograph.</summary>
/// <param name="Issuer">Who is asked to confirm the address on it, or "" when nobody is.</param>
public sealed record ProofOption(string Type, string Issuer, bool HasPhoto);

/// <summary>How often a deposit pays interest: <c>PerYear</c> 0 is on maturity (cumulative).</summary>
/// <param name="Each">The period one payment covers, as a sentence names it ("quarter").</param>
/// <param name="InterestFreq">The payout as the FD system's rate card names it (INTEREST_FREQ); empty when the list does not say.</param>
/// <param name="Scheme">The scheme the rate card files the payout under (SCHEME); empty when the list does not say.</param>
public sealed record PayoutOption(string Code, string Name, int PerYear, string Each, string InterestFreq = "", string Scheme = "");

/// <summary>What an investor type hands over, and the notes that go with it.</summary>
public sealed record RequiredDocumentGroup(string Title, IReadOnlyList<string> Items, IReadOnlyList<string> Notes);

/// <summary>Every list the pages offer. Codes are what is posted and saved.</summary>
public sealed record ReferenceData(
    IReadOnlyList<Option> ApplicationTypes,
    IReadOnlyList<CategoryOption> Categories,
    IReadOnlyList<PaymentModeOption> PaymentModes,
    IReadOnlyList<SourcingModeOption> SourcingModes,
    IReadOnlyList<ProofOption> ProofsOfAddress,
    IReadOnlyList<string> EmployeeHolders,
    IReadOnlyList<string> EmployeeRelations,
    IReadOnlyList<string> EmployeeProofs,
    IReadOnlyList<string> IncomeBands,
    IReadOnlyList<string> Occupations,
    IReadOnlyList<string> SubOccupations,
    IReadOnlyList<string> MaritalStatuses,
    IReadOnlyList<string> Genders,
    IReadOnlyList<string> NameTypes,
    IReadOnlyList<string> NomineeRelations,
    IReadOnlyList<int> Tenures,
    IReadOnlyList<PayoutOption> Payouts,
    IReadOnlyList<Option> RenewInstructions,
    IReadOnlyList<Option> DeliveryTypes,
    IReadOnlyList<string> CmsLocations,
    IReadOnlyList<RequiredDocumentGroup> RequiredDocuments,
    IReadOnlyList<string> IdentificationNotes,
    IReadOnlyList<string> DashboardNotes,
    IReadOnlyList<string> NoticeKinds,
    IReadOnlyList<string> RenewalNotes,
    IReadOnlyList<FeatureOption>? Features = null,
    IReadOnlyList<Option>? SourcesOfFunds = null,
    IReadOnlyList<OccupationOption>? OccupationsWithSubs = null,
    IReadOnlyList<Option>? GatewayBanks = null,
    MasterLists? Masters = null)
{
    /// <summary>
    /// Whether the payment gateway takes an account at this IFSC for online payment:
    /// the bank code, the IFSC's first four letters, is on the gatewayBanks list. With
    /// no list at all every bank is taken.
    /// </summary>
    public bool OnPaymentGateway(string ifsc)
    {
        if (GatewayBanks is null || GatewayBanks.Count == 0) return true;
        if (ifsc.Length < 4) return true;
        var bankCode = ifsc[..4].ToUpperInvariant();
        return GatewayBanks.Any(b => b.Code.Equals(bankCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The sub occupations an occupation offers: its own list. An occupation with no
    /// list of its own offers every sub occupation; one with an empty list offers none
    /// and asks for no sub occupation.
    /// </summary>
    public IReadOnlyList<string> SubOccupationsFor(string occupation)
    {
        if (OccupationsWithSubs is null) return SubOccupations;
        var found = OccupationsWithSubs.FirstOrDefault(o => o.Name == occupation);
        if (found is null) return SubOccupations;
        return found.SubOccupations;
    }
}

/// <summary>An occupation and the sub occupations that go with it.</summary>
public sealed record OccupationOption(string Name, IReadOnlyList<string> SubOccupations);

/// <summary>
/// A console feature: a dashboard tile, the menu key that opens it, and what it is
/// called and says wherever the app names it.
/// </summary>
/// <param name="Group">The dashboard section its tile sits in.</param>
/// <param name="Detail">What stops working while it is off.</param>
/// <param name="OffReason">What its tile says while the app has it switched off.</param>
/// <param name="Tile">False for one switched like a feature but with no tile of its own (Console Admin).</param>
public sealed record FeatureOption(string Code, string Name, string Group, string Detail, string OffReason, bool Tile = true);

/// <summary>The limits and rules the pages check against. The backend checks them again on save.</summary>
/// <param name="SourcingAgency">The agency type that chooses how an application is sourced.</param>
/// <param name="MinAge">The youngest a depositor may be.</param>
/// <param name="SeniorAge">The age a holder is a senior citizen from.</param>
/// <param name="MaxJointHolders">Joint holders an application can carry beside the investor.</param>
/// <param name="MaxAttempts">Copies of one document refused one after another before it has to wait a while to be tried again.</param>
/// <param name="MinAmount">The smallest deposit, in rupees.</param>
/// <param name="MaxAmount">The largest deposit booked online, in rupees.</param>
/// <param name="AmountStep">A deposit is a multiple of this, in rupees; 1 for any amount.</param>
/// <param name="CancellationDays">Days an unpaid application stands before it cancels itself.</param>
/// <param name="DraftDays">Days since its last save an unsubmitted application stays on the lists to continue.</param>
/// <param name="LinkValidityHours">How long a link to the investor stays open, by what it asks of them ("payment", "acceptance").</param>
/// <param name="RenewFromDays">A renewal can be entered from this many days before the deposit matures...</param>
/// <param name="RenewUntilDays">...until this many days before maturity; nearer, it is Operations'.</param>
/// <param name="RenewUntilDaysAutoRenewal">The same, for a deposit tagged for auto renewal.</param>
/// <param name="OverMaxAmountMessage">What the amount field says over the maximum ("For investment above Rs.5 Cr, please write to..."); blank for the plain "Above the maximum".</param>
/// <param name="QuoteAmount">The amount FD Configuration quotes the rate at before one is entered.</param>
/// <param name="SourceOfFundsFrom">The source of funds is asked once the investor's active deposits, with the new one, pass this many rupees...</param>
/// <param name="SourceOfFundsOccupations">...and their occupation is one of these (homemaker, student, retired)...</param>
/// <param name="SourceOfFundsIncomeBands">...or their annual income band is one of these (up to ₹5 lakh).</param>
/// <param name="SourceOfFundsOther">The source of funds, by its master code, that takes a typed remark.</param>
public sealed record AppConfig(
    string SourcingAgency,
    int MinAge,
    int SeniorAge,
    int MaxJointHolders,
    int MaxAttempts,
    long MinAmount,
    long MaxAmount,
    long AmountStep,
    int CancellationDays,
    int DraftDays,
    IReadOnlyDictionary<string, int> LinkValidityHours,
    int RenewFromDays = 61,
    int RenewUntilDays = 7,
    int RenewUntilDaysAutoRenewal = 10,
    int CloseToCancelDays = 3,
    long QuoteAmount = 50_000,
    long SourceOfFundsFrom = 1_00_00_000,
    IReadOnlyList<string>? SourceOfFundsOccupations = null,
    IReadOnlyList<string>? SourceOfFundsIncomeBands = null,
    string OverMaxAmountMessage = "",
    string SourceOfFundsOther = "");

/// <summary>Who the app is being used by: GET me, from the signed-in partner.</summary>
public interface IPartnerApi
{
    Task<PartnerProfile> MeAsync(CancellationToken ct = default);
}

/// <param name="Code">The partner's own code, which a staff-sourced application opens with.</param>
/// <param name="AgencyType">The sourcing agency (<see cref="AppConfig.SourcingAgency"/>) chooses how an
/// application is sourced; any other type sources as a broker under <paramref name="BrokerCode"/>.</param>
/// <param name="AgencySubType">The agency's sub type, which the backend APIs are called with.</param>
/// <param name="AgencyCode">The agency's own code.</param>
/// <param name="UserName">The partner's user name on the portal.</param>
/// <param name="SysCode">The system code the portal came in with: which app this is, as the backend APIs know it.</param>
public sealed record PartnerProfile(
    string Name, string Code, string AgencyType, string BrokerCode,
    string AgencySubType = "", string AgencyCode = "", string UserName = "", string SysCode = "");

/// <summary>What the backend works out for a deposit: the rate table, the rate and what it comes to, and a bank branch by IFSC.</summary>
public interface IDepositApi
{
    /// <summary>
    /// GET deposits/rates: the rate card for a category, gender and application type -
    /// every tenure, scheme and payout frequency it offers, the rate of each, and the
    /// amounts it is offered for. FD Configuration draws its choices from this, at the
    /// amount entered (the standing <see cref="AppConfig.QuoteAmount"/> before one is).
    /// </summary>
    Task<IReadOnlyList<RateOption>> RatesAsync(RatesRequest request, CancellationToken ct = default);

    /// <summary>POST deposits/quote: the card rate and the returns for a deposit as it stands.</summary>
    Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default);

    /// <summary>GET ifsc/{code}: the branch an IFSC names, or null (404) for none.</summary>
    Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default);

    /// <summary>GET ifsc?q=: the branches whose bank name, branch, IFSC or MICR holds the text, best first; at most 20.</summary>
    Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default);
}

/// <summary>Whose rate card: the deposit's category, and whether the application is a fresh one or a renewal.</summary>
/// <param name="Category">A <see cref="CategoryOption.Code"/>: from the holder's date of birth and gender, or the sourcing agency's choice.</param>
/// <param name="ApplicationType"><see cref="RateCard.Purchase"/> or <see cref="RateCard.Renew"/>: the card's MODE_STATUS.</param>
/// <param name="StartsOn">The day the deposit is taken to start, for the day it matures; the backend's today when null. The card read is always today's.</param>
public sealed record RatesRequest(string Category, string ApplicationType, DateOnly? StartsOn = null);

/// <summary>The words the rate card is keyed by.</summary>
public static class RateCard
{
    /// <summary>A fresh application, as the card's MODE_STATUS has it.</summary>
    public const string Purchase = "AF";

    /// <summary>A maturing deposit renewed, as the card's MODE_STATUS has it.</summary>
    public const string Renew = "R";

    /// <summary>Interest paid with the principal at maturity.</summary>
    public const string Cumulative = "CUMULATIVE";

    /// <summary>The card's row for a tenure and payout at an amount, or null when it offers none.</summary>
    public static RateOption? Line(IReadOnlyList<RateOption> card, int tenureMonths, string payout, long amount)
    {
        foreach (var row in card)
        {
            if (row.TenureMonths != tenureMonths) continue;
            if (row.Payout != payout) continue;
            if (!row.Offers(amount)) continue;
            return row;
        }
        return null;
    }
}

/// <summary>One row of the rate card: a tenure, scheme and payout frequency, the rate, and the amounts it is offered for.</summary>
/// <param name="Scheme">The scheme as the rate card names it: <see cref="RateCard.Cumulative"/> for interest paid at maturity, or another for interest paid out through the tenure.</param>
/// <param name="Payout">A <see cref="PayoutOption.Code"/>: the frequency.</param>
/// <param name="Rate">% a year.</param>
/// <param name="MinAmount">The smallest deposit the row is offered for, in rupees.</param>
/// <param name="MaxAmount">The largest, or null for no ceiling.</param>
/// <param name="AsOn">The day the rate took effect.</param>
/// <param name="SchemeCode">The rate card's own code for the row (SCHEME_CODE).</param>
public sealed record RateOption(int TenureMonths, string Scheme, string Payout, decimal Rate, long MinAmount, long? MaxAmount, DateOnly AsOn, string SchemeCode = "")
{
    /// <summary>Whether the row is offered for a deposit of this amount.</summary>
    public bool Offers(long amount)
    {
        if (amount < MinAmount) return false;
        if (MaxAmount is not null && amount > MaxAmount) return false;
        return true;
    }
}

/// <param name="Payout">A <see cref="PayoutOption.Code"/>.</param>
/// <param name="Card">Whose rate card the deposit is quoted from.</param>
public sealed record QuoteRequest(long Amount, int TenureMonths, string Payout, RatesRequest Card);

/// <param name="Rate">The card rate, % a year, locked when the application is submitted.</param>
/// <param name="InterestEach">What each payout pays, for a non-cumulative deposit; 0 for a cumulative one.</param>
/// <param name="MaturityAmount">What is paid back at maturity.</param>
/// <param name="RateAsOn">The day the card rate is quoted as on.</param>
public sealed record DepositQuote(decimal Rate, decimal InterestEach, decimal MaturityAmount, DateOnly MaturesOn, DateOnly RateAsOn);

public sealed record BankBranch(string Ifsc, string Bank, string Branch, string Micr);
