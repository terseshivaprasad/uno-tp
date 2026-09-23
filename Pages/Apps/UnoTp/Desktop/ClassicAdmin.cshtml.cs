using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ClassicAdminModel : PageModel
{
    public ConsoleFeature[] Features => ConsoleAdmin.Features;

    // The windows that still matter: the one running now and the ones to come.
    // An ended window is kept in the model but not listed; it explains nothing
    // the partner can still run into.
    public IEnumerable<FeatureWindow> Upcoming =>
        ConsoleAdmin.Windows.Where(w => !w.Ended).OrderBy(w => w.From);

    public IEnumerable<ConsoleAdmin.Standalone> Notices =>
        ConsoleAdmin.Announcements.Where(a => a.At.Date >= DateTime.Today).OrderBy(a => a.At);

    // What the two forms open on: a window tomorrow night, which is when most
    // of them are set, and a notice a week out.
    public DateTime DefaultFrom => DateTime.Today.AddDays(1).AddHours(22);

    public DateTime DefaultTo => DateTime.Today.AddDays(2).AddHours(1);

    public DateTime DefaultNoticeAt => DateTime.Today.AddDays(7);

    public IActionResult OnGet() =>
        // No sign-in in this mock, so this only ever sends a partner away when
        // the flag is turned off by hand.
        ConsoleAdmin.IsAdmin ? Page() : RedirectToPage("/Apps/UnoTp/Desktop/ClassicDashboard");
}
