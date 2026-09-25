using UnoTP.Features;
using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// One big tile on the dashboard. Controller is where it opens, or null while there
/// is no page behind it yet; Off is what the tile says in place of opening, or null
/// while the feature behind it is on.
/// </summary>
public record DashboardTile(string Key, string Title, string Glyph, string? Controller, string? Off);

/// <summary>The classic dashboard: its tiles, and why a closed page sent the partner here.</summary>
public class DashboardViewModel(FeatureSet features, ConsoleBoard board, string? off)
{
    // The four booking steps' outline glyphs (24px grid), in order: documents,
    // investor, payment, deposit. The dashboard lists them and the search page's
    // rail repeats them.
    public static readonly string[] StepGlyphs =
    {
        "<path d=\"M6.5 2.5H14l5 5v13a1 1 0 0 1-1 1H6.5a1.5 1.5 0 0 1-1.5-1.5V4a1.5 1.5 0 0 1 1.5-1.5z\"></path><path d=\"M14 2.5v5h5\"></path>",
        "<path d=\"M10.5 4.5a8 8 0 1 0 8 8h-8z\"></path><path d=\"M13.5 2a8 8 0 0 1 8 8h-8z\"></path>",
        "<circle cx=\"12\" cy=\"12\" r=\"9.5\"></circle><path d=\"M8.5 7.5h7M8.5 10.5h7M12 7.5c3 0 3 5-.5 5h-3l5.5 5\"></path>",
        "<path d=\"M12 6.5C9.5 4.5 6 4 3 5v14c3-1 6.5-.5 9 1.5 2.5-2 6-2.5 9-1.5V5c-3-1-6.5-.5-9 1.5z\"></path><path d=\"M12 6.5v14\"></path>",
    };

    public static readonly string[] StepLabels =
        ["Upload documents", "Investor information", "Payment & repayment", "Fixed deposit details"];

    // The standing Note under the steps, as the old dashboard words it.
    public static readonly string[] Notes =
    [
        "Currently Esarathi is enabled with individual and sole proprietorship investment only.",
        "Verification of the Fixed Deposit is subject to validation of the submitted documents by the Operations team.",
    ];

    // Tile glyphs are drawn solid, as the old site's SVG icons are: slate shapes
    // with white detail knocked out of them.
    // A page with its corner folded over: the body, then the fold.
    private const string Sheet = "<path d=\"M5 3.5a2 2 0 0 1 2-2h6.5V7a2 2 0 0 0 2 2H21v12.5a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2z\" fill=\"currentColor\"></path><path d=\"M15 1.5l6 6h-5a1 1 0 0 1-1-1z\" fill=\"currentColor\"></path>";
    private const string White = "stroke=\"#fff\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
    private const string Slate = "stroke=\"currentColor\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";

    private IReadOnlyList<DashboardTile>? newFd;

    public IReadOnlyList<DashboardTile> NewFd => newFd ??=
    [
        // A new page with a plus.
        Tile("new-fd", "Create New FD", Sheet + $"<path d=\"M13 11.5v8M9 15.5h8\" {White} stroke-width=\"2.4\"></path>", "InvestorIdentification"),
        // A pay-in slip: a torn-off receipt with the rupee on it.
        Tile("pis", "PIS Generation - Axis", "<path d=\"M5 3.5a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v18l-2.3-1.5-2.4 1.5-2.3-1.5-2.3 1.5-2.4-1.5L5 21.5z\" fill=\"currentColor\"></path>"
            + $"<path d=\"M9 7h6M9 10h6M12.3 7c2.6 0 2.6 5-.8 5H9.4l4.8 4.3\" {White} stroke-width=\"1.8\" fill=\"none\"></path>", "PayInSlip"),
        // Looking an application up: a page with a magnifier.
        Tile("view-app", "View existing application", Sheet + $"<circle cx=\"12.5\" cy=\"14.5\" r=\"3.2\" {White} stroke-width=\"2\"></circle><path d=\"M14.9 16.9l2.6 2.6\" {White} stroke-width=\"2.2\"></path>", "ViewApplication"),
        // Two chain links.
        Tile("short-url", "Short URL", $"<g {Slate} stroke-width=\"2.8\"><path d=\"M10 14a4.5 4.5 0 0 0 6.4 0l3.2-3.2a4.5 4.5 0 0 0-6.4-6.4L11.6 6\"></path><path d=\"M14 10a4.5 4.5 0 0 0-6.4 0l-3.2 3.2a4.5 4.5 0 0 0 6.4 6.4l1.6-1.6\"></path></g>", "ShortUrl"),
    ];

