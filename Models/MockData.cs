namespace UnoTp.Models;

// Icon is the key the _AppIcon partial switches on; MobileDescription is the
// shorter label Board 00 - Mobile uses on the two-up tiles, where the desktop
// description would wrap to three lines.
public record PinnedApp(string Title, string Description, bool Pinned, string Icon, int? Badge = null, string? MobileDescription = null);

public record AppTile(string Title, string Description, string Icon, int? Badge = null, bool Disabled = false, string? MobileDescription = null);

public record InFlightApplication(
    string AppNo,
    string HolderMask,
    string Status,
    string StatusDetail,
    // The condensed detail Board 00 - Mobile shows in the single-line list.
    string ShortDetail,
    string StatusClass,
    string Amount,
    string Step,
    string Waiting,
    string WaitingClass,
    string Action,
    string StatusKey
);

public record NeedsAttentionItem(
    string AppNo,
    string Holder,
    string HolderTag,
    string Stage,
    string Blocker,
    string BlockerClass,
    string Waiting,
    string Action,
    // The mobile board shortens the action to a single word so it fits beside
    // the blocker sentence on a 390px row.
    string ShortAction,
    // A row that is parked with Operations shows its state in place of an
    // action, in muted grey rather than as a red link.
    bool Actionable = true
);

public record InFlightSummary(int Count, string Label, string Detail);

public record QuickLink(string Title, string Detail, string? PageLink = null);

public record ChecklistItem(int Step, string Text);

public enum ConsentSource { Digital, Offline }

// Each record sits in exactly one bucket; the tracker's KPI tiles and its
// "Consent status" filter are both counted from these.
public enum ConsentBucket { OnFile, AwaitingAcceptance, UploadPending, ExpiringSoon, DeclinedOrExpired }

// The colour a status line is written in. Plain is the regular-weight grey the
// board uses for a CERSAI line with nothing to report yet.
public enum ConsentTone { Blue, Green, Amber, Red, Plain }

public record ConsentRecord(
    string Holder,
    string Pan,
    // "holder 3" for a new customer, "folio MF…" for an existing one.
    string Reference,
    // Null when the consent was raised outside an FD application.
    string? AppNo,
    ConsentSource Source,
    ConsentBucket Bucket,
    string Dpdp,
    string DpdpDetail,
    ConsentTone DpdpTone,
    string Cersai,
    string CersaiDetail,
    ConsentTone CersaiTone,
    string? ValidTill,
    string Action,
    int RequestedDaysAgo
);

public static class MockData
{
    public const string ApplicationNo = "FBBMFL26F99AAC1";
    public const string BrokerName = "Shivaprasad Terse";
    public const string EntityCode = "100002225";

    public static List<InFlightApplication> InFlightApplications { get; } = new()
    {
        new("FBBMFL26F71CD4", "D•••• K•••• +1", "Consent pending", "holder 2 has not completed OTP · re-send or switch to offline", "holder 2 has not completed OTP", "text-danger", "₹ 1,50,000", "Consent · 06", "3 d", "text-danger", "Open", "needs-you"),
        new("FBBMFL26F03BBD2", "A•••• D•••••• +1", "Payment link unpaid", "expires tomorrow 5:04 PM · one resend left", "expires tomorrow 5:04 PM", "text-danger", "₹ 3,00,000", "Submitted · 11B", "19 h", "text-danger", "Open", "needs-you"),
        new("FBBMFL26F55QP9", "M•••• S•••", "Re-upload requested", "holder 1’s PAN card illegible · Operations asked for a fresh scan", "holder 1’s PAN illegible", "text-danger", "₹ 10,00,000", "Documents · 07", "6 h", "text-danger", "Open", "needs-you"),
        new("FBBMFL26F62LK3", "V•••• J•••• +2", "Incomplete", "7 investor fields blank across two holders · nominee not captured", "7 fields blank · nominee missing", "text-amber", "₹ 2,00,000", "Investor info · 08", "2 d", "text-muted", "Open", "needs-you"),
        new("FBBMFL26F48TR1", "S•••• B••••", "Incomplete", "bank details not started · cheque uploaded but unread", "bank details not started", "text-amber", "₹ 5,00,000", "Bank · 09", "1 d", "text-muted", "Open", "needs-you"),
        new("FBBMFL26F99AAC1", "R•••••• T••••• +2", "Under review", "holder 3’s Aadhaar and the cheque date with Operations", "Aadhaar and cheque date with Operations", "text-amber", "₹ 5,00,000", "Verification", "1 h", "text-muted", "View", "operations"),
        new("FBBMFL26E88XZ7", "K•••• P••••", "Verified", "awaiting cheque realisation · books on realisation", "awaiting cheque realisation", "text-success", "₹ 7,50,000", "Realisation", "1 d", "text-muted", "View", "realisation"),
    };

