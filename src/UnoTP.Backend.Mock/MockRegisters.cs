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
    // The slips issued while the app runs, by application: they stand over the
    // generated list, the way the backend's own records would.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Issued = new();
    private static int slipSeq = 5100;

    public Task<IReadOnlyList<SlipRecord>> SlipsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlipRecord>>(Build());

    // A slip is issued only for an application still inside the window, paid on
    // paper, and - for a digital one - accepted. A reprint issues a fresh number.
    public Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default)
    {
        var row = Build().FirstOrDefault(r => r.AppNo == appNo);
        if (row is null || row.State is not ("pending" or "generated") || row.Digital && !row.Accepted) return Task.FromResult<SlipRecord?>(null);
        var slip = $"AXPIS{Interlocked.Increment(ref slipSeq):D4}";
        Issued[appNo] = slip;
        return Task.FromResult<SlipRecord?>(row with { State = "generated", SlipNo = slip });
    }

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
            var appNo = $"FBBMFL26F{i * 6421 % 90000 + 10000:D5}";
            var issued = Issued.GetValueOrDefault(appNo);

            rows.Add(new SlipRecord(
                appNo,
                $"{first[i % first.Length]}•••• {last[i * 3 % last.Length]}•••••",
                amounts[i % amounts.Length],
                i % 5 == 0 ? "DD" : "Cheque",
                $"{i * 4139 % 900000 + 100000:D6}",
                banks[i % banks.Length],
                DateTime.Today.AddDays(-daysOld),
                MockBranches.Names[i % MockBranches.Names.Length],
                digital,
                accepted,
                issued is not null ? "generated" : state,
                issued ?? (made ? $"AXPIS{i * 317 % 9000 + 1000:D4}" : null),
                // A digital application is accepted the day after it is raised.
                digital && accepted ? DateTime.Today.AddDays(-daysOld + 1) : null));
        }

        return rows;
    }
}

public sealed class MockLinks : ILinkApi
{
    // The links sent while the app runs, by application and purpose: each stands
    // over the generated list, and the one before it stops working.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string AppNo, string Purpose), DateTime> Sent = new();

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

            var mobile = $"{90 + i % 10}••••{1000 + i * 137}";
            var email = $"{char.ToLowerInvariant(first[i % first.Length])}{char.ToLowerInvariant(last[i % last.Length])}••••@{(i % 2 == 0 ? "gmail.com" : "outlook.com")}";

            // The application is a little older than the link raised against it.
            var appDays = 1 + (sentHoursAgo / 24) + i % 4;
            var sent = now.AddHours(-sentHoursAgo);

            links.Add(new SentLinkRecord(
                $"FBBMFL26F{(i * 7919 % 90000) + 10000:D5}",
                $"{first[i % first.Length]}•••• {last[(i * 3) % last.Length]}•••••",
                mobile,
                email,
                purpose,
                sent,
                sent.AddHours(hours),
                state,
                DateTime.Today.AddDays(-appDays)));
        }

        // What was sent here since stands over what the list says.
        var pending = PendingAsync().Result;
        foreach (var ((appNo, purpose), at) in Sent)
        {
            var hours = Purposes.First(p => p.Key == purpose).Hours;
            var i = links.FindIndex(l => l.AppNo == appNo);
            if (i >= 0) links[i] = links[i] with { Purpose = purpose, SentAt = at, ExpiresAt = at.AddHours(hours), State = "open" };
            else if (pending.FirstOrDefault(p => p.AppNo == appNo) is { } p)
                links.Add(new SentLinkRecord(appNo, p.Investor, p.Mobile, p.Email, purpose, at, at.AddHours(hours), "open", p.Applied));
            else if (AwaitingAcceptance(appNo) is { } slip)
                links.Add(new SentLinkRecord(appNo, slip.Investor, $"98••••{appNo[^4..]}", $"{slip.Investor[..1].ToLowerInvariant()}••••@gmail.com", purpose, at, at.AddHours(hours), "open", slip.Applied));
        }
        return Task.FromResult<IReadOnlyList<SentLinkRecord>>(links);
    }

    // A digital application paying on paper that the investor has still to accept.
    private static SlipRecord? AwaitingAcceptance(string appNo) =>
        new MockPayInSlips().SlipsAsync().Result.FirstOrDefault(s => s.AppNo == appNo && s.Digital && !s.Accepted);

    // A link goes out only for a purpose there is one for, against an application
    // that is waiting on the investor or already carries a link.
    public async Task<SentLinkRecord?> SendAsync(string appNo, string purpose, CancellationToken ct = default)
    {
        if (Purposes.All(p => p.Key != purpose)) return null;
        var known = (await PendingAsync(ct)).Any(p => p.AppNo == appNo) || (await SentAsync(ct)).Any(l => l.AppNo == appNo)
            || purpose == "acceptance" && AwaitingAcceptance(appNo) is not null;
        if (!known) return null;
        Sent[(appNo, purpose)] = DateTime.Now;
        return (await SentAsync(ct)).FirstOrDefault(l => l.AppNo == appNo);
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
                i % 2 == 0 ? "payment" : "acceptance",
                $"{a.HolderMask[..1].ToLowerInvariant()}••••@{(i % 2 == 0 ? "gmail.com" : "yahoo.co.in")}"))
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
                steps[i % steps.Length],
                "Samruddhi",
                Milestones(state, i % 3 != 0, DateTime.Today.AddDays(-daysOld))));
        }

        return rows;
    }

    // How far an application has come, step by step, each dated as it was
    // reached. A cancelled application ends on its cancellation.
    private static List<MilestoneRecord> Milestones(string state, bool digital, DateTime applied)
    {
        var reached = state switch
        {
            "progress" => 2,
            "awaiting" => digital ? 3 : 4,
            "review" => 5,
            "booked" => 6,
            _ => 3,
        };
        List<MilestoneRecord> steps = [];
        void Step(int n, string label, int day) => steps.Add(new MilestoneRecord(label, reached >= n ? applied.AddDays(day) : null));
        Step(1, "Application raised", 0);
        Step(2, "Documents uploaded", 0);
        Step(3, "Submitted for verification", 1);
        Step(4, digital ? "Investor accepted the deposit" : "Signed application received", 2);
        Step(5, "Payment received", 3);
        Step(6, "Booked · FDR issued", 4);
        if (state == "cancelled") steps.Add(new MilestoneRecord($"Cancelled · unpaid for {MockWindow.Days} days", applied.AddDays(MockWindow.Days)));
        return steps;
    }
}
