using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Upload Documents, the second classic wizard step (see
/// <see cref="DocumentsViewModel"/>). Every post reads the application
/// afresh, changes it, and saves it back against the version it read, then
/// redirects back to the page, so a refresh never posts twice.
///
/// The address carries nothing: the application is the one the session is on,
/// opened by Investor Identification, and only ever the owner's own. Reached with
/// none, the step sends the partner to Investor Identification to open one.
/// </summary>
[RequiresFeature("new-fd")]
// One document per post, the largest 4 MB, with the form around it.
[RequestSizeLimit(12 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
[Route("UploadInvestorDocuments/{appNo}")]
public class DocumentsController(
    IApplicationApi applications,
    IDocumentApi documents,
    ISourcingApi sourcing,
    IServiceProvider services) : Controller
{
    /// <summary>
    /// A register searched as a code is typed (documents.js): the brokers or the staff
    /// whose code or name holds the text, as the backend finds them. Nothing is looked
    /// up until three characters are typed. The staff are searched among those the
    /// staff rule of the sourcing mode chosen on the page takes.
    /// </summary>
    [HttpGet("sourcing")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Sourcing(string? register, string? q, string? mode, [FromServices] UnoTP.Infrastructure.Lookups lookups)
    {
        if (HttpContext.CurrentApplication() is null) return NotFound();
        var text = (q ?? "").Trim();
        if (!TypedSearch.LongEnough(text)) return Json(Array.Empty<Party>());
        text = text[..Math.Min(text.Length, 40)];
        if (register == "brokers") return Json(await sourcing.SearchBrokersAsync(text));
        if (register != "staff") return NotFound();

        var modes = (await lookups.ReferenceAsync()).SourcingModes;
        var chosen = modes.FirstOrDefault(m => m.Code == mode);
        return Json(await sourcing.SearchStaffAsync(text, DocumentsViewModel.StaffRuleOf(chosen)));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } model) return Start();
        // Said once: a reload after it shows the page as it stands.
        model.Shown = TempData[FlashKey(model)] is string said ? JsonSerializer.Deserialize<Flash>(said) : null;
        return View(model);
    }

    /// <summary>A copy of a filed document, for the preview, from DMS.</summary>
    // Only the session's own application, so a guessed address opens nothing. The
    // copy is someone's KYC: nothing along the way may keep it, and the browser
    // shows it as the type it was checked to be and nothing else.
    [HttpGet("file")]
    public async Task<IActionResult> Copy(string slot)
    {
        if (await LoadAsync() is not { } model) return NotFound();
        var doc = model.State.Docs.GetValueOrDefault(slot);
        if (doc is null || doc.Before) return NotFound();
        var copy = await documents.CopyAsync(model.AppNo, doc.FileName);
        if (copy is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = "inline";
        return File(copy.Bytes, doc.ContentType is "application/pdf" or "image/jpeg" ? doc.ContentType : "application/octet-stream");
    }

    /// <summary>A choice that reshapes the step: kept, and the page redrawn around it.</summary>
    /// <summary>
    /// What the upload under way is doing now - which outside service it is waiting
    /// on - for the wait on the screen to say. The page asks once a second while it
    /// waits (partial-forms.js); with nothing under way the answer is empty.
    /// </summary>
    [HttpGet("progress")]
    public IActionResult Progress([FromServices] UploadProgress progress)
    {
        var appNo = HttpContext.CurrentApplication();
        var stage = appNo is null ? null : progress.Of(UploadProgress.SessionOf(HttpContext.Session), appNo);
        return Json(new { stage = stage ?? "" });
    }

    [HttpPost("refresh")]
    public Task<IActionResult> Refresh(UploadForm form) => Change(form, model =>
    {
        model.Keep();
        return form.Refresh;
    });

    [HttpPost("save")]
    public Task<IActionResult> Save(UploadForm form) => Change(form, model =>
    {
        model.Keep();
        model.State.SavedAt = DateTime.Now;
        return null;
    });

    [HttpPost("upload")]
    public Task<IActionResult> Upload(UploadForm form) =>
        ChangeAsync(form, model => model.UploadAsync(Request.Form.Files));

    /// <summary>NSDL asked again about the investor's PAN copy already filed, with the name typed from the card where NSDL did not match it.</summary>
    [HttpPost("nsdl")]
    public Task<IActionResult> Nsdl(UploadForm form) =>
        ChangeAsync(form, model => model.RetryNsdlAsync(model.Investor, form.NsdlName));

    /// <summary>The investor's 12-digit Aadhaar number, typed where OCR could not read it, for the PAN-Aadhaar link.</summary>
    [HttpPost("aadhaar-number")]
    public Task<IActionResult> AadhaarNumber(UploadForm form) =>
        ChangeAsync(form, model => model.AadhaarNumberAsync(model.Investor, form.AadhaarNo));

    /// <summary>Fetch from CKYC: CERSAI is searched for the investor's record, which then stands in for the proofs.</summary>
    [HttpPost("ckyc")]
    public Task<IActionResult> Ckyc(UploadForm form) => ChangeAsync(form, model => model.CkycAsync());

    // Proceed says what is missing where it is missing, and the page comes back to
    // the first of it; with nothing missing it goes on to Investor Information.
    [HttpPost("proceed")]
    public Task<IActionResult> Proceed(UploadForm form) => Change(form, model => model.Proceed());

    // The backend only ever finds the partner's own application, so a number in
    // the address that is not theirs finds nothing.
    private async Task<DocumentsViewModel?> LoadAsync()
    {
        var appNo = HttpContext.CurrentApplication();
        var app = appNo is null ? null : await applications.FindAsync(appNo);
        return app is null ? null : await ActivatorUtilities.CreateInstance<DocumentsViewModel>(services, app, HttpContext.Session).ReadyAsync();
    }

    // Every post reads the application afresh, changes it, and saves it back
    // against the version it read. A save refused because the application changed
    // in between - another tab, a second click - keeps nothing, and the page says
    // so. The work returns where on the page to come back to.
    private async Task<IActionResult> ChangeAsync(UploadForm form, Func<DocumentsViewModel, Task<string?>> work)
    {
        if (await LoadAsync() is not { } model) return Start();
        model.Posted = form;
        var at = await work(model);
        if (await applications.SaveUploadAsync(model.AppNo, model.App.Version, model.State) is null)
        {
            model.Said = new Flash { Banner = "This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again.", BannerIsError = true };
            at = null;
        }
        if (model.Said is not null) TempData[FlashKey(model)] = JsonSerializer.Serialize(model.Said);
        return model.Complete && model.Said is null
            ? RedirectToAction(nameof(InvestorController.Index), "Investor")
            : Back(at);
    }

    private Task<IActionResult> Change(UploadForm form, Func<DocumentsViewModel, string?> work) =>
        ChangeAsync(form, model => Task.FromResult(work(model)));

    // Back to the page for the same investor, at the part the post was about.
    private RedirectToActionResult Back(string? at) => RedirectToAction(nameof(Index), null, null, at);

    // With no application in the session there is nothing to show: the partner
    // starts at Investor Identification, which opens one.
    private RedirectToActionResult Start() => RedirectToAction(nameof(NewApplicationController.Index), "NewApplication");

    private static string FlashKey(DocumentsViewModel model) => "flash:" + model.AppNo;
}