    // Board 00 - Mobile leads the "Needs you" list with the payment link rather
    // than the oldest row: it is the only blocker with a hard expiry.
    public static List<string> MobileNeedsYouOrder { get; } = new()
    {
        "FBBMFL26F03BBD2", "FBBMFL26F71CD4", "FBBMFL26F55QP9", "FBBMFL26F62LK3", "FBBMFL26F48TR1",
    };

    public static List<NeedsAttentionItem> NeedsAttention { get; } = new()
    {
        new("F99AAC1", "R•••••• T•••••", "+ 2 joint", "Upload documents", "Proof of address rejected — address does not match source", "text-danger", "2 days", "Fix documents", "Fix"),
        new("F76B373", "P•••• N•••", "new customer", "Investor information", "NSDL failed twice — name and DOB do not match PAN", "text-danger", "1 day", "Correct and retry", "Retry"),
        new("F41C902", "A•••• D•••••", "joint · holder 2", "DPDP consent", "Consent link expires in 6 hours — not opened yet", "text-amber", "3 days", "Resend link", "Resend"),
        new("F38D511", "K•••• I•••••", "primary", "DPDP consent", "Signed CKYC form never uploaded — consent OTP locked", "text-amber", "4 days", "Upload form", "Upload"),
        new("F22E084", "M•••• S•••", "primary", "With Operations", "Manual verification after three NSDL failures", "text-muted", "8 days", "Waiting on Ops", "With Ops", Actionable: false),
    };

    public static List<PinnedApp> PinnedApps { get; } = new()
    {
        new("Uno TP", "FD sourcing and renewals", true, "uno-tp", 5, "FD sourcing"),
        new("DMS Explorer", "Document repository", false, "folder", null, "Documents"),
        new("OVD Explorer", "Officially valid documents", false, "id-card", null, "Valid documents"),
        new("CP MIS View", "Business and payout MIS", false, "bars", null, "Business MIS"),
    };

    public static List<AppTile> SourcingApps { get; } = new()
    {
        new("Deposit Servicing", "Renewals, repayments, maturity", "rupee", MobileDescription: "Renewals, maturity"),
        new("Rate & Yield Calculator", "Quote before sourcing", "calculator"),
        new("Investor 360", "Holdings across deposits", "people"),
        new("Service Requests", "Raise and track tickets", "ticket", Badge: 2, MobileDescription: "Tickets"),
    };

    public static List<AppTile> ComplianceApps { get; } = new()
    {
        new("DPDP Consent Register", "Consents by holder and purpose", "shield-check"),
        new("Circulars & Notices", "Rate cards and policy", "bell", Badge: 1),
        new("Training & Certification", "AMFI, KYC refreshers", "book"),
        new("Sub-broker Admin", "Codes, mapping, payouts", "gear"),
    };

    public static List<AppTile> RequestableApps { get; } = new()
    {
        new("Analytics Studio", "Custom reports", "bars", Disabled: true),
        new("Lead Management", "Campaign leads", "people", Disabled: true),
    };

