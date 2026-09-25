using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>Console Admin: which features are on, and what the bell tells partners is coming.</summary>
public class AdminViewModel(FeatureSet features, ConsoleBoard board)
{
    public ConsoleFeature[] Features => ConsoleAdmin.Features;

    public FeatureFlags Flags => features.Flags;

    /// <summary>The kinds a notice can be, from the backend.</summary>
    public IReadOnlyList<string> NoticeKinds { get; init; } = [];

    // The windows that still matter: the one running now and the ones to come.
    // An ended window is kept in the model but not listed; it explains nothing
    // the partner can still run into.
    public IEnumerable<FeatureWindow> Upcoming =>
        Board.Windows.Where(w => !w.Ended).OrderBy(w => w.From);

    public IEnumerable<AnnouncementRecord> Notices =>
        Board.Announcements.Where(a => a.At.Date >= DateTime.Today).OrderBy(a => a.At);

    /// <summary>What is scheduled, as the backend holds it.</summary>
    public ConsoleBoard Board { get; } = board;

    // What the two forms open on: a window tomorrow night, which is when most
    // of them are set, and a notice a week out.
    public DateTime DefaultFrom => DateTime.Today.AddDays(1).AddHours(22);

    public DateTime DefaultTo => DateTime.Today.AddDays(2).AddHours(1);

    public DateTime DefaultNoticeAt => DateTime.Today.AddDays(7);
}
