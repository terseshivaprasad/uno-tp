using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Mobile;

public class IndexModel : PageModel
{
    public List<PinnedApp> PinnedApps => MockData.PinnedApps;
    public List<InFlightApplication> Applications => MockData.InFlightApplications;
    public List<AppTile> SourcingApps => MockData.SourcingApps;
    public List<AppTile> ComplianceApps => MockData.ComplianceApps;
    public List<AppTile> RequestableApps => MockData.RequestableApps;

    /// <summary>The two non-pinned apps Board 00 - Mobile surfaces; the rest sit behind "See all".</summary>
    public List<AppTile> AlsoProvisioned => SourcingApps
        .Where(a => a.Title is "Deposit Servicing" or "Service Requests")
        .ToList();

    public int MoreCount => SourcingApps.Count + ComplianceApps.Count - AlsoProvisioned.Count;

    /// <summary>Board 00 spells the count out, as the desktop copy does with "Twelve provisioned".</summary>
    public string MoreCountWord => MoreCount switch
    {
        1 => "One", 2 => "Two", 3 => "Three", 4 => "Four", 5 => "Five",
        6 => "Six", 7 => "Seven", 8 => "Eight", 9 => "Nine", 10 => "Ten",
        _ => MoreCount.ToString(),
    };

    /// <summary>Blockers the broker owns, in the board's order rather than by age.</summary>
    public List<InFlightApplication> NeedsYou => Applications
        .Where(a => a.StatusKey == "needs-you")
        .OrderBy(a => MockData.MobileNeedsYouOrder.IndexOf(a.AppNo))
        .ToList();

    /// <summary>The two Operations rows the board seeds; the card's own count is the real 6.</summary>
    public List<InFlightApplication> WithOperations => Applications
        .Where(a => a.StatusKey != "needs-you")
        .ToList();

    public void OnGet()
    {
    }
}