    public static List<InFlightSummary> DashboardInFlightSummary { get; } = new()
    {
        new(3, "Awaiting consent", "2 offline forms · 1 digital link"),
        new(4, "Documents pending", "1 rejected · 3 not started"),
        new(2, "Awaiting payment", "both online, link sent"),
        new(2, "With Operations", "manual verification"),
    };

    public static List<QuickLink> DashboardQuickActions { get; } = new()
    {
        new("Create New FD", "", "/Apps/UnoTp/Application/HolderIdentification"),
        new("PIS Generation — Axis", ""),
        new("Short URL", ""),
        new("DPDP Consent Tracker", "", "/Apps/UnoTp/Desktop/ConsentTracker"),
    };

    public static List<QuickLink> DashboardServices { get; } = new()
    {
        new("Application status", "11 open"),
        new("Renew FD", "4 maturing in 30 days"),
        new("View existing application", "search by PAN or folio"),
    };

    public static List<ChecklistItem> DashboardChecklist { get; } = new()
    {
        new(1, "PAN and date of birth for every holder — or the folio number"),
        new(2, "PAN card image for any holder who is not an existing customer"),
        new(3, "Signed DPDP and CKYC forms for any holder consenting offline"),
        new(4, "Holder's mobile for the consent OTP, or their access to the link"),
        new(5, "Bank details and the payment cheque"),
        new(6, "Deposit amount, tenure and scheme choice"),
    };

    public static List<string> DashboardGoodToKnow { get; } = new()
    {
        "eSarathi covers individual and sole proprietorship investments only.",
        "Booking is subject to Operations validating the submitted documents.",
    };

    // Board 12's "today": validity dates and the Requested filter count back from here.
    public static readonly DateOnly ConsentToday = new(2026, 9, 17);

    public static List<ConsentRecord> ConsentRecords { get; } = BuildConsentRecords();