    private IReadOnlyList<DashboardTile>? services;

    public IReadOnlyList<DashboardTile> Services => services ??=
    [
        // Where an application stands: a clipboard with a tick.
        Tile("app-status", "Application status", "<rect x=\"4.5\" y=\"3.5\" width=\"15\" height=\"19\" rx=\"2\" fill=\"currentColor\"></rect><rect x=\"8.5\" y=\"1.5\" width=\"7\" height=\"4\" rx=\"1\" fill=\"currentColor\" stroke=\"#fff\" stroke-width=\"1.4\"></rect>"
            + $"<path d=\"M8.5 14l2.5 2.5 4.8-5\" {White} stroke-width=\"2.2\" fill=\"none\"></path>", null),
        // Rolling a deposit over: arrows turning round the rupee.
        Tile("renew", "Renew FD", $"<g {Slate} stroke-width=\"2.3\"><path d=\"M20.5 12a8.5 8.5 0 0 1-15 5.5\"></path><path d=\"M3.5 12a8.5 8.5 0 0 1 15-5.5\"></path><path d=\"M19.5 2.5V7H15\"></path><path d=\"M4.5 21.5V17H9\"></path></g>"
            + $"<path d=\"M10.2 9.2h3.6M10.2 11h3.6M12.1 9.2c1.6 0 1.6 3.4-.6 3.4h-1.3l3 2.6\" {Slate} stroke-width=\"1.4\"></path>", null),
    ];

    /// <summary>Console Admin, shown only while the Admin feature is on.</summary>
    private IReadOnlyList<DashboardTile>? admin;

    public IReadOnlyList<DashboardTile> Admin => admin ??= features.Flags.Admin
        ?
        [
            // Sliders: three settings, each with its own knob.
            Tile("admin", "Console Admin", $"<g {Slate} stroke-width=\"2.4\"><path d=\"M3.5 7h17M3.5 12h17M3.5 17h17\"></path></g>"
                + "<circle cx=\"9\" cy=\"7\" r=\"2.9\" fill=\"currentColor\" stroke=\"#fff\" stroke-width=\"1.6\"></circle>"
                + "<circle cx=\"15\" cy=\"12\" r=\"2.9\" fill=\"currentColor\" stroke=\"#fff\" stroke-width=\"1.6\"></circle>"
                + "<circle cx=\"7\" cy=\"17\" r=\"2.9\" fill=\"currentColor\" stroke=\"#fff\" stroke-width=\"1.6\"></circle>", "Admin"),
        ]
        : [];

    /// <summary>What is scheduled against the features, which the tiles are drawn from.</summary>
    public ConsoleBoard Board { get; } = board;

    /// <summary>
    /// Set when FeatureGate sent the partner here from a page that is closed, so the
    /// dashboard says which feature and why instead of just reappearing.
    /// </summary>
    public string? Closed { get; } = ClosedLine(features, board, off);

    private static string? ClosedLine(FeatureSet features, ConsoleBoard board, string? off)
    {
        if (off is null) return null;
        var reason = board.OffLabel(off, features.Flags);
        // The feature may have come back on between the redirect and this page.
        if (reason is null) return null;
        var name = ConsoleAdmin.NameOf(off);
        return features.Flags.IsOn(off)
            ? $"{name} is off for a scheduled window. {reason}."
            : reason == "Unavailable"
                ? $"{name} is switched off right now."
                : $"{name} is not available: {reason.ToLowerInvariant()}.";
    }

    // A tile with no page behind it yet cannot open even when its feature is on,
    // so it reads as coming rather than as a button that does nothing.
    private DashboardTile Tile(string key, string title, string glyph, string? controller) =>
        new(key, title, glyph, controller, Board.OffLabel(key, features.Flags) ?? (controller is null ? "Coming soon" : null));
}
