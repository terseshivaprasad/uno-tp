namespace UnoTP.Backend.Mock;

/// <summary>The brokers and staff sourcing codes are searched against.</summary>
public sealed class MockSourcing : ISourcingApi
{
    private static readonly Party[] Brokers =
    {
        new("BR10021", "Sahyadri Investment Services"),
        new("BR10874", "Deccan Wealth Advisors"),
        new("BR11250", "Konkan Financial Services"),
        new("BR11903", "Nagpur Capital Partners"),
        new("BR12388", "Godavari Securities"),
    };

    // The partner at the keyboard is on the staff register too: an employee-sourced
    // application opens with their own code in the field.
    private static readonly Party[] Staff =
    {
        new("100002225", "Shivaprasad Terse"),
        new("E10428", "Nikhil Ramesh Bhosale"),
        new("E20915", "Sneha Arun Kulkarni"),
        new("E31077", "Farhan Iqbal Shaikh"),
    };

    public Task<IReadOnlyList<Party>> BrokersAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Party>>(Brokers);

    public Task<IReadOnlyList<Party>> StaffAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Party>>(Staff);
}

/// <summary>The Axis CMS branches a cheque can be paid in at.</summary>
internal static class MockBranches
{
    public static readonly string[] Names = ["Mumbai — Andheri", "Mumbai — Fort", "Thane — Naupada", "Pune — Shivajinagar"];
}

/// <summary>
/// The window the lists are built around: an application that goes unpaid for this
/// long cancels itself.
/// </summary>
internal static class MockWindow
{
    public const int Days = 14;
}

public sealed class MockPayInSlips : IPayInSlipApi
{
    public Task<IReadOnlyList<SlipRecord>> SlipsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlipRecord>>(Build());

    // Two dozen applications paying by paper, every value derived from the row
    // number so the list is the same on every load. Twenty sit inside the window
    // and are what the page opens on; the last three are already past it and turn
    // up only under a search by application number.
    private static List<SlipRecord> Build()
    {
        const string first = "ADKMPRSVJB", last = "BDGKMNPRST";
        var banks = new[] { "HDFC Bank", "ICICI Bank", "State Bank of India", "Kotak Mahindra Bank", "Axis Bank" };
        var amounts = new long[] { 150_000, 300_000, 10_00_000, 200_000, 500_000, 750_000, 250_000, 125_000 };
        List<SlipRecord> rows = [];

        for (var i = 0; i < 23; i++)
        {
            // 13 and 14 share no factor, so the first twenty spread over every day
            // of the window; the last three are 16, 23 and 30 days old.
            var daysOld = i < 20 ? i * 13 % MockWindow.Days : 16 + (i - 20) * 7;

            // A slip follows the cheque: made a day or two after the application,
            // paid in at the branch a few days after that.
            var state = daysOld >= MockWindow.Days ? "lapsed"
                : daysOld <= 2 ? "pending"
                : daysOld <= 6 ? (i % 2 == 0 ? "generated" : "pending")
                : (i % 3 == 0 ? "generated" : "deposited");

            var made = state is "generated" or "deposited";

            // Two in three come in digitally. Acceptance always precedes the slip,
            // so an application that already has one necessarily has acceptance
            // too; among the rest it is the newest digital ones still waiting.
            var digital = i % 3 != 0;
            var accepted = !digital || made || daysOld > 4;

            rows.Add(new SlipRecord(
                $"FBBMFL26F{i * 6421 % 90000 + 10000:D5}",
                $"{first[i % first.Length]}•••• {last[i * 3 % last.Length]}•••••",
                amounts[i % amounts.Length],
                i % 5 == 0 ? "DD" : "Cheque",
                $"{i * 4139 % 900000 + 100000:D6}",
                banks[i % banks.Length],
                DateTime.Today.AddDays(-daysOld),
                MockBranches.Names[i % MockBranches.Names.Length],
                digital,
                accepted,
                state,
                made ? $"AXPIS{i * 317 % 9000 + 1000:D4}" : null));
        }

        return rows;
    }
}

public sealed class MockLinks : ILinkApi
{
    // How long a link stays valid, by what it asks for.
    private static readonly (string Key, int Hours)[] Purposes = [("payment", 48), ("acceptance", 72)];

    private static readonly string[] Live = ["open", "opened", "done"];

