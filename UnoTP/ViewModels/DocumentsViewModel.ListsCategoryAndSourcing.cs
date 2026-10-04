using UnoTP.Models;

namespace UnoTP.ViewModels;

// What the backend offers the step: its lists and rules, the deposit's category and how the application is sourced.
public partial class DocumentsViewModel
{
    // ----- What the backend offers, and who is asking ---------------------------
    // Read before the page is drawn (see ReadyAsync): every list the step offers,
    // the rules it keeps, and the partner at the keyboard. None of it is written
    // here.

    /// <summary>Every list the step offers, from the backend.</summary>
    public ReferenceData Ref { get; private set; } = null!;

    /// <summary>The limits and rules the step keeps, from the backend.</summary>
    public AppConfig Config { get; private set; } = null!;

    /// <summary>The partner at the keyboard, from the backend.</summary>
    public PartnerProfile Partner { get; private set; } = null!;

    /// <summary>
    /// Reads the lists, the rules and the partner, and the names behind the sourcing
    /// codes the application holds. Every request calls it before the model is used.
    /// </summary>
    public async Task<DocumentsViewModel> ReadyAsync(CancellationToken ct = default)
    {
        var (reference, config, partner) = (lookups.ReferenceAsync(ct), lookups.ConfigAsync(ct), currentPartner.ProfileAsync(ct));
        (Ref, Config, Partner) = (await reference, await config, await partner);
        await ReadSourcingNamesAsync(ct);
        return this;
    }

    // The brokers and the staff are far too many to read whole, so only the codes the
    // application holds are looked up, a row each: the source code, the sub broker and
    // the employee. A page is always drawn on a request of its own, after a post has
    // been saved, so the codes read here are the ones the page shows.
    private async Task ReadSourcingNamesAsync(CancellationToken ct)
    {
        var s = State;
        var mode = ModeOf(s.Sourcing);
        sourceParty = await FindPartyAsync(RegisterName(mode, sub: false), s.SourceCode, mode, ct);
        subParty = await FindPartyAsync(RegisterName(mode, sub: true), s.SubBroker, mode, ct);
        employee = await FindPartyAsync("staff", s.EmpCode, mode, ct);
    }

    // The party a code names on a register ("brokers" or "staff"); null for no code, no
    // register, or a code the register does not hold. The staff are looked for in the
    // departments the sourcing mode takes.
    private async Task<Party?> FindPartyAsync(string? register, string code, SourcingModeOption? mode, CancellationToken ct)
    {
        if (code.Length == 0) return null;
        if (register == "brokers") return await sourcing.BrokerAsync(code, ct);
        if (register == "staff") return await sourcing.StaffMemberAsync(code, DepartmentsOf(mode), ct);
        return null;
    }

    /// <summary>The staff departments a sourcing mode takes; none for every department.</summary>
    public static IReadOnlyList<string> DepartmentsOf(SourcingModeOption? mode) => mode?.Departments ?? [];

    public IReadOnlyList<Option> ApplicationTypes => Ref.ApplicationTypes;

    /// <summary>What a digital application carries where a paper one carries its form number.</summary>
    public const string DigitalFormNo = "0000";

    /// <summary>The proofs of address the step takes, by type.</summary>
    public IReadOnlyList<string> ProofsOfAddress => [.. Ref.ProofsOfAddress.Select(p => p.Type)];

    /// <summary>
    /// The proofs a box takes: an Aadhaar, a passport, a driving licence or a voter
    /// ID - the proofsOfAddress list. A utility bill is not taken for either address.
    /// The permanent address is proved only by an officially valid document, one that
    /// carries the holder's photograph; were the list to carry a proof without one,
    /// it would prove the communication address alone.
    /// </summary>
    public IReadOnlyList<string> ProofsFor(string slot) =>
        slot == PoaSlot.Key ? [.. Ref.ProofsOfAddress.Where(p => p.HasPhoto).Select(p => p.Type)] : ProofsOfAddress;

    // "Aadhaar, Passport, Driving Licence or Voter ID".
    private static string OneOf(IReadOnlyList<string> types) =>
        types.Count <= 1 ? string.Join("", types) : string.Join(", ", types.Take(types.Count - 1)) + " or " + types[^1];

