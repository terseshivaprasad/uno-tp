using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>View Application: the partner's applications, each with where it stands.</summary>
[RequiresFeature("view-app")]
public class ApplicationsModel(IApplicationApi applications, Lookups lookups) : PageModel
{
    /// <summary>What the page shows.</summary>
    public ApplicationsViewModel View { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        View = new ApplicationsViewModel(await applications.ListAsync(), (await lookups.ConfigAsync()).CancellationDays);
        return Page();
    }
}
