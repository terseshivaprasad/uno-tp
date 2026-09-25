namespace UnoTP.Backend;

/// <summary>The registers an application's sourcing codes are searched against.</summary>
public interface ISourcingApi
{
    /// <summary>The brokers a broker-sourced application can be filed under.</summary>
    Task<IReadOnlyList<Party>> BrokersAsync(CancellationToken ct = default);

    /// <summary>The staff an employee code is searched against.</summary>
    Task<IReadOnlyList<Party>> StaffAsync(CancellationToken ct = default);
}

/// <summary>A code and the name the register holds against it.</summary>
public sealed record Party(string Code, string Name);

/// <summary>The Axis pay-in slips for the partner's applications paid on paper.</summary>
public interface IPayInSlipApi
{
    /// <summary>Every application paying by cheque or DD, cancelled ones included.</summary>
    Task<IReadOnlyList<SlipRecord>> SlipsAsync(CancellationToken ct = default);
}

/// <param name="State">pending, generated, deposited or lapsed.</param>
/// <param name="Digital">Accepted online rather than signed on paper.</param>
/// <param name="Accepted">Whether the investor has accepted the deposit.</param>
public sealed record SlipRecord(
    string AppNo,
    string Investor,
    long Amount,
    string Instrument,
    string InstrumentNo,
    string DrawnOn,
    DateTime Applied,
    string Branch,
    bool Digital,
    bool Accepted,
    string State,
    string? SlipNo);

/// <summary>The short links sent to investors, and the applications that can carry one.</summary>
public interface ILinkApi
{
    Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default);

    /// <summary>The partner's applications waiting on the investor.</summary>
    Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default);
}

/// <summary>A link sent. The link itself is never returned: it goes to the investor and nowhere else.</summary>
/// <param name="Purpose">payment or acceptance.</param>
/// <param name="State">open, opened, done or expired.</param>
/// <param name="Applied">When the application the link was raised against was created.</param>
public sealed record SentLinkRecord(
    string AppNo, string Investor, string Contact, string Purpose,
    DateTime SentAt, DateTime ExpiresAt, string State, DateTime Applied);

/// <summary>An application waiting on the investor, with the mobile a link would go to, masked.</summary>
/// <param name="Due">payment or acceptance: what the investor has still to do.</param>
public sealed record PendingRecord(string AppNo, string Investor, DateTime Applied, string Mobile, string Due);

/// <summary>What the console's administrator has scheduled.</summary>
public interface IConsoleApi
{
    Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default);
}

public sealed record ConsoleSchedule(IReadOnlyList<WindowRecord> Windows, IReadOnlyList<AnnouncementRecord> Announcements);

/// <summary>A stretch of time in which the features it names are off.</summary>
/// <param name="Notice">The line partners are shown; empty when the window is not announced.</param>
public sealed record WindowRecord(
    string Id, IReadOnlyList<string> Features, DateTime From, DateTime To,
    string Notice, string SetBy, DateTime SetOn);

/// <summary>A notice with nothing to disable: told at one moment rather than over a window.</summary>
public sealed record AnnouncementRecord(
    string Id, string Kind, string Title, DateTime At, string Detail, string SetBy, DateTime SetOn);