    // Only the modes settled by an instrument carry a document; the rest are
    // settled electronically and have nothing to file.
    public IReadOnlyList<PaymentModeOption> PaymentModes => Ref.PaymentModes;

    // The partner in the top bar, whose code fills the sourcing field.
    private string PartnerCode => Partner.Code;

    // ----- What a deposit is booked as -------------------------------------------

    /// <summary>Whether a category is booked against a staff record - which is what
    /// the block of employee fields is for, and only a sourcing agency books.</summary>
    public bool IsEmployee(string category) => Ref.Categories.Any(c => c.Code == category && c.Employee);

    /// <summary>A category as the backend names it.</summary>
    public string CategoryName(string code) => Ref.Categories.FirstOrDefault(c => c.Code == code)?.Name ?? code;

    /// <summary>
    /// The category the holder's date of birth and gender make them: a senior
    /// citizen from the senior age, and the women's category of either for a
    /// woman. With no gender known yet, the one the date of birth alone gives.
    /// </summary>
    public string CategoryFor(string dob, string gender, DateTime today)
    {
        var senior = DateTime.TryParseExact(dob, "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var born) && born.AddYears(Config.SeniorAge) <= today.Date;
        var woman = gender == Genders.Female;
        return Ref.Categories.FirstOrDefault(c => !c.Employee && c.Senior == senior && c.Women == woman)?.Code ?? "";
    }

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

    /// <summary>The ways an application can be sourced, in the backend's order.</summary>
    public IReadOnlyList<SourcingModeOption> SourcingModes => Ref.SourcingModes;

    /// <summary>The broker mode, the only one a partner other than the sourcing agency sources under.</summary>
    public SourcingModeOption BrokerMode => SourcingModes.First(m => m.Register == Register.Brokers);

    /// <summary>
    /// Whether the partner chooses how the application is sourced and what it is
    /// booked as: agency type 1033. Anyone else sources as a broker under their own
    /// business broker code, and the category follows the holder's date of birth
    /// and gender.
    /// </summary>
    public bool Chooses => Partner.AgencyType == Config.SourcingAgency;

    /// <summary>The business broker code a partner other than the sourcing agency files under.</summary>
    public string BusinessBroker => Partner.BrokerCode;

    /// <summary>The gender the category is set from: the folio's, else an Aadhaar's read here.</summary>
    public string HolderGender => GenderIn(State);

    // Read off the state handed in, not State, which settles through here. The
    // folio's gender, else an Aadhaar's read on this step, else what Investor
    // Information asked and saved.
    private string GenderIn(UploadState s)
    {
        if (Who.Gender.Length > 0) return Who.Gender;
        if (s.Gender.Length > 0) return s.Gender;
        return InvestorInformationGender;
    }

    /// <summary>
    /// Whether a holder may invest online, from the name screening service. Asked
    /// once per holder and name - the answer is kept on the application, and a call
    /// may be charged - so a holder answered already, under the same name, is not
    /// asked again. Throws ExternalServiceException when the service is not answering.
    /// </summary>
    public async Task<ScreeningOutcome> ScreenAsync(DocHolder h, string mobile)
    {
        var s = State;
        if (s.Screening.TryGetValue(h.Code, out var kept) && kept.Name == h.Who.Name) return kept;

        Doing($"Screening {h.Who.Name}\u2026");
        var result = await screening.ScreenAsync(new NameScreeningRequest(AppNo, h.Code, h.Who.Name, h.Who.Dob, mobile));
        var outcome = new ScreeningOutcome(result.Allowed, result.Reference, h.Who.Name, DateTime.Now);
        s.Screening[h.Code] = outcome;
        return outcome;
    }

    /// <summary>The investor's gender as Investor Information saved it, for a holder nothing read one for; empty until then.</summary>
    public string InvestorInformationGender =>
        App.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor)?.Gender ?? "";

    /// <summary>A category by code, or null.</summary>
    public CategoryOption? CategoryOf(string code) => Ref.Categories.FirstOrDefault(c => c.Code == code);

