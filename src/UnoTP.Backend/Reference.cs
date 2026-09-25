namespace UnoTP.Backend;

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

/// <summary>A deposit category, and what booking under it takes.</summary>
/// <param name="Employee">Booked against a staff record, and open only to the sourcing agency.</param>
/// <param name="Women">For a woman holder.</param>
/// <param name="Senior">For a holder at the senior citizen age or over.</param>
public sealed record CategoryOption(string Code, string Name, bool Employee, bool Women, bool Senior);

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
public sealed record PayoutOption(string Code, string Name, int PerYear, string Each);

/// <summary>What an investor type hands over, and the notes that go with it.</summary>
public sealed record RequiredDocumentGroup(string Title, IReadOnlyList<string> Items, IReadOnlyList<string> Notes);

/// <summary>Every list the pages offer. Codes are what is posted and saved. <c>Declarations</c> are
/// what the partner signs on Review Summary before an application is submitted.</summary>
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
    IReadOnlyList<string> Declarations);

/// <summary>The limits and rules the pages check against. The backend checks them again on save.</summary>
/// <param name="SourcingAgency">The agency type that chooses how an application is sourced.</param>
/// <param name="MinAge">The youngest a depositor may be.</param>
/// <param name="SeniorAge">The age a holder is a senior citizen from.</param>
/// <param name="MaxJointHolders">Joint holders an application can carry beside the investor.</param>
/// <param name="MaxAttempts">Copies of one document refused one after another before it goes to Operations.</param>
/// <param name="MinAmount">The smallest deposit, in rupees.</param>
/// <param name="MaxAmount">The largest deposit booked online, in rupees.</param>
/// <param name="AmountStep">A deposit is a multiple of this, in rupees.</param>
/// <param name="CancellationDays">Days an unpaid application stands before it cancels itself.</param>
/// <param name="DraftDays">Days a saved, unsubmitted application is kept.</param>
/// <param name="LinkValidityHours">How long a link to the investor stays open, by what it asks of them ("payment", "acceptance").</param>
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
    IReadOnlyDictionary<string, int> LinkValidityHours);

/// <summary>Who the app is being used by: GET me, from the signed-in partner.</summary>
public interface IPartnerApi
{
    Task<PartnerProfile> MeAsync(CancellationToken ct = default);
}

/// <param name="Code">The partner's own code, which a staff-sourced application opens with.</param>
/// <param name="AgencyType">The sourcing agency (<see cref="AppConfig.SourcingAgency"/>) chooses how an
/// application is sourced; any other type sources as a broker under <paramref name="BrokerCode"/>.</param>
public sealed record PartnerProfile(string Name, string Code, string AgencyType, string BrokerCode);

/// <summary>What the backend works out for a deposit: the rate and what it comes to, and a bank branch by IFSC.</summary>
public interface IDepositApi
{
    /// <summary>POST deposits/quote: the card rate and the returns for a deposit as it stands.</summary>
    Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default);

    /// <summary>GET ifsc/{code}: the branch an IFSC names, or null (404) for none.</summary>
    Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default);
}

/// <param name="Payout">A <see cref="PayoutOption.Code"/>.</param>
/// <param name="Category">A <see cref="CategoryOption.Code"/>.</param>
/// <param name="StartsOn">The day the deposit is taken to start; the backend's today when null.</param>
public sealed record QuoteRequest(long Amount, int TenureMonths, string Payout, string Category, DateOnly? StartsOn = null);

/// <param name="Rate">The card rate, % a year, locked when the application is submitted.</param>
/// <param name="InterestEach">What each payout pays, for a non-cumulative deposit; 0 for a cumulative one.</param>
/// <param name="MaturityAmount">What is paid back at maturity.</param>
/// <param name="RateAsOn">The day the card rate is quoted as on.</param>
public sealed record DepositQuote(decimal Rate, decimal InterestEach, decimal MaturityAmount, DateOnly MaturesOn, DateOnly RateAsOn);

public sealed record BankBranch(string Ifsc, string Bank, string Branch, string Micr);