    // Two dozen links across the two purposes, every value derived from the row
    // number so the list is the same on every load, and a link's state follows its
    // clock: one whose validity has run out is expired, whatever else it was doing.
    public Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default)
    {
        const string first = "ADKMPRSVJB", last = "BDGKMNPRST";
        var now = DateTime.Now;
        List<SentLinkRecord> links = [];

        for (var i = 0; i < 24; i++)
        {
            var (purpose, hours) = Purposes[i % Purposes.Length];
            var sentHoursAgo = 3 + i * 7;
            var state = hours - sentHoursAgo <= 0 ? "expired" : Live[i % 3];

            var contact = i % 3 == 0
                ? $"{char.ToLowerInvariant(first[i % first.Length])}{char.ToLowerInvariant(last[i % last.Length])}••••@{(i % 2 == 0 ? "gmail.com" : "outlook.com")}"
                : $"{90 + i % 10}••••{1000 + i * 137}";

            // The application is a little older than the link raised against it.
            var appDays = 1 + (sentHoursAgo / 24) + i % 4;
            var sent = now.AddHours(-sentHoursAgo);

            links.Add(new SentLinkRecord(
                $"FBBMFL26F{(i * 7919 % 90000) + 10000:D5}",
                $"{first[i % first.Length]}•••• {last[(i * 3) % last.Length]}•••••",
                contact,
                purpose,
                sent,
                sent.AddHours(hours),
                state,
                DateTime.Today.AddDays(-appDays)));
        }

        return Task.FromResult<IReadOnlyList<SentLinkRecord>>(links);
    }

    // The in-flight applications, aged off their position in the list so the
    // window has something on either side of it. What each one is waiting for
    // decides which link it can carry.
    public Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PendingRecord>>(MockInFlight.Applications
            .Select((a, i) => new PendingRecord(
                a.AppNo,
                a.HolderMask,
                DateTime.Today.AddDays(-(1 + i * 3)),
                $"{90 + i % 10}••••{1000 + i * 137}",
                i % 2 == 0 ? "payment" : "acceptance"))
            .ToList());
}

/// <summary>The applications View Application lists.</summary>
internal static class MockApplicationList
{
    // Twenty-eight applications, every value derived from the row number so the
    // list is the same on every load. The first twenty fall inside the window and
    // are what the page opens on; the rest are older, already booked or cancelled,
    // and are reached only by searching for their number or their investor's folio.
    public static List<ApplicationRecord> Build()
    {
        const string first = "ADKMPRSVJB", last = "BDGKMNPRST";
        var amounts = new long[] { 150_000, 300_000, 10_00_000, 200_000, 500_000, 750_000, 250_000, 125_000 };
        var months = new[] { 12, 24, 36, 18, 60, 24 };
        var payouts = new[] { "On maturity", "Monthly", "Quarterly", "Half-yearly", "Yearly" };
        var steps = new[] { "Investor Information", "Upload Documents", "Bank Details & Payment", "FD Configuration" };
        List<ApplicationRecord> rows = [];

        for (var i = 0; i < 28; i++)
        {
            // 13 and 14 share no factor, so the first twenty spread over every day
            // of the window; the rest are 15 to 71 days old.
            var daysOld = i < 20 ? i * 13 % MockWindow.Days : 15 + (i - 20) * 8;

            // Inside the window an application is still moving: the newest are
            // unfinished, the older ones have gone out to the investor and on to
            // Operations. Outside it, it has either booked or cancelled itself.
            var state = daysOld >= MockWindow.Days
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

            rows.Add(new ApplicationRecord(
                $"FBBMFL26F{i * 8317 % 90000 + 10000:D5}",
                newCustomer ? null : $"MF{who * 4073 + 40000:D7}",
                $"{first[who % first.Length]}•••• {last[who * 3 % last.Length]}•••••",
                $"{first[who % first.Length]}{last[who % last.Length]}{first[(who + 4) % first.Length]}P{last[(who + 2) % last.Length]}••••{last[who * 7 % last.Length]}",
                amounts[i % amounts.Length],
                cumulative,
                months[i % months.Length],
                cumulative ? "On maturity" : payouts[1 + i % 4],
                1 + i % 3,
                DateTime.Today.AddDays(-daysOld),
                i % 3 != 0,
                i % 5 == 0 ? "DD" : i % 4 == 1 ? "Net banking" : "Cheque",
                MockBranches.Names[i % MockBranches.Names.Length],
                state,
                state == "booked" ? $"FD25{i * 4931 % 900000 + 100000:D6}" : null,
                steps[i % steps.Length]));
        }

        return rows;
    }
}
