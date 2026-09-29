using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>View Application: the partner's applications, to look one up.</summary>
public class ViewApplicationViewModel(IReadOnlyList<ApplicationRecord> applications, int windowDays)
{
    // The old ViewApplication screen asked for an application number before it
    // would show anything, and had no way to reach an investor's other
    // applications at all. This one opens on the last fourteen days - every
    // application still in flight, because one that goes unpaid for that long
    // cancels itself - and searches by application number or by folio.
    // The backend's cancellation window (GET config).
    public int WindowDays { get; } = windowDays;

    public static DateTime Today => DateTime.Today;

    // The oldest application day the list opens on.
    public DateTime Earliest => Today.AddDays(-(WindowDays - 1));

    // Where an application has got to. The key drives the filter; the label is
    // what the pill says and the text what the row's status cell says.
    public record AppState(string Key, string Label, string Text, string Tone);

    public static readonly AppState[] States =
    {
        new("progress", "In progress", "Not submitted", "text-amber"),
        new("awaiting", "Awaiting investor", "Awaiting investor", "text-amber"),
        new("review", "With Operations", "Under verification", "text-muted"),
        new("booked", "Booked", "Booked", "text-success"),
        new("cancelled", "Cancelled", "Cancelled", "text-danger"),
    };

    public record Milestone(string Label, string When, bool Done);

    // One application as this screen reads it: who it is for, what it is for, and
    // how far it has gone. Nothing here can be edited - the screen only looks.
    public record ApplicationRow(
        string AppNo,
        // An existing investor's folio. A new customer has none until the deposit
        // books, so a folio search never reaches their application before then.
        string? Folio,
        string Investor,
        string Pan,
        long Amount,
        bool Cumulative,
        int Months,
        string Payout,
        int Holders,
        int DaysOld,
        // How the investor signed up: a digital application is accepted online,
        // a physical one arrives on paper already signed.
        bool Digital,
        string Instrument,
        string Branch,
        string State,
        string? Fdr,
        // The wizard step an unfinished application stopped on.
        string Step,
        int WindowDays,
        string SchemeName,
        IReadOnlyList<MilestoneRecord> Milestones)
    {
        public DateTime Applied => Today.AddDays(-DaysOld);

        public bool InWindow => DaysOld < WindowDays;

        public int DaysLeft => WindowDays - DaysOld;

        public AppState Status => States.First(s => s.Key == State);

        public string Scheme => $"{SchemeName} · {(Cumulative ? "cumulative" : "non-cumulative")}";

        public string Tenure => Months % 12 == 0 ? $"{Months / 12} year{(Months == 12 ? "" : "s")}" : $"{Months} months";

        public DateTime Maturity => Applied.AddMonths(Months);

        public string FolioLabel => Folio ?? "—";

        public string FolioNote => Folio is null ? "opens on booking" : "existing investor";

        // What the status cell says on its second line: the one thing the
        // application is waiting for.
        public string Waiting => State switch
        {
            "progress" => $"stopped on {Step}",
            "awaiting" => Digital ? "acceptance and payment" : "payment",
            "review" => "documents with Operations",
            "booked" => Fdr is null ? "booked" : $"FDR {Fdr}",
            _ => $"unpaid after {WindowDays} days",
        };

        // The line under the application number: how long is left before the
        // application cancels itself, or that its clock has stopped.
        public string Countdown => State switch
        {
            "booked" => "booked · no longer at risk",
            "cancelled" => $"cancelled {Applied.AddDays(WindowDays):dd/MM/yyyy}",
            _ when DaysLeft <= 1 => "cancels today",
            _ => $"cancels in {DaysLeft} days",
        };

        public bool Urgent => State is not ("booked" or "cancelled") && DaysLeft <= 3;

        public string Tag => Digital ? "Digital" : "Physical";

        // The application's own history, as the backend dates it. A step not yet
        // reached carries no date.
        public List<Milestone> Timeline =>
            [.. Milestones.Select(m => new Milestone(m.Step, m.At?.ToString("dd/MM/yyyy") ?? "pending", m.At is not null))];
    }

    /// <summary>The partner's applications, from the backend.</summary>
    public IReadOnlyList<ApplicationRow> Rows => rows ??= applications.Select(ToRow).ToList();

    private List<ApplicationRow>? rows;

    // The backend dates each application; the page counts its days against the window.
    private ApplicationRow ToRow(ApplicationRecord r) => new(
        r.AppNo, r.Folio, r.Investor, r.Pan, r.Amount, r.Cumulative, r.Months, r.Payout, r.Holders,
        (Today - r.Applied.Date).Days, r.Digital, r.Instrument, r.Branch, r.State, r.Fdr, r.Step,
        WindowDays, r.Scheme, r.Milestones ?? []);

    // The list the page opens on: everything raised inside the window, newest
    // first, with the applications that still need someone at the top of each day.
    public IEnumerable<ApplicationRow> Listed =>
        Rows.Where(r => r.InWindow)
            .OrderBy(r => r.DaysOld)
            .ThenBy(r => r.State switch { "progress" => 0, "awaiting" => 1, "review" => 2, _ => 3 });

    // The older applications, kept out of the opening list but still findable by
    // application number or folio, so a search for one says what became of it.
    public IEnumerable<ApplicationRow> Older => Rows.Where(r => !r.InWindow);

    // The pay-in slip page writes rupees for the same applications, so the two
    // lists never disagree about an amount.
    public static string Rupees(long amount) => PayInSlipViewModel.Rupees(amount);
}
