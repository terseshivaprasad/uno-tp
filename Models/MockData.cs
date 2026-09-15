namespace UnoTp.Models;

public record PinnedApp(string Title, string Description, bool Pinned, int? Badge = null);

public record AppTile(string Title, string Description, int? Badge = null, bool Disabled = false);

public record InFlightApplication(
    string AppNo,
    string HolderMask,
    string Status,
    string StatusDetail,
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
    string Action
);

public record InFlightSummary(int Count, string Label, string Detail);

public record QuickLink(string Title, string Detail, string? PageLink = null);

public record ChecklistItem(int Step, string Text);

public static class MockData
{
    public const string ApplicationNo = "FBBMFL26F99AAC1";
    public const string BrokerName = "Shivaprasad Terse";
    public const string EntityCode = "100002225";

    public static List<InFlightApplication> InFlightApplications { get; } = new()
    {
        new("FBBMFL26F71CD4", "D•••• K•••• +1", "Consent pending", "holder 2 has not completed OTP · re-send or switch to offline", "text-danger", "₹ 1,50,000", "Consent · 06", "3 d", "text-danger", "Open", "needs-you"),
        new("FBBMFL26F03BBD2", "A•••• D•••••• +1", "Payment link unpaid", "expires tomorrow 5:04 PM · one resend left", "text-danger", "₹ 3,00,000", "Submitted · 11B", "19 h", "text-danger", "Open", "needs-you"),
        new("FBBMFL26F55QP9", "M•••• S•••", "Re-upload requested", "holder 1's PAN card illegible · Operations asked for a fresh scan", "text-danger", "₹ 10,00,000", "Documents · 07", "6 h", "text-danger", "Open", "needs-you"),
        new("FBBMFL26F62LK3", "V•••• J•••• +2", "Incomplete", "7 investor fields blank across two holders · nominee not captured", "text-amber", "₹ 2,00,000", "Investor info · 08", "2 d", "text-muted", "Open", "needs-you"),
        new("FBBMFL26F48TR1", "S•••• B••••", "Incomplete", "bank details not started · cheque uploaded but unread", "text-amber", "₹ 5,00,000", "Bank · 09", "1 d", "text-muted", "Open", "needs-you"),
        new("FBBMFL26F99AAC1", "R•••••• T••••• +2", "Under review", "holder 3's Aadhaar and the cheque date with Operations", "text-amber", "₹ 5,00,000", "Verification", "1 h", "text-muted", "View", "operations"),
        new("FBBMFL26E88XZ7", "K•••• P••••", "Verified", "awaiting cheque realisation · books on realisation", "text-success", "₹ 7,50,000", "Realisation", "1 d", "text-muted", "View", "realisation"),
    };

    public static List<NeedsAttentionItem> NeedsAttention { get; } = new()
    {
        new("F99AAC1", "R•••••• T•••••", "+ 2 joint", "Upload documents", "Proof of address rejected — address does not match source", "text-danger", "2 days", "Fix documents"),
        new("F76B373", "P•••• N•••", "new customer", "Investor information", "NSDL failed twice — name and DOB do not match PAN", "text-danger", "1 day", "Correct and retry"),
        new("F41C902", "A•••• D•••••", "joint · holder 2", "DPDP consent", "Consent link expires in 6 hours — not opened yet", "text-amber", "3 days", "Resend link"),
        new("F38D511", "K•••• I•••••", "primary", "DPDP consent", "Signed CKYC form never uploaded — consent OTP locked", "text-amber", "4 days", "Upload form"),
        new("F22E084", "M•••• S•••", "primary", "With Operations", "Manual verification after three NSDL failures", "text-muted", "8 days", "Waiting on Ops"),
    };

    public static List<PinnedApp> PinnedApps { get; } = new()
    {
        new("Uno TP", "FD sourcing and renewals", true, 5),
        new("DMS Explorer", "Document repository", false, null),
        new("OVD Explorer", "Officially valid documents", false, null),
        new("CP MIS View", "Business and payout MIS", false, null),
    };

    public static List<AppTile> SourcingApps { get; } = new()
    {
        new("Deposit Servicing", "Renewals, repayments, maturity"),
        new("Rate & Yield Calculator", "Quote before sourcing"),
        new("Investor 360", "Holdings across deposits"),
        new("Service Requests", "Raise and track tickets", Badge: 2),
    };

    public static List<AppTile> ComplianceApps { get; } = new()
    {
        new("DPDP Consent Register", "Consents by holder and purpose"),
        new("Circulars & Notices", "Rate cards and policy", Badge: 1),
        new("Training & Certification", "AMFI, KYC refreshers"),
        new("Sub-broker Admin", "Codes, mapping, payouts"),
    };

    public static List<AppTile> RequestableApps { get; } = new()
    {
        new("Analytics Studio", "Custom reports", Disabled: true),
        new("Lead Management", "Campaign leads", Disabled: true),
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
        new("DPDP Consent Tracker", ""),
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
}
