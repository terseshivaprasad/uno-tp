using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Upload Documents, the second classic wizard step (see
/// <see cref="UploadDocumentsViewModel"/>). Every post reads the application
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
[Route("Apps/UnoTp/Classic/UploadDocuments")]
public class UploadDocumentsController(
    IApplicationApi applications,
    IDocumentApi documents,
    ISourcingApi sourcing,
    IServiceProvider services) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } model) return Start();
        // Said once: a reload after it shows the page as it stands.
        model.Shown = HttpContext.Session.Read<Flash>(FlashKey(model));
        HttpContext.Session.Write<Flash>(FlashKey(model), null);
        model.Brokers = await sourcing.BrokersAsync();
        model.Staff = await sourcing.StaffAsync();
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
        if (doc is null || doc.Before || model.DmsOf(slot) is not { } at) return NotFound();
        var copy = await documents.CopyAsync(model.AppNo, at.Holder, at.Slot);
        if (copy is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = "inline";
        return File(copy.Bytes, doc.ContentType is "application/pdf" or "image/jpeg" ? doc.ContentType : "application/octet-stream");
    }

    /// <summary>A choice that reshapes the step: kept, and the page redrawn around it.</summary>
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

    /// <summary>The name typed from the investor's PAN card, put to NSDL again.</summary>
    [HttpPost("nsdl")]
    public Task<IActionResult> Nsdl(UploadForm form) =>
        ChangeAsync(form, model => model.RetryNsdlAsync(model.Investor, Request.Form["nsdlName"]));

    [HttpPost("ckyc")]
    public Task<IActionResult> Ckyc(UploadForm form) => Change(form, model => model.Ckyc());

    // Proceed says what is missing where it is missing, and the page comes back to
    // the first of it; with nothing missing it goes on to Investor Information.
    [HttpPost("proceed")]
    public Task<IActionResult> Proceed(UploadForm form) => Change(form, model => model.Proceed());

    // The backend only ever finds the partner's own application, so a number in
    // the session that is not theirs finds nothing.
    private async Task<UploadDocumentsViewModel?> LoadAsync()
    {
        var appNo = HttpContext.Session.CurrentApplication();
        var app = appNo is null ? null : await applications.FindAsync(appNo);
        return app is null ? null : await ActivatorUtilities.CreateInstance<UploadDocumentsViewModel>(services, app, HttpContext.Session).ReadyAsync();
    }

    // Every post reads the application afresh, changes it, and saves it back
    // against the version it read. A save refused because the application changed
    // in between - another tab, a second click - keeps nothing, and the page says
    // so. The work returns where on the page to come back to.
    private async Task<IActionResult> ChangeAsync(UploadForm form, Func<UploadDocumentsViewModel, Task<string?>> work)
    {
        if (await LoadAsync() is not { } model) return Start();
        model.Posted = form;
        var at = await work(model);
        if (await applications.SaveUploadAsync(model.AppNo, model.App.Version, model.State) is null)
        {
            model.Said = new Flash { Banner = "This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again." };
            at = null;
        }
        else
        {
            await model.SettleAsync();
        }
        if (model.Said is not null) HttpContext.Session.Write(FlashKey(model), model.Said);
        return model.Complete && model.Said is null
            ? RedirectToAction(nameof(InvestorInfoController.Index), "InvestorInfo")
            : Back(at);
    }

    private Task<IActionResult> Change(UploadForm form, Func<UploadDocumentsViewModel, string?> work) =>
        ChangeAsync(form, model => Task.FromResult(work(model)));

    // Back to the page for the same investor, at the part the post was about.
    private RedirectToActionResult Back(string? at) => RedirectToAction(nameof(Index), null, null, at);

    // With no application in the session there is nothing to show: the partner
    // starts at Investor Identification, which opens one.
    private RedirectToActionResult Start() => RedirectToAction(nameof(InvestorIdentificationController.Index), "InvestorIdentification");

    private static string FlashKey(UploadDocumentsViewModel model) => "flash:" + model.AppNo;
}
