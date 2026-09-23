using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ClassicDashboardModel : PageModel
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

    public void OnGet()
    {
    }
}
