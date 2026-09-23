using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ClassicViewApplicationModel : PageModel
{
    // The old ViewApplication screen asked for an application number before it
    // would show anything, and had no way to reach an investor's other
    // applications at all. This one opens on the last fourteen days - every
    // application still in flight, because one that goes unpaid for that long
    // cancels itself - and searches by application number or by folio.
    public const int WindowDays = ClassicShortUrlModel.EligibleDays;

    public static DateTime Today => DateTime.Today;

    // The oldest application day the list opens on.
    public static DateTime Earliest => Today.AddDays(-(WindowDays - 1));

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
        string Step)
    {
        public DateTime Applied => Today.AddDays(-DaysOld);

        public bool InWindow => DaysOld < WindowDays;

        public int DaysLeft => WindowDays - DaysOld;

        public AppState Status => States.First(s => s.Key == State);

        public string Scheme => $"Samruddhi · {(Cumulative ? "cumulative" : "non-cumulative")}";

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

        // How far down the six milestones the application has come. Everything
        // the dialog shows about its progress follows from this one number.
        private int Reached => State switch
        {
            "progress" => 2,
            "awaiting" => Digital ? 3 : 4,
            "review" => 5,
            "booked" => 6,
            _ => 3,
        };

        // The application's own history, as far as it goes. A step not yet
        // reached carries no date, and a cancelled application ends on the
        // cancellation rather than on a step it never took.
        public List<Milestone> Timeline
        {
            get
            {
                var reached = Reached;
                var steps = new List<Milestone>();

                void Step(int n, string label, int dayOffset) =>
                    steps.Add(new Milestone(
                        label,
                        reached >= n ? Applied.AddDays(dayOffset).ToString("dd/MM/yyyy") : "pending",
                        reached >= n));

                Step(1, "Application raised", 0);
                Step(2, "Documents uploaded", 0);
                Step(3, "Submitted for verification", 1);
                Step(4, Digital ? "Investor accepted the deposit" : "Signed application received", 2);
                Step(5, "Payment received", 3);
                Step(6, "Booked · FDR issued", 4);

                if (State == "cancelled")
                {
                    steps.Add(new Milestone(
                        $"Cancelled · unpaid for {WindowDays} days",
                        Applied.AddDays(WindowDays).ToString("dd/MM/yyyy"),
                        true));
                }

                return steps;
            }
        }
    }

    public static readonly ApplicationRow[] Rows = Build();

    // Twenty-eight applications, every value derived from the row number so the
    // list is the same on every load. The first twenty fall inside the window and
    // are what the page opens on; the rest are older, already booked or cancelled,
    // and are reached only by searching for their number or their investor's folio.
    private static ApplicationRow[] Build()
    {
        const string first = "ADKMPRSVJB", last = "BDGKMNPRST";
        var amounts = new long[] { 150_000, 300_000, 10_00_000, 200_000, 500_000, 750_000, 250_000, 125_000 };
        var months = new[] { 12, 24, 36, 18, 60, 24 };
        var payouts = new[] { "On maturity", "Monthly", "Quarterly", "Half-yearly", "Yearly" };
        var steps = new[] { "Investor Information", "Upload Documents", "Bank Details & Payment", "FD Configuration" };
        var rows = new List<ApplicationRow>();

        for (var i = 0; i < 28; i++)
        {
            // 13 and 14 share no factor, so the first twenty spread over every day
            // of the window; the rest are 15 to 71 days old.
            var daysOld = i < 20 ? i * 13 % WindowDays : 15 + (i - 20) * 8;

            // Inside the window an application is still moving: the newest are
            // unfinished, the older ones have gone out to the investor and on to
            // Operations. Outside it, it has either booked or cancelled itself.
            var state = daysOld >= WindowDays
                ? (i % 4 == 0 ? "cancelled" : "booked")
                : daysOld <= 2 ? "progress"
                : daysOld <= 7 ? (i % 3 == 0 ? "progress" : "awaiting")
                : i % 4 == 0 ? "booked" : "review";

            var cumulative = i % 3 != 0;

            // A new customer has no folio until the deposit books.
            var newCustomer = i % 5 == 2 && state != "booked";

            // Eleven folios over twenty-eight applications, so a folio search finds
            // an investor's other applications beside the one that was searched for.
            // Name and PAN follow the folio rather than the row, so everything one
            // folio returns is plainly the same person's.
            var who = newCustomer ? 11 + i : i % 11;

            rows.Add(new ApplicationRow(
                $"FBBMFL26F{i * 8317 % 90000 + 10000:D5}",
                newCustomer ? null : $"MF{who * 4073 + 40000:D7}",
                $"{first[who % first.Length]}•••• {last[who * 3 % last.Length]}•••••",
                $"{first[who % first.Length]}{last[who % last.Length]}{first[(who + 4) % first.Length]}P{last[(who + 2) % last.Length]}••••{last[who * 7 % last.Length]}",
                amounts[i % amounts.Length],
                cumulative,
                months[i % months.Length],
                cumulative ? "On maturity" : payouts[1 + i % 4],
                1 + i % 3,
                daysOld,
                i % 3 != 0,
                i % 5 == 0 ? "DD" : i % 4 == 1 ? "Net banking" : "Cheque",
                ClassicPayInSlipModel.Branches[i % ClassicPayInSlipModel.Branches.Length].Name,
                state,
                state == "booked" ? $"FD25{i * 4931 % 900000 + 100000:D6}" : null,
                steps[i % steps.Length]));
        }

        return rows.ToArray();
    }

    // The list the page opens on: everything raised inside the window, newest
    // first, with the applications that still need someone at the top of each day.
    public static IEnumerable<ApplicationRow> Listed =>
        Rows.Where(r => r.InWindow)
            .OrderBy(r => r.DaysOld)
            .ThenBy(r => r.State switch { "progress" => 0, "awaiting" => 1, "review" => 2, _ => 3 });

    // The older applications, kept out of the opening list but still findable by
    // application number or folio, so a search for one says what became of it.
    public static IEnumerable<ApplicationRow> Older => Rows.Where(r => !r.InWindow);

    // The pay-in slip page writes rupees for the same applications, so the two
    // lists never disagree about an amount.
    public static string Rupees(long amount) => ClassicPayInSlipModel.Rupees(amount);

    public void OnGet()
    {
    }
}