    /// <summary>
    /// The women's counterpart of a category: the one with the same employee and
    /// senior standing and Women set (Public / General to Women, Senior citizen to
    /// Senior citizen women, Employee to Employee women). Null for a women's category,
    /// or one with no counterpart.
    /// </summary>
    public CategoryOption? WomensCategoryFor(string code)
    {
        var category = CategoryOf(code);
        if (category is null) return null;
        if (category.Women) return null;
        return Ref.Categories.FirstOrDefault(c => c.Women && c.Employee == category.Employee && c.Senior == category.Senior);
    }

    /// <summary>
    /// Whether the deposit category and the investor's gender agree, once Investor
    /// Information has the gender. A female applicant under a non-women category:
    /// the category moves to its women's counterpart (<see cref="CategoryGenderOutcome.MovedTo"/>),
    /// so she gets the women's rate. A male applicant under a women's category: the
    /// category is wrong and is corrected on Upload Documents (<see cref="CategoryGenderOutcome.Conflict"/>).
    /// </summary>
    public CategoryGenderOutcome CategoryAgainstGender(string gender)
    {
        var category = CategoryOf(State.Category);
        if (category is null) return new CategoryGenderOutcome();

        if (gender == Genders.Female && WomensCategoryFor(category.Code) is { } womens)
        {
            return new CategoryGenderOutcome(MovedFrom: category, MovedTo: womens);
        }
        if (gender == Genders.Male && category.Women)
        {
            return new CategoryGenderOutcome(Conflict: $"The category on Upload Documents is {category.Name}, a women's category, but the applicant's gender is male. Correct the category on Upload Documents before proceeding.");
        }
        return new CategoryGenderOutcome();
    }

    /// <summary>Why the category stands as it does, for a partner who does not choose it.</summary>
    public string SetCategoryWhy =>
        (Who.Dob.Length == 0 ? "No date of birth is on record" : "Set from the date of birth")
        + (HolderGender.Length > 0 ? $" and gender ({HolderGender.ToLowerInvariant()})."
            : ". The gender is read off an Aadhaar filed as the proof of address; until then a women's category cannot be given.");

    // A partner other than 1033 has nothing to choose: broker mode, their own
    // code, and the category the holder's details set. Kept whenever the state is
    // read, so a saved state from before holds to it too.
    private void Settle(UploadState s)
    {
        if (Chooses) return;
        s.Sourcing = BrokerMode.Code;
        s.SourceCode = BusinessBroker;
        s.Category = CategoryFor(Who.Dob, GenderIn(s), DateTime.Today);
    }

    // Who the application's source code, sub broker and employee code name, as the
    // registers hold them; null for a code not on its register (ReadSourcingNamesAsync).
    private Party? sourceParty;
    private Party? subParty;
    private Party? employee;

    /// <summary>The name the staff register holds against the employee code, or null.</summary>
    public string? EmployeeName => employee?.Name;

    public IReadOnlyList<string> EmployeeHolders => Ref.EmployeeHolders;

    public IReadOnlyList<string> EmployeeRelations => Ref.EmployeeRelations;

    /// <summary>The primary holder, as the employee holders list names it: its first entry.</summary>
    public string PrimaryHolder => EmployeeHolders.FirstOrDefault() ?? "";

    /// <summary>The employee's relation with the primary holder when they are the primary holder: the relations list's first entry.</summary>
    public string SelfRelation => EmployeeRelations.FirstOrDefault() ?? "";

    /// <summary>The employee is the primary holder, so the relation is Self and is not chosen.</summary>
    public bool EmployeeIsPrimary => PrimaryHolder.Length > 0 && State.EmpHolder == PrimaryHolder;

    public IReadOnlyList<string> EmployeeProofs => Ref.EmployeeProofs;
}

/// <summary>What <see cref="DocumentsViewModel.CategoryAgainstGender"/> found: nothing, a move to a women's category, or a conflict to correct.</summary>
public sealed record CategoryGenderOutcome(CategoryOption? MovedFrom = null, CategoryOption? MovedTo = null, string? Conflict = null)
{
    /// <summary>What the page says when the category has moved to its women's counterpart.</summary>
    public string MovedMessage =>
        $"We notice a female applicant is selected under {MovedFrom?.Name}, a non-women category. The category has been updated to {MovedTo?.Name} to ensure they receive the applicable women's category benefits.";
}