    private static List<ConsentRecord> BuildConsentRecords()
    {
        const ConsentSource Digital = ConsentSource.Digital;
        const ConsentSource Offline = ConsentSource.Offline;

        // The first six are the rows Board 12 draws, in its order; the next six
        // make up the rest of the non-"on file" tiles (3 / 2 / 2 / 4).
        var records = new List<ConsentRecord>
        {
            new("P•••• N•••", "CDEPN2290F", "holder 3", "FD2500184213", Digital, ConsentBucket.AwaitingAcceptance,
                "Link sent · not accepted", "sent 11:28 · expires in 21h", ConsentTone.Blue,
                "Embedded in same link", "CKYC pull at application completion", ConsentTone.Plain,
                null, "Resend link", 0),
            new("S•••• T•••••", "AXKP••••L", "folio MF0051188", "FD2500184213", Offline, ConsentBucket.UploadPending,
                "Signed form uploaded", "11:14 · accepted", ConsentTone.Green,
                "Upload pending", "searched · form downloaded", ConsentTone.Amber,
                null, "Upload forms", 0),
            new("A•••• K•••", "BKLPA••••C", "folio MF0049023", "FD2500183998", Digital, ConsentBucket.DeclinedOrExpired,
                "Link expired", "not opened in 24h", ConsentTone.Red,
                "Not authorised", "CKYC not pulled", ConsentTone.Plain,
                null, "Resend link", 2),
            new("R•••••• T•••••", "ABCPT••••K", "folio MF0051187", "FD2500184213", Digital, ConsentBucket.OnFile,
                "On file · accepted", "12 Mar 2025 · CN-77341", ConsentTone.Green,
                "CKYC downloaded", "12 Mar 2025", ConsentTone.Green,
                "11 Mar 2027", "View artefact", 554),
            new("M•••• D•••••", "CJKPD••••M", "folio MF0044120", null, Offline, ConsentBucket.ExpiringSoon,
                "Expiring soon", "renewal not started", ConsentTone.Amber,
                "CKYC on file", "02 Oct 2024", ConsentTone.Green,
                "01 Oct 2026", "Renew consent", 715),
            new("V•••• I•••", "DKMPI••••R", "folio MF0050771", "FD2500184007", Digital, ConsentBucket.DeclinedOrExpired,
                "Declined by holder", "11:47 · reason not stated", ConsentTone.Red,
                "Not authorised", "holder removed from FD", ConsentTone.Plain,
                null, "Send again", 0),

            new("K•••• I•••••", "EFHPI••••S", "folio MF0050912", "FD2500184121", Digital, ConsentBucket.AwaitingAcceptance,
                "Link sent · not accepted", "sent yesterday 16:02 · expires in 4h", ConsentTone.Blue,
                "Embedded in same link", "CKYC pull at application completion", ConsentTone.Plain,
                null, "Resend link", 1),
            new("D•••• K••••", "GHKPK••••A", "holder 2", "FD2500184188", Digital, ConsentBucket.AwaitingAcceptance,
                "Link opened · not accepted", "opened 09:40 · expires in 19h", ConsentTone.Blue,
                "Embedded in same link", "CKYC pull at application completion", ConsentTone.Plain,
                null, "Resend link", 0),
            new("J•••• B•••••", "HJLPB••••D", "folio MF0050433", "FD2500184150", Offline, ConsentBucket.UploadPending,
                "Upload pending", "form downloaded 10:02", ConsentTone.Amber,
                "Upload pending", "searched · form downloaded", ConsentTone.Amber,
                null, "Upload forms", 0),
            new("N•••• S••••", "JKMPS••••F", "folio MF0041876", null, Digital, ConsentBucket.ExpiringSoon,
                "Expiring soon", "renewal link not sent", ConsentTone.Amber,
                "CKYC on file", "20 Sep 2024", ConsentTone.Green,
                "19 Sep 2026", "Renew consent", 727),
            new("G•••• R•••", "KLNPR••••H", "folio MF0048210", null, Offline, ConsentBucket.DeclinedOrExpired,
                "Consent expired", "lapsed 04 Sep 2026", ConsentTone.Red,
                "CKYC on file", "05 Sep 2024", ConsentTone.Green,
                "04 Sep 2026", "Renew consent", 742),
            new("B•••• S•••••", "LMNPS••••J", "holder 2", "FD2500184066", Digital, ConsentBucket.DeclinedOrExpired,
                "Link expired", "opened · not accepted in 24h", ConsentTone.Red,
                "Not authorised", "CKYC not pulled", ConsentTone.Plain,
                null, "Resend link", 4),
        };

        // The quiet rows behind "On file": accepted at some point in the last two
        // years and valid for two years from acceptance, so none is inside the
        // 30-day expiry window.
        const string first = "ABCDGHJKLMNPRSTV";
        const string last = "BDGIJKMNPRST";
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        for (var i = 0; i < 30; i++)
        {
            var f = first[i % first.Length];
            var l = last[(i * 5) % last.Length];
            var days = 3 + (i * 53) % 690;
            var accepted = ConsentToday.AddDays(-days);
            var date = accepted.ToString("dd MMM yyyy", culture);
            var pan = $"{(char)('A' + (i * 3) % 20)}{(char)('B' + (i * 7) % 22)}{(char)('C' + (i * 11) % 23)}P{l}••••{(char)('A' + (i * 13) % 26)}";

            records.Add(new(
                $"{f}•••• {l}•••••", pan, $"folio MF{40100 + i * 373:D7}",
                i % 7 == 3 ? null : $"FD25{180000 + i * 113:D8}",
                i % 3 == 1 ? Offline : Digital, ConsentBucket.OnFile,
                "On file · accepted", $"{date} · CN-{77342 + i}", ConsentTone.Green,
                "CKYC downloaded", date, ConsentTone.Green,
                accepted.AddYears(2).AddDays(-1).ToString("dd MMM yyyy", culture), "View artefact", days));
        }

        return records;
    }
}

// One entry in the Board 00 icon set. Size and Stroke vary by where the icon is
// used: 18px grey on an app tile, 17px on a profile row, 13px on a chevron.
public record AppIcon(string Key, int Size = 18, string Stroke = "#6B7280");
