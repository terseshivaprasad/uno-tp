using Microsoft.AspNetCore.Mvc;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Investor Information (see <see cref="InvestorInfoViewModel"/>). The page is one
/// form, and every post carries all of it: what was typed is kept in the session
/// first, then the post does its one thing - a joint holder's identification step,
/// adding or removing a card, Proceed - and redirects back, so nothing typed is lost
/// to a post about something else.
/// </summary>
[RequiresFeature("new-fd")]
[RequestSizeLimit(12 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
[Route("Apps/UnoTp/Application/InvestorInfo")]
public class InvestorInfoController(HolderSearch search) : Controller
{
    private const string AlreadyOn = "This PAN is already on the application";

    // One page's state per application the session is on.
    private string Key => "investor-info:" + (HttpContext.Session.CurrentApplication ?? "sample");

    private InvestorInfoState State
    {
        get => HttpContext.Session.Read<InvestorInfoState>(Key) ?? new();
        set => HttpContext.Session.Write(Key, value);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var state = State;
        var model = new InvestorInfoViewModel(state)
        {
            Offline = TempData["offline"] is true,
            Unfinished = TempData["unfinished"] as int?,
            Focus = TempData["focus"] as string,
        };

        // Each joint holder checked from what the session holds. A PAN already on the
        // application - the investor's, or the other joint holder's - is turned back.
        var taken = new List<string> { InvestorInfoViewModel.PrimaryPan };
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
    // online, and every joint holder opened has to be added or removed first.
    [HttpPost("")]
    public IActionResult Proceed(IFormCollection form)
    {
        var state = Keep(form);
        var fatca = state.Fields.Any(f => (f.Key.EndsWith(".FatcaTaxResident") || f.Key.EndsWith(".FatcaPermanentResident"))
            && f.Value is "on" or "true");
        if (fatca)
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
        return RedirectToAction(nameof(ApplicationController.BankDetails), "Application");
    }

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(Key);
        return Back(null);
    }

    // ----- Joint holders -----------------------------------------------------

    /// <summary>Opens the next joint holder's card, once every one before is added.</summary>
    [HttpPost("joint/add")]
    public IActionResult JointAdd(IFormCollection form)
    {
        var state = Keep(form);
        if (!new InvestorInfoViewModel(state).CanAddJoint) return Back(null);
        state.Joint.Add(new SearchState("pan", null, null, null, null, null, Checked: false));
        State = state;
        return Back($"holder-{state.Joint.Count + 1}");
    }

    /// <summary>Check record, by PAN and date of birth: a joint holder has no folio search.</summary>
    [HttpPost("joint/{n:int}/check")]
    public Task<IActionResult> JointCheck(int n, IFormCollection form) => JointAsync(n, form, (state, i) =>
    {
        var p = $"Joint{n}.";
        state.Joint[i] = HolderSearch.Check(new SearchForm("pan", form[p + "Pan"], form[p + "Dd"], form[p + "Mm"], form[p + "Yyyy"], null));
        return Task.CompletedTask;
    });

    [HttpPost("joint/{n:int}/again")]
    public Task<IActionResult> JointAgain(int n, IFormCollection form) => JointAsync(n, form, (state, i) =>
    {
        state.Joint[i] = HolderSearch.Again(state.Joint[i])!;
        return Task.CompletedTask;
    });

    [HttpPost("joint/{n:int}/verify")]
    public Task<IActionResult> JointVerify(int n, IFormCollection form) => JointAsync(n, form, async (state, i) =>
        state.Joint[i] = (await search.VerifyAsync(state.Joint[i], form.Files.GetFile($"Joint{n}.Copy")))!);

    /// <summary>Adds an identified joint holder to the application.</summary>
    [HttpPost("joint/{n:int}/continue")]
    public Task<IActionResult> JointContinue(int n, IFormCollection form) => JointAsync(n, form, async (state, i) =>
    {
        if (await search.IdentifiedAsync(state.Joint[i]) is not { Record: { } record }) return;
        var taken = new List<string> { InvestorInfoViewModel.PrimaryPan };
        for (var other = 0; other < state.Joint.Count; other++)
        {
            if (other != i && state.Joint[other].Added && state.Joint[other].Pan is { } pan) taken.Add(pan.ToUpperInvariant());
        }
        if (!taken.Contains(record.Pan)) state.Joint[i] = state.Joint[i] with { Added = true };
    });

    /// <summary>
    /// Takes a joint holder off, and all they carried. The third holder, if there is
    /// one, becomes the second.
    /// </summary>
    [HttpPost("joint/{n:int}/remove")]
    public IActionResult JointRemove(int n, IFormCollection form)
    {
        var state = Keep(form);
        var i = n - 2;
        if (i < 0 || i >= state.Joint.Count) return Back(null);
        state.Joint.RemoveAt(i);
        Drop(state, $"Holder{n}.", $"Joint{n}.");
        for (var later = n + 1; later <= InvestorInfoViewModel.MaxJoint + 1; later++)
        {
            Rename(state, $"Holder{later}.", $"Holder{later - 1}.");
            Rename(state, $"Joint{later}.", $"Joint{later - 1}.");
        }
        State = state;
        return Back(state.Joint.Count > 0 ? $"holder-{state.Joint.Count + 1}" : "ciiAddHolder");
    }

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

    private async Task<IActionResult> JointAsync(int n, IFormCollection form, Func<InvestorInfoState, int, Task> step)
    {
        var state = Keep(form);
        var i = n - 2;
        if (i < 0 || i >= state.Joint.Count || state.Joint[i].Added) return Back(null);
        await step(state, i);
        State = state;
        return Back($"holder-{n}");
    }

    private static void Drop(InvestorInfoState state, params string[] prefixes)
    {
        foreach (var name in state.Fields.Keys.Where(k => prefixes.Any(k.StartsWith)).ToList()) state.Fields.Remove(name);
    }

    private static void Rename(InvestorInfoState state, string from, string to)
    {
        foreach (var name in state.Fields.Keys.Where(k => k.StartsWith(from)).ToList())
        {
            state.Fields[to + name[from.Length..]] = state.Fields[name];
            state.Fields.Remove(name);
        }
    }

    // Back to the page, at the part the post was about.
    private RedirectToActionResult Back(string? at)
    {
        if (at is not null) TempData["focus"] = at;
        return RedirectToAction(nameof(Index), null, null, at);
    }
}
