namespace UnoTP.Backend;

/// <summary>
/// Renew FD: the deposits a folio holds, and opening a renewal of one. A renewal
/// is an application like a new deposit's - the same steps, submitted and
/// accepted the same way - opened from the deposit, with what the deposit already
/// has on it: its holders, the account it repays into, and its maturity amount as
/// the new deposit's amount.
/// </summary>
public interface IRenewalApi
{
    /// <summary>GET folios/{folio}/deposits: the deposits on a folio, soonest to mature first - empty for a folio with none, null (404) for no such folio.</summary>
    Task<IReadOnlyList<HeldDeposit>?> DepositsByFolioAsync(string folio, CancellationToken ct = default);

    /// <summary>GET deposits?pan=&amp;dob=: the deposits held by the investor with that PAN and date of birth (dd-MM-yyyy), or null (404) for none on record.</summary>
    Task<IReadOnlyList<HeldDeposit>?> DepositsByPanAsync(string pan, string dob, CancellationToken ct = default);

    /// <summary>GET deposits/{number}: one deposit, or null (404).</summary>
    Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default);

    /// <summary>
    /// POST renewals { depositNumber }: opens the application that renews the
    /// deposit, with <see cref="Application.Renewal"/> set and the deposit's joint
    /// holders, repayment account and maturity amount already on it. Null (409)
    /// when the deposit cannot be renewed now.
    /// </summary>
    Task<Application?> StartAsync(string depositNumber, CancellationToken ct = default);

    /// <summary>
    /// DELETE renewals/{depositNumber}: cancels the renewal request opened for the
    /// deposit - its application, while not yet submitted - and the deposit is due
    /// for renewal again. False when there is none to cancel.
    /// </summary>
    Task<bool> CancelAsync(string depositNumber, CancellationToken ct = default);
}

/// <summary>A deposit on a folio, as the register holds it.</summary>
/// <param name="Category">A <see cref="CategoryOption.Code"/>: a renewal is quoted under it.</param>
/// <param name="Payout">A <see cref="PayoutOption.Code"/>.</param>
/// <param name="Status">running (too early), due (inside the renewal window), late (too near maturity: Operations'), matured, or renewed.</param>
/// <param name="Renewable">Whether a renewal can be entered now: due, and not renewed already.</param>
/// <param name="Why">Why not, when it cannot be; empty otherwise.</param>
/// <param name="JointHolders">The joint holders on the deposit, in order; they come on to a renewal.</param>
/// <param name="Repayment">The account the deposit repays into, which a renewal opens with.</param>
/// <param name="AutoRenewal">Tagged for auto renewal: its renewal window closes earlier.</param>
public sealed record HeldDeposit(
    string Number, string Folio, string Investor, string Category,
    long Amount, decimal Rate, int TenureMonths, string Payout,
    DateOnly StartedOn, DateOnly MaturesOn, long MaturityAmount,
    string Status, bool Renewable, string Why = "",
    IReadOnlyList<DepositHolder>? JointHolders = null, BankAccount? Repayment = null, bool AutoRenewal = false);

/// <summary>A joint holder on a deposit: enough to find them on the register.</summary>
public sealed record DepositHolder(string Pan, string Dob, string Name, string Folio);
