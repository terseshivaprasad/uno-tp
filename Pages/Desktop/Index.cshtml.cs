using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Desktop;

public class IndexModel : PageModel
{
    public List<PinnedApp> PinnedApps => MockData.PinnedApps;
    public List<InFlightApplication> Applications => MockData.InFlightApplications;
    public List<AppTile> SourcingApps => MockData.SourcingApps;
    public List<AppTile> ComplianceApps => MockData.ComplianceApps;
    public List<AppTile> RequestableApps => MockData.RequestableApps;

    public void OnGet()
    {
    }
}
