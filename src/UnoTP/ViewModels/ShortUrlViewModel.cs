using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>Short URL: the links sent to investors, and the applications that can carry one.</summary>
public class ShortUrlViewModel(IReadOnlyList<SentLinkRecord> sent, IReadOnlyList<PendingRecord> pending, AppConfig config)
{
    // What the investor is asked to do once the link opens. The purpose decides
    // how long the link stays valid, so the two travel together.
    public record LinkPurpose(string Key, string Label, string Detail, string Validity);

    // How long each stays open is the backend's rule (GET config).
    public IReadOnlyList<LinkPurpose> Purposes { get; } =
    [
        new("payment", "Pay for the FD", "The payment link for an application awaiting money", $"{config.LinkValidityHours.GetValueOrDefault("payment")} hours"),
        new("acceptance", "Accept the FD", "The investor confirms the deposit's terms and the FDR", $"{config.LinkValidityHours.GetValueOrDefault("acceptance")} hours"),
    ];

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
    public record SentLink(string AppNo, string Investor, string Contact, string Purpose, string Sent, string Expires, string State, string Status, string Tone, int AppDays, int EligibleDays, string PurposeKey)
    {
        public int DaysLeft => EligibleDays - AppDays;

        // What the row says under the application number: a booked one is safe,
        // the rest are counting down to the automatic cancellation.
        public string Countdown => State == "done"
            ? "booked \u00b7 no longer at risk"
            : DaysLeft <= 1 ? "auto-cancels today" : $"auto-cancels in {DaysLeft} days";

        public bool Urgent => State != "done" && DaysLeft <= 3;
    }

    // A link as the backend reports it, written the way the row says it. The link
    // itself is never held here - it goes to the investor and nowhere else.
    private SentLink ToRow(SentLinkRecord l)
    {
        var purpose = Purposes.FirstOrDefault(p => p.Key == l.Purpose) ?? Purposes[0];
        var state = States.FirstOrDefault(s => s.Key == l.State) ?? States[1];
        var now = DateTime.Now;
        var expires = state.Key switch
        {
            "expired" => "expired",
            // A completed link closes the moment it is used.
            "done" => "closed on use",
            _ => Left((int)Math.Round((l.ExpiresAt - now).TotalHours)),
        };
        return new SentLink(
            l.AppNo, l.Investor, l.Contact, purpose.Label,
            Ago((int)Math.Round((now - l.SentAt).TotalHours)), expires, state.Key,
            state.Key == "done" ? (purpose.Key == "payment" ? "Paid" : "Accepted") : state.Text,
            state.Tone, (Today - l.Applied.Date).Days, EligibleDays, purpose.Key);
    }

    private static DateTime Today => DateTime.Today;

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
    public int EligibleDays { get; } = config.CancellationDays;

    // A pending application, with the mobile the link would go to. It comes from
    // the application itself, masked: the partner never types it and never sees
    // it in full.
    public record PendingApplication(string AppNo, string Investor, int DaysOld, string Mobile, string Due);

    /// <summary>The applications waiting on the investor that can still carry a link.</summary>
    public IReadOnlyList<PendingApplication> EligibleApplications { get; } = pending
        .Select(p => new PendingApplication(p.AppNo, p.Investor, (Today - p.Applied.Date).Days, p.Mobile, p.Due))
        .Where(p => p.DaysOld <= config.CancellationDays)
        .ToList();

    /// <summary>The links already sent, from the backend.</summary>
    public IReadOnlyList<SentLink> Sent => sentRows ??= sent.Select(ToRow).ToList();

    private List<SentLink>? sentRows;

    // The list the page shows: every eligible application still without a link,
    // then the links already sent.
    public IEnumerable<SentLink> Rows =>
        EligibleApplications
            .Where(a => !Sent.Any(s => s.AppNo == a.AppNo))
            .Select(a => new SentLink(
                a.AppNo, a.Investor, a.Mobile,
                a.Due == "payment" ? Purposes[0].Label : Purposes[1].Label,
                "\u2014", "\u2014", "none", "No link sent", "text-muted", a.DaysOld, EligibleDays, a.Due == "payment" ? Purposes[0].Key : Purposes[1].Key))
            .Concat(Sent.Where(s => s.AppDays <= EligibleDays))
            .OrderBy(r => r.State == "done")
            .ThenByDescending(r => r.DaysLeft);
}
