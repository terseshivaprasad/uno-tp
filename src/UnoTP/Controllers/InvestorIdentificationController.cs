using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Investor Identification, the first classic wizard step: the primary holder is
/// taken through <see cref="HolderSearch"/>. Every step is a post, and every post
/// redirects back to the bare page address: what was typed and what the check found
/// are kept in the browser's encrypted TempData cookie until Proceed - never in the
/// address, so no PAN, date of birth or name reaches a log, the history or a
/// Referer header, and never in the server's session. Proceed opens the
/// application in the backend, which keeps it and everything on it from then on.
/// </summary>
[RequiresFeature("new-fd")]
[Route("Purchase/InvestorIdentification")]
public class InvestorIdentificationController(
    FeatureSet features,
    IDemoApi demo,
    IApplicationApi applications,
    HolderSearch search) : Controller
{
    private const string SearchKey = "search";

    // Peeked, not read, so it stays until the page replaces or clears it.
    private SearchState? Saved
    {
        get => TempData.Peek(SearchKey) is string json ? JsonSerializer.Deserialize<SearchState>(json) : null;
        set
        {
            if (value is null) TempData.Remove(SearchKey);
            else TempData[SearchKey] = JsonSerializer.Serialize(value);
        }
    }

    /// <summary>The page as the session left it: blank, being filled in, or checked.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var model = search.NewModel();
        model.Demo = features.Flags.DemoData ? await demo.CasesAsync() : null;
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

    /// <summary>
    /// Opens the application and carries the holder to the upload step, once the
    /// register has found them or holds no folio against their PAN - whose PAN copy
    /// is then filed and put to NSDL on the upload step. The application is held by
    /// the backend; the upload step finds it by the number in its address.
    /// </summary>
    [HttpPost("proceed")]
    public async Task<IActionResult> Proceed()
    {
        if (await search.FoundAsync(Saved) is not { Record: { } record }) return Back();

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

    // The upload step finds the application by the number in its address.
    private IActionResult Opened(Application? app)
    {
        if (app is null) return Back();
        Saved = null;
        return RedirectToAction(nameof(UploadDocumentsController.Index), "UploadDocuments", new { appNo = app.AppNo });
    }

    // The page again, at its bare address.
    private RedirectToActionResult Back() => RedirectToAction(nameof(Index));
}
