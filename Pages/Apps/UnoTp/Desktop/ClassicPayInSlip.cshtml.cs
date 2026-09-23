using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ClassicPayInSlipModel : PageModel
{
    // The window the page works to. It is the Short URL page's rule seen from the
    // other side: an application that goes unpaid for this long cancels itself, so
    // a slip can only be made for one raised inside it. The list therefore opens on
    // the last fourteen days rather than on nothing.
    public const int WindowDays = ClassicShortUrlModel.EligibleDays;

    public static DateTime Today => DateTime.Today;

    // The earliest application date a slip can still be made for.
    public static DateTime Earliest => Today.AddDays(-(WindowDays - 1));

    // The Axis branches a cheque can be paid in at. The same four the Bank Details
    // step offers as the Axis CMS Location. The branch is not chosen here: it was
    // chosen on the application, so every row carries its own and the slip is
    // printed for that one.
    public record CmsBranch(string Code, string Name);

    public static readonly CmsBranch[] Branches =
    {
        new("AXCMS0012", "Mumbai — Andheri"),
        new("AXCMS0034", "Mumbai — Fort"),
        new("AXCMS0108", "Thane — Naupada"),
        new("AXCMS0221", "Pune — Shivajinagar"),
    };

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
        string? SlipNo)
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
            : Accepted ? $"accepted {Applied.AddDays(1):dd/MM}" : "acceptance pending";

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

        // The second line of the Slip cell: its number and the branch it names.
        public string? SlipDetail => SlipNo is null ? null : $"{SlipNo} · {Branch}";
    }

    public static readonly SlipRow[] Rows = Build();

    // Two dozen applications paying by paper, every value derived from the row
    // number so the list is the same on every load. Twenty sit inside the window
    // and are what the page opens on; the last three are already past it and turn
    // up only under a search by application number.
    private static SlipRow[] Build()
    {
        const string first = "ADKMPRSVJB", last = "BDGKMNPRST";
        var banks = new[] { "HDFC Bank", "ICICI Bank", "State Bank of India", "Kotak Mahindra Bank", "Axis Bank" };
        var amounts = new long[] { 150_000, 300_000, 10_00_000, 200_000, 500_000, 750_000, 250_000, 125_000 };
        var rows = new List<SlipRow>();

        for (var i = 0; i < 23; i++)
        {
            // 13 and 14 share no factor, so the first twenty spread over every day
            // of the window; the last three are 16, 23 and 30 days old.
            var daysOld = i < 20 ? i * 13 % WindowDays : 16 + (i - 20) * 7;

            // A slip follows the cheque: made a day or two after the application,
            // paid in at the branch a few days after that.
            var state = daysOld >= WindowDays ? "lapsed"
                : daysOld <= 2 ? "pending"
                : daysOld <= 6 ? (i % 2 == 0 ? "generated" : "pending")
                : (i % 3 == 0 ? "generated" : "deposited");

            var made = state is "generated" or "deposited";

            // Two in three come in digitally. Acceptance always precedes the slip,
            // so an application that already has one necessarily has acceptance
            // too; among the rest it is the newest digital ones still waiting.
            var digital = i % 3 != 0;
            var accepted = !digital || made || daysOld > 4;

            rows.Add(new SlipRow(
                $"FBBMFL26F{i * 6421 % 90000 + 10000:D5}",
                $"{first[i % first.Length]}•••• {last[i * 3 % last.Length]}•••••",
                amounts[i % amounts.Length],
                i % 5 == 0 ? "DD" : "Cheque",
                $"{i * 4139 % 900000 + 100000:D6}",
                banks[i % banks.Length],
                daysOld,
                Branches[i % Branches.Length].Name,
                digital,
                accepted,
                state,
                made ? $"AXPIS{i * 317 % 9000 + 1000:D4}" : null));
        }

        return rows.ToArray();
    }

    // The list the page opens on: everything raised inside the window, newest
    // first, with the slips still to be made at the top of each day.
    public static IEnumerable<SlipRow> Listed =>
        Rows.Where(r => r.InWindow)
            .OrderBy(r => r.DaysOld)
            // Within a day: the slips that can be made now, then the ones waiting
            // on an acceptance, then the ones already dealt with.
            .ThenBy(r => r.Blocked ? 1 : r.State == "pending" ? 0 : 2);

    // The cancelled applications, kept out of the list but still findable by
    // number so a search for one says what became of it.
    public static IEnumerable<SlipRow> Lapsed => Rows.Where(r => !r.InWindow);

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

    public void OnGet()
    {
    }
}
