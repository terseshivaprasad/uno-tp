namespace UnoTp.Models;

// Icon is the key the _AppIcon partial switches on; MobileDescription is the
// shorter label Board 00 - Mobile uses on the two-up tiles, where the desktop
// description would wrap to three lines.
public record PinnedApp(string Title, string Description, bool Pinned, string Icon, int? Badge = null, string? MobileDescription = null, string? PageLink = null);

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

public enum DmsClass { Kyc, Fd, Open }

// One file the document store holds under a control number. InDms and DmsId stay
// null while the upload is still waiting to reach DMS. Only a KYC document belongs to
// a holder; FD and Open documents have no holder relation and are always filed as
// "Not holder-specific" (code 00).
public record DmsDocument(
    string HolderType,
    string Type,
    DmsClass Class,
    string File,
    DateTime Uploaded,
    DateTime? InDms,
    string? DmsId,
    // Masked as the row shows it; the viewer adds that it unmasks on open.
    string? Ref = null,
    string? Expiry = null,
    // A free line under the type, e.g. "mailing address".
    string? Note = null,
    string? Remark = null,
    // Versions this one replaced, oldest first. The store never drops a version.
    List<DmsVersion>? Earlier = null,
    // What the FD upload form captured; null for KYC and Open documents.
    DmsFdDetails? Fd = null
);

// The FD upload form's fields. Pan is held in full: the view masks it, and a lookup
// by PAN matches it.
public record DmsFdDetails(string FinYear, string Period, string Pan, string FolioNo, string FdrNo);

// One filed version of a document. ApplicationNo is set only when it was filed
// for an earlier application than the one the control number is now on;
// ReplacedBecause is the reason given when the next version replaced it.
public record DmsVersion(
    string File,
    DateTime Uploaded,
    DateTime? InDms,
    string? DmsId,
    string? ApplicationNo = null,
    string? ReplacedBecause = null,
    DmsFdDetails? Fd = null
);

