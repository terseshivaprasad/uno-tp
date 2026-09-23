using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ClassicShortUrlModel : PageModel
{
    // What the investor is asked to do once the link opens. The purpose decides
    // how long the link stays valid, so the two travel together.
    public record LinkPurpose(string Key, string Label, string Detail, string Validity);

    public static readonly LinkPurpose[] Purposes =
    {
        new("payment", "Pay for the FD", "The payment link for an application awaiting money", "48 hours"),
        new("acceptance", "Accept the FD", "The investor confirms the deposit's terms and the FDR", "72 hours"),
    };

    // Where a link got to. The key drives the filter; the label and tone are what
    // the row shows.
    public record LinkState(string Key, string Label, string Text, string Tone);

    public static readonly LinkState[] States =
    {
        new("none", "No link", "No link sent", "text-muted"),
        new("open", "Not opened", "Not opened", "text-muted"),
        new("opened", "Opened", "Opened · not completed", "text-amber"),
        new("done", "Completed", "Completed", "text-success"),
        new("expired", "Expired", "Expired · resend to reopen", "text-danger"),
    };

    // A short link is one row: who it went to, what it asks for, and where it got to.
    public record SentLink(string AppNo, string Investor, string Contact, string Purpose, string Sent, string Expires, string State, string Status, string Tone, int AppDays)
    {
        public int DaysLeft => EligibleDays - AppDays;

        // What the row says under the application number: a booked one is safe,
        // the rest are counting down to the automatic cancellation.
        public string Countdown => State == "done"
            ? "booked \u00b7 no longer at risk"
            : DaysLeft <= 1 ? "auto-cancels today" : $"auto-cancels in {DaysLeft} days";

        public bool Urgent => State != "done" && DaysLeft <= 3;
    }

    public static readonly SentLink[] Sent = BuildSent();

    // Two dozen links across the two purposes, so the filter and the pages have
    // something to work with. Every value is derived from the row number, so the
    // list is the same on every load, and a link's state follows its clock: one
    // whose validity has run out is expired, whatever else it was doing. The link
    // itself is never held here - it goes to the investor and nowhere else.
    private static SentLink[] BuildSent()
    {
        const string first = "ADKMPRSVJB", last = "BDGKMNPRST";
        var links = new List<SentLink>();

        for (var i = 0; i < 24; i++)
        {
            var purpose = Purposes[i % Purposes.Length];
            var sentHoursAgo = 3 + i * 7;
            var left = Hours(purpose.Validity) - sentHoursAgo;
            var state = left <= 0 ? States[4] : States[1 + i % 3];

            var contact = i % 3 == 0
                ? $"{char.ToLowerInvariant(first[i % first.Length])}{char.ToLowerInvariant(last[i % last.Length])}\u2022\u2022\u2022\u2022@{(i % 2 == 0 ? "gmail.com" : "outlook.com")}"
                : $"{90 + i % 10}\u2022\u2022\u2022\u2022{1000 + i * 137}";

            var expires = state.Key switch
            {
                "expired" => "expired",
                // A completed link closes the moment it is used.
                "done" => "closed on use",
                _ => Left(left),
            };

            // The application is a little older than the link raised against it.
            var appDays = 1 + (sentHoursAgo / 24) + i % 4;

            links.Add(new SentLink(
                $"FBBMFL26F{(i * 7919 % 90000) + 10000:D5}",
                $"{first[i % first.Length]}\u2022\u2022\u2022\u2022 {last[(i * 3) % last.Length]}\u2022\u2022\u2022\u2022\u2022",
                contact,
                purpose.Label,
                Ago(sentHoursAgo),
                expires,
                state.Key,
                state.Key == "done" ? (purpose.Key == "payment" ? "Paid" : "Accepted") : state.Text,
                state.Tone,
                appDays));
        }

        return links.ToArray();
    }

    private static int Hours(string validity) =>
        validity.EndsWith("days") ? int.Parse(validity.Split(' ')[0]) * 24 : int.Parse(validity.Split(' ')[0]);

    private static string Ago(int hours) => hours switch
    {
        < 24 => $"{hours} hours ago",
        < 48 => "yesterday",
        _ => $"{hours / 24} days ago",
    };

    private static string Left(int hours) => hours switch
    {
        < 24 => $"in {hours} hours",
        < 48 => "in a day",
        _ => $"in {hours / 24} days",
    };

    // A link can only be raised against an application that is still pending and
    // was started within the eligibility window; anything older needs a fresh
    // application, so it never reaches the picker.
    // An application that goes unpaid for this long is cancelled by itself, so it
    // can neither carry a link nor stay in the list.
    public const int EligibleDays = 14;

    // A pending application, with the contacts the link would go to. The contacts
    // come from the application itself, masked: the partner never types them and
    // never sees them in full.
    public record PendingApplication(string AppNo, string Investor, string Stage, string Amount, int DaysOld, string Mobile, string Email, string Due)
    {
        public bool Eligible => DaysOld <= EligibleDays;

        public string Awaiting => Due == "payment" ? "Awaiting payment" : "Awaiting acceptance";
    }

    // The console's in-flight applications, aged off their position in the list so
    // the window has something on either side of it. What each one is waiting for
    // decides which link it can carry.
    public static readonly PendingApplication[] Pending =
        MockData.InFlightApplications
            .Select((a, i) => new PendingApplication(
                a.AppNo,
                a.HolderMask,
                a.Step,
                a.Amount,
                1 + i * 3,
                $"{90 + i % 10}\u2022\u2022\u2022\u2022{1000 + i * 137}",
                $"{char.ToLowerInvariant(a.HolderMask[0])}{(char)('a' + i % 26)}\u2022\u2022\u2022\u2022@{(i % 2 == 0 ? "gmail.com" : "outlook.com")}",
                i % 2 == 0 ? "payment" : "acceptance"))
            .ToArray();

    public static IEnumerable<PendingApplication> EligibleApplications => Pending.Where(p => p.Eligible);

    // The list the page shows: every eligible application still without a link,
    // then the links already sent.
    public static IEnumerable<SentLink> Rows =>
        EligibleApplications
            .Where(a => !Sent.Any(s => s.AppNo == a.AppNo))
            .Select(a => new SentLink(
                a.AppNo, a.Investor, a.Mobile,
                a.Due == "payment" ? Purposes[0].Label : Purposes[1].Label,
                "\u2014", "\u2014", "none", "No link sent", "text-muted", a.DaysOld))
            .Concat(Sent.Where(s => s.AppDays <= EligibleDays))
            .OrderBy(r => r.State == "done")
            .ThenByDescending(r => r.DaysLeft);

    public void OnGet()
    {
    }
}
