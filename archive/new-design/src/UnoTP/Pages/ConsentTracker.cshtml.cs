using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Features;
using UnoTP.Models;

namespace UnoTP.Pages;

public class ConsentTrackerModel(ConsentPlan consents) : PageModel
{
    public const int PageSize = 6;

    // With CKYC consent switched off there is no CERSAI authorisation to track,
    // so the column and the subtitle's mention of it both drop out.
    public bool ShowCersai { get; } = consents.HasCkyc;

    public List<ConsentRecord> Records => MockData.ConsentRecords;

    public int Count(ConsentBucket bucket) => Records.Count(r => r.Bucket == bucket);

    public void OnGet()
    {
    }
}
