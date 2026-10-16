namespace UnoTP.Models;

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

    /// <summary>GET renewals/window: how many days before maturity renewal entry opens and closes, as the FD system's config has it for this user.</summary>
    Task<RenewalWindow> WindowAsync(CancellationToken ct = default);
}

/// <summary>
/// The renewal window, from the FD system's web config (RENEWAL_PERIOD): entry opens
/// <paramref name="MaxDays"/> days before a deposit matures and closes
/// <paramref name="MinDays"/> days before; a request can be cancelled until
/// <paramref name="CancelMinDays"/> days before.
/// </summary>
public sealed record RenewalWindow(int MinDays, int MaxDays, int CancelMinDays);

/// <summary>A deposit on a folio, as the register holds it.</summary>
/// <param name="Category">A <see cref="CategoryOption.Code"/>: a renewal is quoted under it.</param>
/// <param name="Payout">A <see cref="PayoutOption.Code"/>.</param>
/// <param name="Status">due (a renewal can be entered), auto (tagged for auto renewal: it renews by itself), requested (a renewal request is already raised in the FD system), held (a loan or a freeze stands on it), renewing (a renewal is being entered here) or renewed (one is submitted here, or processed by the FD system). The register lists only deposits inside the renewal window.</param>
/// <param name="Renewable">Whether a renewal can be entered now: due, and not renewed already.</param>
/// <param name="Why">Why not, when it cannot be; empty otherwise.</param>
/// <param name="JointHolders">The joint holders on the deposit, in order; they come on to a renewal.</param>
/// <param name="Repayment">The account the deposit repays into, which a renewal opens with.</param>
/// <param name="AutoRenewal">Tagged for auto renewal: it renews on maturity by itself, so no renewal is entered here.</param>
/// <param name="Renewal">The application that renews it, while one is being entered or is submitted; null otherwise.</param>
public sealed record HeldDeposit(
    string Number, string Folio, string Investor, string Category,
    long Amount, decimal Rate, int TenureMonths, string Payout,
    DateOnly MaturesOn, long MaturityAmount,
    string Status, bool Renewable, string Why = "",
    IReadOnlyList<DepositHolder>? JointHolders = null, BankAccount? Repayment = null, bool AutoRenewal = false, RenewalApp? Renewal = null);

/// <summary>
/// The application that renews a deposit. A draft is picked up again or cancelled by
/// the partner who opened it; once the deposit's renewal window closes it is cancelled
/// by the separate cancelling process, not by this app. A submitted one can be cancelled only while it is a digital application
/// the investor has not accepted yet; a physical one cannot be cancelled here.
/// </summary>
/// <param name="Submitted">False for a draft, still being entered.</param>
/// <param name="NextStep">For a draft, the step it stopped before.</param>
/// <param name="Mine">Whether the partner asking opened it.</param>
/// <param name="CanCancel">Whether the renewal request can be cancelled now.</param>
/// <param name="CancelWhy">Why not, when it cannot be; empty otherwise.</param>
public sealed record RenewalApp(string AppNo, bool Submitted, string NextStep, bool Mine, bool CanCancel, string CancelWhy = "");

/// <summary>A joint holder on a deposit: enough to find them on the register.</summary>
public sealed record DepositHolder(string Pan, string Dob, string Name, string Folio);
