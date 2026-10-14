namespace UnoTP.Models;

/// <summary>
/// The registers an application's sourcing codes are searched against: the brokers
/// and the staff. Each is far too big to read whole, so nothing here lists one: a
/// code is looked up by itself, and a search is run only on what was typed.
/// </summary>
public interface ISourcingApi
{
    /// <summary>The broker a code names, or null.</summary>
    Task<Party?> BrokerAsync(string code, CancellationToken ct = default);

    /// <summary>The employee a code names, or null, among the staff a sourcing mode's rule takes (<see cref="SourcingModeOption.Staff"/>); with no rule, any employee in service.</summary>
    Task<Party?> StaffMemberAsync(string code, string staffRule, CancellationToken ct = default);

    /// <summary>GET sourcing/brokers?q=: the brokers whose code or name holds every word of the text, best first; at most 20.</summary>
    Task<IReadOnlyList<Party>> SearchBrokersAsync(string query, CancellationToken ct = default);

    /// <summary>GET sourcing/staff?q=: the same, on the staff register, among the staff a sourcing mode's rule takes; with no rule, any employee in service.</summary>
    Task<IReadOnlyList<Party>> SearchStaffAsync(string query, string staffRule, CancellationToken ct = default);
}

/// <summary>A search of a big table (brokers, staff, bank branches) as its text is typed.</summary>
public static class TypedSearch
{
    /// <summary>The fewest characters a search is run on. On less, nothing is asked and nothing is listed.</summary>
    public const int FromLength = 3;

    /// <summary>The fewest characters the staff register is searched on: an employee's code is searched from five.</summary>
    public const int StaffFromLength = 5;

    /// <summary>The fewest characters a register ("staff" or "brokers") is searched on.</summary>
    public static int FromLengthOf(string? register)
    {
        if (register == "staff") return StaffFromLength;
        return FromLength;
    }

    /// <summary>Whether enough was typed for a search to be run.</summary>
    public static bool LongEnough(string text) => text.Trim().Length >= FromLength;

    /// <summary>Whether enough was typed for a register ("staff" or "brokers") to be searched.</summary>
    public static bool LongEnough(string text, string? register) => text.Trim().Length >= FromLengthOf(register);
}

/// <summary>A code and the name the register holds against it.</summary>
public sealed record Party(string Code, string Name);

/// <summary>The Axis pay-in slips for the partner's applications paid on paper.</summary>
public interface IPayInSlipApi
{
    /// <summary>
    /// The partner's applications paying by cheque that a search reaches, newest first:
    /// by the days they were raised on, or by part of an application number (cancelled
    /// ones included). Never the whole list, and never more than
    /// <see cref="ApplicationSearch.Most"/> of them.
    /// </summary>
    Task<SlipsFound> SearchAsync(ApplicationSearch search, CancellationToken ct = default);

    /// <summary>
    /// Every one of the partner's applications paying by cheque raised in the days an
    /// unpaid application stands, for the dashboard to count. Only those days are read.
    /// </summary>
    Task<IReadOnlyList<SlipRecord>> StillOpenAsync(CancellationToken ct = default);

    /// <summary>
    /// POST payin-slips/{appNo}: issues the application's pay-in slip and returns
    /// the row as it now stands. A slip is made once: an application that has one is
    /// given the same slip again, for a reprint, and nothing is written. Null when the
    /// application cannot have a slip: not found, cancelled, digital and not yet
    /// accepted, or its deposit row not standing as APR.
    /// </summary>
    Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default);

    /// <summary>The newest pay-in slip generated for one of the partner's applications, as it is printed; null when it has none.</summary>
    Task<SlipPrint?> SlipToPrintAsync(string appNo, CancellationToken ct = default);
}

/// <summary>
/// A generated pay-in slip as it is printed: its number, the application, and the
/// cheque on it (t_FD_BT_Payinslip_Dtl). The customer's name is whole here, as the
/// bank needs it.
/// </summary>
public sealed record SlipPrint
{
    public string SlipNo { get; init; } = "";
    public string AppNo { get; init; } = "";

    /// <summary>When the application was submitted.</summary>
    public DateTime ApplicationDate { get; init; }
    public string CustomerName { get; init; } = "";
    public long Amount { get; init; }
    public string ChequeNumber { get; init; } = "";
    public string BankName { get; init; } = "";
    public string BankIfsc { get; init; } = "";
    public string BranchName { get; init; } = "";

    /// <summary>The account the cheque is drawn on, from the application's payment row.</summary>
    public string AccountNumber { get; init; } = "";

    /// <summary>The Axis branch the cheque is presented at, by its SOL id.</summary>
    public string SolId { get; init; } = "";
}

/// <summary>What a search of the partner's cheque applications found.</summary>
/// <param name="Slips">The newest of them, up to <see cref="ApplicationSearch.Most"/>.</param>
/// <param name="MoreExist">Set when the search reaches more applications than are listed.</param>
public sealed record SlipsFound(IReadOnlyList<SlipRecord> Slips, bool MoreExist);

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

/// <summary>
/// The page a link is sent from. Each runs its own queries for the send (the page's
/// file under UnoTP.Data/Queries), shared with no other page.
/// </summary>
public enum LinkPage
{
    ReviewSummary,
    ApplicationSubmitted,
    ShortUrl,
}

/// <summary>The short links sent to investors, and the applications that can carry one.</summary>
public interface ILinkApi
{
    Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default);

    /// <summary>The partner's applications waiting on the investor.</summary>
    Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default);

    /// <summary>
    /// POST links { appNo, purpose, newLink }: sends the investor a link - payment or
    /// acceptance. Resend (newLink false) sends the link the application has again,
    /// the same short link, open for LinkRequestRow.RegeneratedForHours; a new link
    /// (newLink true) takes the old one out of use and sends a fresh one. The link as
    /// sent; or why it was not: the reason the limits refused it (CommLimits), or null
    /// when the application cannot carry one.
    /// </summary>
    Task<LinkSent> SendAsync(string appNo, string purpose, LinkPage from, bool newLink = false, CancellationToken ct = default);
}

/// <summary>A link sent, or why it was not: the reason the limits refused it, shown to the partner; null with no link when the application cannot carry one.</summary>
public sealed record LinkSent(SentLinkRecord? Link, string? Refused = null);

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
