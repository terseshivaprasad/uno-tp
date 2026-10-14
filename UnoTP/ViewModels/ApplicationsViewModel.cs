using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>View Application: the partner's applications a search reaches, to look one up.</summary>
public class ApplicationsViewModel(ApplicationsFound found, int windowDays, ApplicationSearch search)
{
    // The old ViewApplication screen asked for an application number before it
    // would show anything, and had no way to reach an investor's other
    // applications at all. This one opens on the last fourteen days - every
    // application still in flight, because one that goes unpaid for that long
    // cancels itself - and searches by application number or by folio.
    // The search is run by the database: the page holds what it reached, never
    // the partner's whole list.
    // The backend's cancellation window (GET config).
    public int WindowDays { get; } = windowDays;

    public static DateTime Today => DateTime.Today;

    // The oldest application day the list opens on.
    public DateTime Earliest => ListSearch.Earliest(WindowDays);

    /// <summary>The search the list on the page is the answer to.</summary>
    public ApplicationSearch Search { get; } = search;

    /// <summary>The first and the last day in the date boxes: the search's own, or the window's.</summary>
    public DateTime From => Search.From ?? Earliest;

    public DateTime To => Search.To ?? Today;

    /// <summary>Set when the list is the one the page opens on: the whole window, by dates.</summary>
    public bool IsOpeningList => ListSearch.IsOpening(Search, WindowDays);

    /// <summary>Set when the search reaches more applications than the page lists.</summary>
    public bool MoreExist { get; } = found.MoreExist;

    /// <summary>What the line under the search says the list holds.</summary>
    public string Scope
    {
        get
        {
            if (Search.By == ApplicationSearch.ByNumber) return $"Showing every application matching “{Search.Text}”, inside the window or older.";
            if (Search.By == ApplicationSearch.ByFolio) return $"Showing every application under folio “{Search.Text}”, however old.";
            if (IsOpeningList) return $"Showing the last {WindowDays} days — {From:dd/MM/yyyy} to {To:dd/MM/yyyy}.";
            return $"Showing applications raised {From:dd/MM/yyyy} to {To:dd/MM/yyyy}.";
        }
    }

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

    // A stage as the History list draws it: done and dated, pending, not one this
    // application goes through (and why), or come back against it.
    public record Milestone(string Label, string When, bool Done, bool Applies, bool Failed, string? Note);

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
        string State,
        string? Fdr,
        // The wizard step an unfinished application stopped on.
        string Step,
        int WindowDays,
        string SchemeName,
        IReadOnlyList<MilestoneRecord> Milestones)
    {
        public DateTime Applied => Today.AddDays(-DaysOld);

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
            // Cancelled by the user as a draft, or by the cancelling process as unpaid:
            // the row does not say which, so neither is claimed.
            _ => "no longer open",
        };

        // The line under the application number: how long is left before the
        // application cancels itself, or that its clock has stopped.
        public string Countdown => State switch
        {
            "booked" => "booked · no longer at risk",
            "cancelled" => "cancelled",
            _ when DaysLeft <= 1 => "cancels today",
            _ => $"cancels in {DaysLeft} days",
        };

        public bool Urgent => State is not ("booked" or "cancelled") && DaysLeft <= 3;

        public string Tag => Digital ? "Digital" : "Physical";

        // The application's stages from entry to the FDR, as the backend dates them.
        // A stage not reached yet is pending; one it does not go through says so.
        public List<Milestone> Timeline =>
            [.. Milestones.Select(m => new Milestone(m.Step,
                !m.Applies ? "Not applicable" : m.At?.ToString("dd/MM/yyyy") ?? "Pending",
                m.Applies && m.At is not null, m.Applies, m.Failed, m.Note))];
    }

    /// <summary>The applications the search reached, from the backend.</summary>
    public IReadOnlyList<ApplicationRow> Rows => rows ??= found.Applications.Select(ToRow).ToList();

    private List<ApplicationRow>? rows;

    // The backend dates each application; the page counts its days against the window.
    private ApplicationRow ToRow(ApplicationRecord r) => new(
        r.AppNo, r.Folio, r.Investor, r.Pan, r.Amount, r.Cumulative, r.Months, r.Payout, r.Holders,
        (Today - r.Applied.Date).Days, r.Digital, r.Instrument, r.State, r.Fdr, r.Step,
        WindowDays, r.Scheme, r.Milestones ?? []);

    // The list as the page shows it: newest first, with the applications that
    // still need someone at the top of each day.
    public IEnumerable<ApplicationRow> Listed =>
        Rows.OrderBy(r => r.DaysOld)
            .ThenBy(r => r.State switch { "progress" => 0, "awaiting" => 1, "review" => 2, _ => 3 });

    // The pay-in slip page writes rupees for the same applications, so the two
    // lists never disagree about an amount.
    public static string Rupees(long amount) => PayInSlipsViewModel.Rupees(amount);
}
