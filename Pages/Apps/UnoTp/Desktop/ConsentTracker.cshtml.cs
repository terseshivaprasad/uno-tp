using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Features;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ConsentTrackerModel : PageModel
{
    public const int PageSize = 6;

    public ConsentTrackerModel(ConsentPlan consents)
    {
        // With CKYC consent switched off there is no CERSAI authorisation to track,
        // so the column and the subtitle's mention of it both drop out.
        ShowCersai = consents.HasCkyc;
    }

    public bool ShowCersai { get; }

    public List<ConsentRecord> Records => MockData.ConsentRecords;

    public int Count(ConsentBucket bucket) => Records.Count(r => r.Bucket == bucket);

    public void OnGet()
    {
    }
}
