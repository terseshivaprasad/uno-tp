using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Features;

namespace UnoTP.Pages.Application;

/// <summary>
/// Consolidated single page covering mockup screens 02, 02A, 02B, 03, 04, 05 and 06:
/// holder search -> search result / renewal entry -> renewal decision -> existing customer
/// found -> new customer PAN &amp; NSDL -> DPDP consent -> all holders complete.
/// Each mockup screen is rendered as an internal "step" section on this one page and the
/// sections are shown/hidden with a small inline script (see wwwroot/js/holder-identification.js),
/// mirroring how the mockup's own doc-page.js/support.js switch between states client-side.
/// </summary>
public class HolderIdentificationModel : PageModel
{
    public HolderIdentificationModel(ConsentPlan consent) => Consent = consent;

    /// <summary>Which consents this application collects, and the copy that describes them.</summary>
    public ConsentPlan Consent { get; }

    /// <summary>Which internal step to start on: search, result, renewal, existing, newpan, consent, complete.</summary>
    [BindProperty(SupportsGet = true)]
    public string Step { get; set; } = "search";

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        // Mock flow: any submit from this page moves on to document upload (screen 07).
        return RedirectToPage("/Application/UploadDocuments");
    }
}
