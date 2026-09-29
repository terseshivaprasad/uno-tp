using System.Globalization;
using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>Pay In Slip Generation: the applications paying on paper, and their slips.</summary>
public class PayInSlipViewModel(IReadOnlyList<SlipRecord> slips, int windowDays)
{
    // The window the page works to. It is the Short URL page's rule seen from the
    // other side: an application that goes unpaid for this long cancels itself, so
    // a slip can only be made for one raised inside it. The list therefore opens on
    // the last fourteen days rather than on nothing.
    public int WindowDays { get; } = windowDays;

    public static DateTime Today => DateTime.Today;

    // The earliest application date a slip can still be made for.
    public DateTime Earliest => Today.AddDays(-(WindowDays - 1));

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
        public string Countdown => !InWindow
            ? $"cancelled after {WindowDays} days"
            : DaysLeft <= 1 ? "cancels today" : $"cancels in {DaysLeft} days";

        public bool Urgent => InWindow && DaysLeft <= 3;
    }

    /// <summary>Every application paying on paper, from the backend.</summary>
    public IReadOnlyList<SlipRow> Rows => rows ??= slips.Select(ToRow).ToList();

    private List<SlipRow>? rows;

    // The backend dates each application; the page counts its days against the window.
    private SlipRow ToRow(SlipRecord r) => new(
        r.AppNo, r.Investor, r.Amount, r.Instrument, r.InstrumentNo, r.DrawnOn,
        (Today - r.Applied.Date).Days, r.Branch, r.Digital, r.Accepted, r.State, r.SlipNo, r.AcceptedOn, WindowDays);

    // The list the page opens on: everything raised inside the window, newest
    // first, with the slips still to be made at the top of each day.
    public IEnumerable<SlipRow> Listed =>
        Rows.Where(r => r.InWindow)
            .OrderBy(r => r.DaysOld)
            // Within a day: the slips that can be made now, then the ones waiting
            // on an acceptance, then the ones already dealt with.
            .ThenBy(r => r.Blocked ? 1 : r.State == "pending" ? 0 : 2);

    // The cancelled applications, kept out of the list but still findable by
    // number so a search for one says what became of it.
    public IEnumerable<SlipRow> Lapsed => Rows.Where(r => !r.InWindow);

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
