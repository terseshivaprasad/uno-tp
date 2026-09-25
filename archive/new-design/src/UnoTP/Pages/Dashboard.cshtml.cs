using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;

namespace UnoTP.Pages;

public class DashboardModel : PageModel
{
    public List<NeedsAttentionItem> NeedsAttention => MockData.NeedsAttention;
    public List<InFlightSummary> InFlightSummary => MockData.DashboardInFlightSummary;
    public List<QuickLink> QuickActions => MockData.DashboardQuickActions;
    public List<QuickLink> Services => MockData.DashboardServices;
    public List<ChecklistItem> Checklist => MockData.DashboardChecklist;
    public List<string> GoodToKnow => MockData.DashboardGoodToKnow;

    public void OnGet()
    {
    }
}
