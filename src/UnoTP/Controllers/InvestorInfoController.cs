using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Investor Information (see <see cref="InvestorInfoViewModel"/>). The page is one
/// form, and every post carries all of it: what was typed is kept in the session
/// first, then the post does its one thing - a joint holder's identification step,
/// a joint holder's document, adding or removing a card, Proceed - and redirects
/// back, so nothing typed is lost to a post about something else.
///
/// A joint holder's PAN copy, photograph, proof of address and communication address
/// proof go through Upload Documents' own model, against the same
/// application: read afresh for every post, changed, and saved back against the
/// version read, with DMS following once the save has gone through.
/// </summary>
[RequiresFeature("new-fd")]
[RequestSizeLimit(12 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
[Route("Apps/UnoTp/Application/InvestorInfo")]
public class InvestorInfoController(
    HolderSearch search,
    IApplicationApi applications,
    IServiceProvider services) : Controller
{
    private const string AlreadyOn = "This PAN is already on the application";

    private const string Changed =
        "This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again.";

    // One page's state per application the session is on.
    private string Key => "investor-info:" + HttpContext.Session.CurrentApplication();

    private InvestorInfoState State
    {
        get => HttpContext.Session.Read<InvestorInfoState>(Key) ?? new();
        set => HttpContext.Session.Write(Key, value);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await LoadAsync() is not { } docs) return Start();
        docs.Shown = HttpContext.Session.Read<Flash>(FlashKey(docs));
        HttpContext.Session.Write<Flash>(FlashKey(docs), null);

        var state = State;
        Recover(state, docs);
        var model = new InvestorInfoViewModel(state, docs)
        {
            Offline = TempData["offline"] is true,
            Unfinished = TempData["unfinished"] as int?,
            PepMissing = (TempData["pep"] as string ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(),
            Focus = docs.Shown?.Focus ?? TempData["focus"] as string,
        };

        // Each joint holder checked from what the session holds. A PAN already on the
        // application - the investor's, or the other joint holder's - is turned back.
        var taken = new List<string> { docs.App.Holder.Pan };
        for (var i = 0; i < state.Joint.Count; i++)
        {
            var holder = search.NewModel();
            state.Joint[i] = await search.ShowAsync(holder, state.Joint[i]) ?? state.Joint[i];
            if (holder.Record is { } record)
            {
                if (taken.Contains(record.Pan)) holder.Reject(AlreadyOn);
                else taken.Add(record.Pan);
            }
            model.Joint.Add(holder);
        }
        State = state;
        return View(model);
    }

    // A holder who is a tax or permanent resident of another country cannot invest
    // online - the investor's card asks it of every holder - every joint holder
    // opened has to be added or removed, and every one added needs their documents.
    [HttpPost("")]
    public async Task<IActionResult> Proceed(IFormCollection form)
    {
        var state = Keep(form);
        var page = new InvestorInfoViewModel(state, null);
        if (page.On("Holder1.FatcaTaxResident") || page.On("Holder1.FatcaPermanentResident"))
        {
            TempData["offline"] = true;
            return Back("ciiFatcaAlert");
        }
        var unfinished = state.Joint.FindIndex(j => !j.Added);
        if (unfinished >= 0)
        {
            TempData["unfinished"] = unfinished + 2;
            return Back($"holder-{unfinished + 2}");
        }

        if (await LoadAsync() is not { } docs) return Start();

        // Every holder with no folio says whether they are, or are related to, a
        // politically exposed person.
        var holders = docs.JointHolders.Select(h => (int.Parse(h.Code), h)).Prepend((1, docs.Investor));
        if (InvestorInfoViewModel.PepUnanswered(state, holders) is [var first, ..] pep)
        {
            TempData["pep"] = string.Join(',', pep);
            return Back("pep-" + first);
        }

        docs.KeepJoint(form);
        var at = docs.ProceedJoint();
        if (!await SaveAsync(docs)) at = null;
        return docs.Said is null
            ? RedirectToAction(nameof(ApplicationController.BankDetails), "Application")
            : Back(at);
    }

    /// <summary>Clear All: the page as it opened, and no joint holder left on the application.</summary>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear()
    {
        if (await LoadAsync() is not { } docs) return Start();
        // The third first, so the second going does not move them up on the way.
        foreach (var h in docs.JointHolders.Reverse().ToList()) docs.RemoveJoint(h.Code);
        if (await SaveAsync(docs)) HttpContext.Session.Remove(Key);
        return Back(null);
    }

    // ----- Joint holders -----------------------------------------------------

    /// <summary>Opens the next joint holder's card, once every one before is added.</summary>
    [HttpPost("joint/add")]
    public IActionResult JointAdd(IFormCollection form)
    {
        var state = Keep(form);
        if (!new InvestorInfoViewModel(state, null).CanAddJoint) return Back(null);
        state.Joint.Add(new SearchState("pan", null, null, null, null, null, Checked: false));
        State = state;
        return Back($"holder-{state.Joint.Count + 1}");
    }

    /// <summary>
    /// Check record, by PAN and date of birth: a joint holder has no folio search. One
    /// the register holds, or a PAN with no folio, is added there and then, under
    /// their holder type - a PAN with no folio is put to NSDL once their PAN copy is
    /// filed. A PAN already on the application is turned back.
    /// </summary>
    [HttpPost("joint/{n:int}/check")]
    public async Task<IActionResult> JointCheck(int n, IFormCollection form)
    {
        var state = Keep(form);
        var i = n - 2;
        if (i < 0 || i >= state.Joint.Count || state.Joint[i].Added) return Back(null);
        var p = $"Joint{n}.";
        state.Joint[i] = HolderSearch.Check(new SearchForm("pan", form[p + "Pan"], form[p + "Dd"], form[p + "Mm"], form[p + "Yyyy"], null));
        State = state;

        if (await search.FoundAsync(state.Joint[i]) is not { Record: { } record }) return Back($"holder-{n}");
        if (await LoadAsync() is not { } docs) return Start();
        var taken = new List<string> { docs.App.Holder.Pan };
        taken.AddRange(docs.JointHolders.Select(h => h.Who.Pan));
        if (taken.Contains(record.Pan)) return Back($"holder-{n}");

        docs.AddJoint(InvestorInfoViewModel.CodeOf(n), HolderSearch.ApplicationHolder(record));
        if (await SaveAsync(docs))
        {
            state.Joint[i] = state.Joint[i] with { Added = true };
            State = state;
        }
        return Back($"holder-{n}");
    }

    /// <summary>
    /// Takes a joint holder off, and all they carried: what was typed for them, and
    /// their documents. Only the last one opened: the second holder cannot go while
    /// there is a third, so the third never has to become the second.
    /// </summary>
    [HttpPost("joint/{n:int}/remove")]
    public async Task<IActionResult> JointRemove(int n, IFormCollection form)
    {
        var state = Keep(form);
        var i = n - 2;
        if (i < 0 || i != state.Joint.Count - 1) return Back($"holder-{n}");
        if (await LoadAsync() is not { } docs) return Start();
        docs.RemoveJoint(InvestorInfoViewModel.CodeOf(n));
        if (!await SaveAsync(docs)) return Back(null);

        state.Joint.RemoveAt(i);
        Drop(state, $"Holder{n}.", $"Joint{n}.");
        State = state;
        return Back(state.Joint.Count > 0 ? $"holder-{state.Joint.Count + 1}" : "ciiAddHolder");
    }

    // ----- A joint holder's documents: holder 2 or 3 -----------------------------

    /// <summary>
    /// A choice that reshapes a joint holder's documents - a proof's type, where post
    /// goes - kept, and the card redrawn around the control that changed.
    /// </summary>
    [HttpPost("joint/{n:int}/refresh")]
    public Task<IActionResult> JointRefresh(int n, IFormCollection form) =>
        JointDocsAsync(n, form, (docs, h) => Task.FromResult<string?>(form["refresh"].ToString() is { Length: > 0 } control ? control : null));

    /// <summary>
    /// A joint holder's PAN copy, photograph, proof of address or communication
    /// address proof, through Upload Documents' checks. The investor's are all on
    /// Upload Documents.
    /// </summary>
    [HttpPost("joint/{n:int}/upload")]
    public Task<IActionResult> JointUpload(int n, IFormCollection form) =>
        JointDocsAsync(n, form, (docs, h) =>
        {
            var key = form["slot"].ToString();
            return UploadDocumentsViewModel.HolderSlots.Any(d => h.Key(d.Key) == key)
                ? docs.UploadAsync(key, form.Files)
                : Task.FromResult<string?>(null);
        });

    /// <summary>The name printed on a joint holder's PAN card, put to NSDL where it did not hold the name OCR read.</summary>
    [HttpPost("joint/{n:int}/nsdl")]
    public Task<IActionResult> JointNsdl(int n, IFormCollection form) =>
        JointDocsAsync(n, form, (docs, h) => docs.RetryNsdlAsync(h, form[h.Key("nsdlName")]));

    // ----- The nominee -------------------------------------------------------

    [HttpPost("nominee/add")]
    public IActionResult NomineeAdd(IFormCollection form)
    {
        var state = Keep(form);
        state.Nominee = true;
        State = state;
        return Back("nominee");
    }

    [HttpPost("nominee/remove")]
    public IActionResult NomineeRemove(IFormCollection form)
    {
        var state = Keep(form);
        state.Nominee = false;
        Drop(state, "Nominee.");
        State = state;
        return Back("ciiAddNominee");
    }

    // ----- Keeping what was typed --------------------------------------------

    // Every post carries the whole form, so every post keeps it. A joint holder not
    // yet checked keeps what was typed into their search fields too.
    private InvestorInfoState Keep(IFormCollection form)
    {
        var state = State;
        state.Fields.Clear();
        foreach (var (name, value) in form)
        {
            if (name != "__RequestVerificationToken") state.Fields[name] = value.ToString();
        }
        for (var i = 0; i < state.Joint.Count; i++)
        {
            var p = $"Joint{i + 2}.";
            if (!state.Joint[i].Checked && form.ContainsKey(p + "Pan"))
                state.Joint[i] = state.Joint[i] with { Pan = form[p + "Pan"], Dd = form[p + "Dd"], Mm = form[p + "Mm"], Yyyy = form[p + "Yyyy"] };
        }
        State = state;
        return state;
    }

    // A session that lost the page - it expired, or the partner came back in another
    // - finds the joint holders the application already carries, added.
    private static void Recover(InvestorInfoState state, UploadDocumentsViewModel docs)
    {
        if (state.Joint.Count > 0) return;
        foreach (var h in docs.JointHolders)
        {
            var dob = h.Who.Dob.Split('-');
            state.Joint.Add(new SearchState("pan", h.Who.Pan, dob.ElementAtOrDefault(0), dob.ElementAtOrDefault(1), dob.ElementAtOrDefault(2), null,
                Checked: true, Added: true));
        }
    }

    // A post about a joint holder's documents: the application read afresh, what
    // every card posted about its documents kept, the step done, and all of it saved
    // back against the version read.
    private async Task<IActionResult> JointDocsAsync(int n, IFormCollection form, Func<UploadDocumentsViewModel, UploadDocumentsViewModel.DocHolder, Task<string?>> step)
    {
        Keep(form);
        if (await LoadAsync() is not { } docs) return Start();
        if (docs.JointHolder(InvestorInfoViewModel.CodeOf(n)) is not { } h) return Back($"holder-{n}");
        docs.KeepJoint(form);
        var at = await step(docs, h);
        if (!await SaveAsync(docs)) at = null;
        return Back(at ?? $"holder-{n}");
    }

    // The backend only ever finds the partner's own application.
    private async Task<UploadDocumentsViewModel?> LoadAsync()
    {
        var appNo = HttpContext.Session.CurrentApplication();
        var app = appNo is null ? null : await applications.FindAsync(appNo);
        return app is null ? null : ActivatorUtilities.CreateInstance<UploadDocumentsViewModel>(services, app, HttpContext.Session);
    }

    // Saved against the version read, then DMS brought in line. A save refused
    // because the application changed in between keeps nothing, and says so. What
    // the post has to say is kept for the page it redirects to.
    private async Task<bool> SaveAsync(UploadDocumentsViewModel docs)
    {
        var saved = await applications.SaveUploadAsync(docs.AppNo, docs.App.Version, docs.State) is not null;
        if (saved) await docs.SettleAsync();
        else docs.Said = new Flash { Banner = Changed };
        if (docs.Said is not null) HttpContext.Session.Write(FlashKey(docs), docs.Said);
        return saved;
    }

    private static void Drop(InvestorInfoState state, params string[] prefixes)
    {
        foreach (var name in state.Fields.Keys.Where(k => prefixes.Any(k.StartsWith)).ToList()) state.Fields.Remove(name);
    }

    // Back to the page, at the part the post was about.
    private RedirectToActionResult Back(string? at)
    {
        if (at is not null) TempData["focus"] = at;
        return RedirectToAction(nameof(Index), null, null, at);
    }

    // With no application in the session there is nothing to show: the partner
    // starts at Investor Identification, which opens one.
    private RedirectToActionResult Start() => RedirectToAction(nameof(InvestorIdentificationController.Index), "InvestorIdentification");

    private static string FlashKey(UploadDocumentsViewModel docs) => "flash-info:" + docs.AppNo;
}
