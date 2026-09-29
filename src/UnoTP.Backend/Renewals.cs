namespace UnoTP.Backend;

/// <summary>
/// Renew FD: the deposits a folio holds, and renewing one. A renewal is asked for
/// here and accepted by the investor through a link, as a new deposit is; the
/// backend quotes the rate, records the request and sends the link.
/// </summary>
public interface IRenewalApi
{
    /// <summary>GET folios/{folio}/deposits: the deposits on a folio, newest first - empty for a folio with none, null (404) for no such folio.</summary>
    Task<IReadOnlyList<HeldDeposit>?> DepositsAsync(string folio, CancellationToken ct = default);

    /// <summary>GET deposits/{number}: one deposit, or null (404).</summary>
    Task<HeldDeposit?> DepositAsync(string number, CancellationToken ct = default);

    /// <summary>
    /// POST renewals: renews a deposit as asked. The backend quotes the rate as on
    /// the day, records the request and sends the investor the acceptance link; it
    /// answers with the request as recorded, or null (409) when the deposit cannot
    /// be renewed now.
    /// </summary>
    Task<RenewalRecord?> RenewAsync(NewRenewal renewal, CancellationToken ct = default);

    /// <summary>GET renewals: the partner's renewal requests, newest first.</summary>
    Task<IReadOnlyList<RenewalRecord>> RenewalsAsync(CancellationToken ct = default);
}

/// <summary>A deposit on a folio, as the register holds it.</summary>
/// <param name="Category">A <see cref="CategoryOption.Code"/>: a renewal is quoted under it.</param>
/// <param name="Payout">A <see cref="PayoutOption.Code"/>.</param>
/// <param name="Status">running, maturing (inside the renewal window), matured (the window not yet closed), renewed, or closed (paid out).</param>
/// <param name="Renewable">Whether a renewal can be asked for now: maturing or matured, and not renewed already.</param>
/// <param name="Why">Why not, when it cannot be; empty otherwise.</param>
public sealed record HeldDeposit(
    string Number, string Folio, string Investor, string Category,
    long Amount, decimal Rate, int TenureMonths, string Payout,
    DateOnly StartedOn, DateOnly MaturesOn, long MaturityAmount,
    string Status, bool Renewable, string Why = "");

/// <param name="Mode">A renew instruction's code: principal (the interest is paid out) or principal-interest (the maturity amount is renewed).</param>
public sealed record NewRenewal(string DepositNumber, string Mode, int TenureMonths, string Payout);

/// <summary>A renewal asked for, as the backend recorded it.</summary>
/// <param name="Amount">What is renewed: the principal, or the maturity amount.</param>
/// <param name="RenewsOn">The day the new deposit starts: maturity, or the day asked when the deposit has matured.</param>
/// <param name="Status">sent (the acceptance link is with the investor) or accepted.</param>
/// <param name="LinkSentTo">Where the link went, masked.</param>
public sealed record RenewalRecord(
    string Id, string DepositNumber, string Folio, string Investor,
    string Mode, long Amount, int TenureMonths, string Payout, decimal Rate,
    DateOnly RenewsOn, DateOnly MaturesOn, long MaturityAmount,
    string Status, DateTime RequestedAt, string LinkSentTo);
