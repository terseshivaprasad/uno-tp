using System.Globalization;
using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>Pay In Slip Generation: the applications paying on paper a search reaches, and their slips.</summary>
public class PayInSlipsViewModel(SlipsFound found, int windowDays, ApplicationSearch search)
{
    // The window the page works to. It is the Short URL page's rule seen from the
    // other side: an application that goes unpaid for this long cancels itself, so
    // a slip can only be made for one raised inside it. The list therefore opens on
    // the last fourteen days rather than on nothing.
    // The search is run by the database: the page holds what it reached, never
    // the partner's whole list.
    public int WindowDays { get; } = windowDays;

    public static DateTime Today => DateTime.Today;

    // The earliest application date a slip can still be made for.
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
            if (Search.By == ApplicationSearch.ByNumber) return $"Showing every application matching “{Search.Text}”, inside the window or cancelled.";
            if (IsOpeningList) return $"Showing the last {WindowDays} days — {From:dd/MM/yyyy} to {To:dd/MM/yyyy}.";
            return $"Showing applications raised {From:dd/MM/yyyy} to {To:dd/MM/yyyy}.";
        }
    }

    // Where the slip for an application has got to. A lapsed application has no
    // slip and never will; it is listed only when someone searches for it by
    // number, to say why it is not in the list.
    public record SlipState(string Key, string Label, string Text, string Tone);

    public static readonly SlipState[] States =
    {
        new("pending", "Not generated", "Not generated", "text-amber"),
        new("generated", "Generated", "Generated", "text-muted"),
        new("deposited", "Paid in", "Paid in", "text-success"),
        new("lapsed", "Cancelled", "Application cancelled", "text-danger"),
    };

    // One application on the list: the money it is waiting for, the instrument that
    // will bring it, and the slip that instrument is paid in with.
    public record SlipRow(
        string AppNo,
        string Investor,
        long Amount,
        string Instrument,
        string InstrumentNo,
        string DrawnOn,
        int DaysOld,
        string Branch,
        // How the investor signed up to the deposit. A physical application comes
        // in on paper, already signed; a digital one is accepted online, and until
        // that acceptance is in there is nothing to pay the money in against.
        bool Digital,
        bool Accepted,
        string State,
        string? SlipNo,
        DateTime? AcceptedOn,
        int WindowDays)
    {
        public DateTime Applied => Today.AddDays(-DaysOld);

        public int DaysLeft => WindowDays - DaysOld;

        public bool InWindow => DaysOld < WindowDays;


        public SlipState Status => States.First(s => s.Key == State);

        // The one rule this screen enforces: a digital application cannot have a
        // slip until the investor has accepted the deposit.
        public bool Blocked => Digital && !Accepted;

        public string Tag => Digital ? "Digital" : "Physical";

        // The line under the tag: what the acceptance is doing, or that a physical
        // application has nothing to wait for.
        public string TagNote => !Digital
            ? "signed form on file"
            : AcceptedOn is { } on ? $"accepted {on:dd/MM}" : Accepted ? "accepted" : "acceptance pending";

        // A blocked row says so where its slip would be, so the reason it cannot
        // be ticked is next to the tick that is missing.
        public string SlipLabel => Blocked ? "Blocked" : Status.Text;

        public string SlipTone => Blocked ? "text-danger" : Status.Tone;

        public string? SlipNote => Blocked ? "needs customer acceptance" : SlipNo;

        // What the Applied cell says underneath the date: how long the application
        // has before it cancels itself, or that it already has.
        public string Countdown => State == "lapsed"
            ? (InWindow ? "cancelled" : $"cancelled after {WindowDays} days")
            : DaysLeft <= 1 ? "cancels today" : $"cancels in {DaysLeft} days";

        public bool Urgent => State != "lapsed" && InWindow && DaysLeft <= 3;
    }

    /// <summary>The applications paying on paper the search reached, from the backend.</summary>
    public IReadOnlyList<SlipRow> Rows => rows ??= found.Slips.Select(ToRow).ToList();

    private List<SlipRow>? rows;

    // The backend dates each application; the page counts its days against the window.
    private SlipRow ToRow(SlipRecord r) => new(
        r.AppNo, r.Investor, r.Amount, r.Instrument, r.InstrumentNo, r.DrawnOn,
        (Today - r.Applied.Date).Days, r.Branch, r.Digital, r.Accepted, r.State, r.SlipNo, r.AcceptedOn, WindowDays);

    // The list as the page shows it: newest first, with the slips still to be made
    // at the top of each day. A search by dates only reaches applications inside
    // the window; a cancelled one is reached by its number, to say what became of it.
    public IEnumerable<SlipRow> Listed =>
        Rows.OrderBy(r => r.DaysOld)
            // Within a day: the slips that can be made now, then the ones waiting
            // on an acceptance, then the ones already dealt with.
            .ThenBy(r => r.Blocked ? 1 : r.State == "pending" ? 0 : 2);

    // Rupees the way the rest of the console writes them: lakh grouping, with the
    // symbol and a space in front. A custom format string cannot vary its group
    // sizes - .NET takes them all from the group nearest the decimal - so the
    // sizes are given on the format itself.
    private static readonly NumberFormatInfo Lakh = new()
    {
        NumberGroupSizes = new[] { 3, 2 },
        NumberDecimalDigits = 0,
    };

    public static string Rupees(long amount) => "₹ " + amount.ToString("N", Lakh);
}
