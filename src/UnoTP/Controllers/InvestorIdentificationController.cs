using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Investor Identification, the first classic wizard step: the primary holder is
/// taken through <see cref="HolderSearch"/>. Every step is a post, and every post
/// redirects back to the bare page address: what was typed and what the check found
/// are kept in the session, never in the address, so no PAN, date of birth or name
/// reaches a log, the history or a Referer header.
/// </summary>
[RequiresFeature("new-fd")]
[RequestSizeLimit(12 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
[Route("Purchase/InvestorIdentification")]
public class InvestorIdentificationController(
    FeatureSet features,
    IOptions<BackendOptions> backend,
    IApplicationApi applications,
    HolderSearch search) : Controller
{
    private const string SearchKey = "search";

    private SearchState? Saved
    {
        get => HttpContext.Session.Read<SearchState>(SearchKey);
        set => HttpContext.Session.Write(SearchKey, value);
    }

    /// <summary>The page as the session left it: blank, being filled in, or checked.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var model = search.NewModel();
        model.ShowDemoData = features.Flags.DemoData && backend.Value.IsMock;
        model.Drafts = await applications.DraftsAsync();
        Saved = await search.ShowAsync(model, Saved);
        return View(model);
    }

    /// <summary>Check record: what was typed is kept, and the page checks it.</summary>
    [HttpPost("check")]
    public IActionResult Check(SearchForm form)
    {
        Saved = HolderSearch.Check(form);
        return Back();
    }

    /// <summary>Search again: back to the fields, still holding what was typed.</summary>
    [HttpPost("again")]
    public IActionResult Again()
    {
        Saved = HolderSearch.Again(Saved);
        return Back();
    }

    /// <summary>Clear All: nothing typed, nothing found.</summary>
    [HttpPost("clear")]
    public IActionResult Clear()
    {
        Saved = null;
        return Back();
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(IFormFile? copy)
    {
        Saved = await search.VerifyAsync(Saved, copy);
        return Back();
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry(string? nsdlName)
    {
        Saved = await search.RetryAsync(Saved, nsdlName);
        return Back();
    }

    /// <summary>
    /// Opens the application and carries the holder to the upload step, once they are
    /// identified. The application is held on the server; the upload step finds it
    /// through the session.
    /// </summary>
    [HttpPost("proceed")]
    public async Task<IActionResult> Proceed()
    {
        if (await search.IdentifiedAsync(Saved) is not { Record: { } record }) return Back();

        // Every search that is proceeded from opens an application of its own, and
        // the backend gives it its number. A PAN copy already on it is not asked again.
        return Opened(await applications.OpenAsync(HolderSearch.ApplicationHolder(record)));
    }

    /// <summary>A draft picked up again, under the number it was opened with.</summary>
    [HttpPost("continue")]
    public async Task<IActionResult> Continue(string? draft)
    {
        // Only one of this partner's own drafts: the number posted is checked
        // against the list, never taken on trust.
        var d = (await applications.DraftsAsync()).FirstOrDefault(x => x.AppNo == draft);
        if (d is null) return Back();
        return Opened(await applications.FindAsync(d.AppNo));
    }

    // The upload step finds the application through the session.
    private IActionResult Opened(Application? app)
    {
        if (app is null) return Back();
        HttpContext.Session.SetCurrentApplication(app.AppNo);
        Saved = null;
        return RedirectToAction(nameof(UploadDocumentsController.Index), "UploadDocuments");
    }

    // The page again, at its bare address.
    private RedirectToActionResult Back() => RedirectToAction(nameof(Index));
}
