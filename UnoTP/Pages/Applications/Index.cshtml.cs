using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// View Application: the partner's applications a search reaches, each with where it
/// stands. The search is in the address - <c>by</c> (dates, appno or folio), <c>from</c> and
/// <c>to</c> (yyyy-MM-dd) or <c>q</c> (what was typed) - and is run by the database.
/// </summary>
[RequiresFeature("view-app")]
public class ApplicationsModel(IApplicationApi applications, Lookups lookups) : PageModel
{
    /// <summary>What the page shows.</summary>
    public ApplicationsViewModel View { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string? by, string? q, string? from, string? to)
    {
        var windowDays = (await lookups.ConfigAsync()).CancellationDays;
        var search = ListSearch.Asked(by, q, from, to, windowDays, folioToo: true);
        View = new ApplicationsViewModel(await applications.SearchAsync(search), windowDays, search);
        return Page();
    }
}
