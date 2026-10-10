using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// Investor Identification, the first classic wizard step, for a new deposit and a
/// renewal alike: the primary holder is taken through <see cref="HolderSearch"/>,
/// and the deposits they hold are listed under their record. Proceed opens a new
/// deposit's application; Renew on a deposit that is due opens its renewal
/// (<see cref="RenewModel"/>). Every step is a post, and every post
/// redirects back to the bare page address: what was typed and what the check found
/// are kept in the browser's encrypted TempData cookie until Proceed - never in the
/// address, so no PAN, date of birth or name reaches a log, the history or a
/// Referer header, and never in the server's session. Proceed opens the
/// application in the backend, which keeps it and everything on it from then on.
/// </summary>
[RequiresFeature("new-fd", "renew")]
public class NewApplicationModel(
    IApplicationApi applications,
    HolderSearch search,
    IRenewalApi renewals,
    FeatureSet features,
    ConsoleState console,
    Lookups lookups) : PageModel
{
    /// <summary>What the page shows.</summary>
    public NewApplicationViewModel View { get; private set; } = null!;

    internal const string SearchKey = "search";

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
    public async Task<IActionResult> OnGetAsync()
    {
        var model = search.NewModel();
        model.Drafts = await applications.DraftsAsync();
        Saved = await search.ShowAsync(model, Saved);
        // The page serves a new deposit and a renewal, each switched on and off on its
        // own: whichever is off is shown disabled, saying why.
        var board = await console.BoardAsync();
        model.NewFdOff = DashboardViewModel.ClosedLine(features, board, "new-fd");
        model.Deposits = await DepositsOfAsync(model.Record, DashboardViewModel.ClosedLine(features, board, "renew"));
        model.Said = TempData[RenewModel.SaidKey] as string;
        View = model;
        return Page();
    }

    // The deposits the investor found holds: by their folio, or - with none - by their
    // PAN and date of birth. Nothing to list for one with none.
    private async Task<DepositsBlock?> DepositsOfAsync(NewApplicationViewModel.Holder? record, string? renewOff)
    {
        if (record is null) return null;

        IReadOnlyList<HeldDeposit>? held;
        if (record.Folio.Length > 0) held = await renewals.DepositsByFolioAsync(record.Folio);
        else held = await renewals.DepositsByPanAsync(record.Pan, record.Dob);

        if (held is null || held.Count == 0) return null;
        return new DepositsBlock(held, await lookups.ReferenceAsync(), await lookups.ConfigAsync(), renewOff);
    }

    /// <summary>Check record: what was typed is kept, and the page checks it.</summary>
    public IActionResult OnPostCheck(SearchForm form)
    {
        Saved = HolderSearch.Check(form);
        return Back();
    }

    /// <summary>Search again: back to the fields, still holding what was typed.</summary>
    public IActionResult OnPostAgain()
    {
        Saved = HolderSearch.Again(Saved);
        return Back();
    }

    /// <summary>Clear All: nothing typed, nothing found.</summary>
    public IActionResult OnPostClear()
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
    [RequiresFeature("new-fd")]
    public async Task<IActionResult> OnPostProceedAsync()
    {
        if (await search.FoundAsync(Saved) is not { Record: { } record }) return Back();

        // Every search that is proceeded from opens an application of its own, and
        // the backend gives it its number. A PAN copy already on it is not asked again.
        return Opened(await applications.OpenAsync(HolderSearch.ApplicationHolder(record)));
    }

    /// <summary>A draft picked up again, under the number it was opened with.</summary>
    public async Task<IActionResult> OnPostContinueAsync(string? draft)
    {
        // Only one of this partner's own drafts: the number posted is checked
        // against the list, never taken on trust.
        var d = (await applications.DraftsAsync()).FirstOrDefault(x => x.AppNo == draft);
        if (d is null) return Back();
        return Opened(await applications.FindAsync(d.AppNo));
    }

    /// <summary>
    /// A draft cancelled from the list of incomplete applications: it leaves the list
    /// and is not opened again. Only one of this partner's own drafts - the backend
    /// checks. Goes back to the page the list was on.
    /// </summary>
    public async Task<IActionResult> OnPostCancelDraftAsync(string? draft, string? from)
    {
        var cancelled = draft is { Length: > 0 } && await applications.CancelDraftAsync(draft);
        if (from == FromDashboard) return RedirectToPage("/Dashboard/Index");
        TempData[RenewModel.SaidKey] = cancelled
            ? $"Application {draft} is cancelled."
            : Messages.InvestorIdentification.NoDraftToCancel;
        return Back();
    }

    /// <summary>What the dashboard's list posts as "from", so a cancel goes back to it.</summary>
    public const string FromDashboard = "dashboard";

    // The upload step finds the application by the number in its address.
    private IActionResult Opened(Application? app)
    {
        if (app is null) return Back();
        Saved = null;
        return RedirectToPage("/Documents/Index", new { appNo = app.AppNo });
    }

    // The page again, at its bare address.
    private RedirectToPageResult Back() => RedirectToPage();
}