public record DmsFiling(string ControlNo, string ApplicationNo, string FiledBy, List<DmsDocument> Documents);

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
        // The one Uno TP tile opens either view: the tile itself carries the switch.
        new("Uno TP", "FD sourcing and renewals", true, "uno-tp", 5, "FD sourcing"),
        new("DMS Explorer", "Document repository", false, "folder", null, "Documents", "/Apps/DmsExplorer/Index"),
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

    // The DMS Explorer's one filing: the documents behind FBBMFL26F99AAC1, whose
    // three holders the store files as the investor and two joint holders. The
    // investor first deposited in March 2025 (consent CN-77341), so the investor's
    // KYC, the cheque and the form each carry a version from that application.
    public static DmsFiling DmsFiling { get; } = new("123456", ApplicationNo, EntityCode, BuildDmsDocuments());

    private static List<DmsDocument> BuildDmsDocuments()
    {
        static DateTime At(int day, int hour, int minute) => new(2026, 9, day, hour, minute, 0);
        static DateTime In2025(int hour, int minute) => new(2025, 3, 12, hour, minute, 0);
        const string Earlier = "FBBMFL25C12RT7";
        // The investor's folio (as the consent register has it) holds both deposits;
        // each deposit has its own receipt, period and financial year.
        const string Folio = "MF0051187", InvestorPan = "ABCPT1234D";
        const string Fdr2025 = "250312007", Fdr2026 = "260914012", Period2025 = "12 months", Period2026 = "36 months";
        const string Investor = "Investor", Joint1 = "Joint holder 1", Joint2 = "Joint holder 2", Shared = "Not holder-specific";

        return new()
        {
            new(Investor, "Identity proof · PAN", DmsClass.Kyc, "123456_01_PAN__13092026.tif", At(13, 18, 15), At(13, 18, 16), "DMS-883911", Ref: "ABCPT••••D",
                Earlier: new() { new("123456_01_PAN__12032025.tif", In2025(10, 14), In2025(10, 15), "DMS-612044", Earlier, "refiled with the new application") }),
            new(Investor, "Proof of address · passport", DmsClass.Kyc, "123456_01_Passport__14092026.jpg", At(14, 11, 36), At(14, 11, 37), "DMS-884101", Ref: "P44•••82", Expiry: "11/2032"),
            new(Investor, "Proof of address · utility bill", DmsClass.Kyc, "123456_01_UtilityBill__14092026.pdf", At(14, 11, 38), At(14, 11, 39), "DMS-884103", Note: "mailing address",
                Earlier: new() { new("123456_01_UtilityBill__12032025.pdf", In2025(10, 18), In2025(10, 19), "DMS-612047", Earlier, "bill older than three months") }),
            new(Investor, "Photograph", DmsClass.Kyc, "123456_01_Photograph__13092026.jpg", At(13, 18, 14), At(13, 18, 15), "DMS-883912",
                Earlier: new() { new("123456_01_Photograph__12032025.jpg", In2025(10, 16), In2025(10, 17), "DMS-612045", Earlier, "refiled with the new application") }),

            new(Joint1, "Identity proof · PAN", DmsClass.Kyc, "123456_02_PAN__13092026.tif", At(13, 18, 16), null, null, Ref: "BCDPT••••E", Remark: "Awaiting DMS · 2 h"),
            new(Joint1, "Proof of address · driving licence", DmsClass.Kyc, "123456_02_DrivingLicence__14092026.jpg", At(14, 11, 41), At(14, 11, 42), "DMS-884108", Ref: "MH02••••••044", Expiry: "06/2031"),
            new(Joint1, "Photograph", DmsClass.Kyc, "123456_02_Photograph__13092026.jpg", At(13, 18, 16), At(13, 18, 17), "DMS-883915"),

            new(Joint2, "Identity proof · PAN", DmsClass.Kyc, "123456_03_PAN__13092026.tif", At(13, 18, 17), At(13, 18, 18), "DMS-883914", Ref: "CDEPN••••F"),
            new(Joint2, "Proof of address · Aadhaar", DmsClass.Kyc, "123456_03_AadharCard__14092026.jpg", At(14, 11, 36), At(14, 11, 37), "DMS-884120", Ref: "••••2290",
                Remark: "Superseded on re-upload",
                Earlier: new() { new("123456_03_AadharCard__13092026.jpg", At(13, 18, 17), At(13, 18, 18), "DMS-883913", ReplacedBecause: "address side cropped") }),
            new(Joint2, "Photograph", DmsClass.Kyc, "123456_03_Photograph__13092026.jpg", At(13, 18, 18), At(13, 18, 19), "DMS-883916"),

            new(Shared, "Payment instrument · cheque", DmsClass.Fd, "123456_00_Cheque__14092026.jpg", At(14, 12, 2), At(14, 12, 3), "DMS-884130", Ref: "004512", Fd: new("2026-27", Period2026, InvestorPan, Folio, Fdr2026),
                Earlier: new()
                {
                    new("123456_00_Cheque__12032025.jpg", In2025(10, 31), In2025(10, 32), "DMS-612052", Earlier, "cheque for the new deposit",
                        new("2024-25", Period2025, InvestorPan, Folio, Fdr2025)),
                    new("123456_00_Cheque__13092026.jpg", At(13, 18, 20), At(13, 18, 21), "DMS-883918", ReplacedBecause: "cheque date overwritten · Operations asked for a fresh scan",
                        Fd: new("2026-27", Period2026, InvestorPan, Folio, Fdr2026)),
                }),
            new(Shared, "Application form · signed", DmsClass.Fd, "123456_00_FDForm__14092026.pdf", At(14, 16, 58), At(14, 16, 59), "DMS-884144",
                Fd: new("2026-27", Period2026, InvestorPan, Folio, Fdr2026),
                Earlier: new()
                {
                    new("123456_00_FDForm__12032025.pdf", In2025(10, 40), In2025(10, 41), "DMS-612058", Earlier, "form for the new application",
                        new("2024-25", Period2025, InvestorPan, Folio, Fdr2025)),
                }),
            new(Shared, "Tax declaration · Form 15G", DmsClass.Fd, "123456_00_Form15G__14092026.pdf", At(14, 12, 20), At(14, 12, 21), "DMS-884122", Expiry: "31/03/2027",
                Fd: new("2026-27", Period2026, InvestorPan, Folio, Fdr2026)),
            new(Shared, "Letter of authority", DmsClass.Open, "123456_00_Authority Letter__14092026.pdf", At(14, 17, 4), At(14, 17, 5), "DMS-884151",
                Note: "typed type · open class", Remark: "Filed at broker request"),
        };
    }

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
