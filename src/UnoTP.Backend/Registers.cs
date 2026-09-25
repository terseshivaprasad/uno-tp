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

    /// <summary>
    /// POST payin-slips/{appNo}: issues the application's pay-in slip - or a fresh
    /// one, for a reprint - and returns the row as it now stands. Null when the
    /// application cannot have a slip: not found, cancelled, or digital and not
    /// yet accepted.
    /// </summary>
    Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default);
}

/// <param name="State">pending, generated, deposited or lapsed.</param>
/// <param name="AcceptedOn">When the investor accepted a digital application; null until they do.</param>
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
    string? SlipNo,
    DateTime? AcceptedOn = null);

/// <summary>The short links sent to investors, and the applications that can carry one.</summary>
public interface ILinkApi
{
    Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default);

    /// <summary>The partner's applications waiting on the investor.</summary>
    Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default);

    /// <summary>
    /// POST links { appNo, purpose }: sends the investor a link - payment or
    /// acceptance - replacing any sent before, which stops working. The link as
    /// sent, or null when the application cannot carry one.
    /// </summary>
    Task<SentLinkRecord?> SendAsync(string appNo, string purpose, CancellationToken ct = default);
}

/// <summary>A link sent, by SMS and e-mail both. The link itself is never returned: it goes to the investor and nowhere else.</summary>
/// <param name="Mobile">The mobile number it went to, masked.</param>
/// <param name="Email">The e-mail address it went to, masked; empty when the application has none.</param>
/// <param name="Purpose">payment or acceptance.</param>
/// <param name="State">open, opened, done or expired.</param>
/// <param name="Applied">When the application the link was raised against was created.</param>
public sealed record SentLinkRecord(
    string AppNo, string Investor, string Mobile, string Email, string Purpose,
    DateTime SentAt, DateTime ExpiresAt, string State, DateTime Applied);

/// <summary>An application waiting on the investor, with the mobile and e-mail a link would go to, masked.</summary>
/// <param name="Due">payment or acceptance: what the investor has still to do.</param>
public sealed record PendingRecord(string AppNo, string Investor, DateTime Applied, string Mobile, string Due, string Email = "");

/// <summary>What the console's administrator has scheduled.</summary>
public interface IConsoleApi
{
    Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default);

    /// <summary>POST console/windows: takes features off for a window. The backend mints its id and records who set it.</summary>
    Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default);

    /// <summary>POST console/announcements: a notice every partner sees in the bell.</summary>
    Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default);

    /// <summary>POST console/windows/{id}/end: ends a window that is on now, or cancels one still to come. False when there is none.</summary>
    Task<bool> EndWindowAsync(string id, CancellationToken ct = default);

    /// <summary>DELETE console/announcements/{id}: takes a notice out of the bell. False when there is none.</summary>
    Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default);
}

/// <param name="Notice">The line partners are shown in the bell; empty to leave them untold.</param>
public sealed record NewWindow(IReadOnlyList<string> Features, DateTime From, DateTime To, string Notice);

public sealed record NewAnnouncement(string Kind, string Title, DateTime At, string Detail);

public sealed record ConsoleSchedule(IReadOnlyList<WindowRecord> Windows, IReadOnlyList<AnnouncementRecord> Announcements);

/// <summary>A stretch of time in which the features it names are off.</summary>
/// <param name="Notice">The line partners are shown; empty when the window is not announced.</param>
public sealed record WindowRecord(
    string Id, IReadOnlyList<string> Features, DateTime From, DateTime To,
    string Notice, string SetBy, DateTime SetOn);

/// <summary>A notice with nothing to disable: told at one moment rather than over a window.</summary>
public sealed record AnnouncementRecord(
    string Id, string Kind, string Title, DateTime At, string Detail, string SetBy, DateTime SetOn);
