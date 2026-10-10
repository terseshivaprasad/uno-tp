using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// Console Admin. No sign-in in this mock: the page is open while the Admin
/// feature is on, and FeatureGate sends anyone back to the dashboard while it is off.
/// A window or a notice is the backend's to keep: the page checks it before it is
/// sent, the backend mints its id and records who set it, and the page comes back
/// with the schedule as the backend now holds it.
/// </summary>
[RequiresFeature("admin")]
public class AdminModel(FeatureSet features, ConsoleState console, IConsoleApi consoleApi, Lookups lookups) : PageModel
{
    /// <summary>What the page shows.</summary>
    public AdminViewModel View { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Toast"] = TempData["toast"];
        View = new AdminViewModel(features, await console.BoardAsync()) { NoticeKinds = (await lookups.ReferenceAsync()).NoticeKinds };
        return Page();
    }

    /// <summary>Takes the chosen tiles off for a window, told in the bell when it has a notice.</summary>
    public async Task<IActionResult> OnPostAddWindowAsync(string[]? features, string? from, string? to, string? notice)
    {
        var known = (await console.BoardAsync()).Tiles.Select(f => f.Key).ToHashSet();
        var picked = (features ?? []).Where(known.Contains).Distinct().ToList();
        if (picked.Count == 0 || ParseLocalDateTime(from) is not { } start || ParseLocalDateTime(to) is not { } end || end <= start || start < DateTime.Now.AddMinutes(-1))
        {
            TempData["toast"] = Messages.Admin.WindowNotSet;
            return RedirectToPage();
        }
        var text = (notice ?? "").Trim();
        await consoleApi.AddWindowAsync(new NewWindow(picked, start, end, text));
        TempData["toast"] = text.Length > 0
            ? "Window set, and partners can see it in the bell."
            : "Window set. Partners are not told, so only the tile will say it is off.";
        return RedirectToPage();
    }

    /// <summary>A notice every partner sees in the bell.</summary>
    public async Task<IActionResult> OnPostAddNoticeAsync(string? kind, string? at, string? title, string? detail)
    {
        var head = (title ?? "").Trim();
        if (ParseLocalDateTime(at) is not { } moment || moment < DateTime.Now.AddMinutes(-1) || head.Length == 0 || string.IsNullOrWhiteSpace(kind))
        {
            TempData["toast"] = Messages.Admin.NoticeNotPublished;
            return RedirectToPage();
        }
        await consoleApi.AddAnnouncementAsync(new NewAnnouncement(kind.Trim(), head, moment, (detail ?? "").Trim()));
        TempData["toast"] = "Published. Every partner sees it in the bell now.";
        return RedirectToPage();
    }

    /// <summary>Ends a window that is on now, or cancels one still to come; its notice goes with it.</summary>
    public async Task<IActionResult> OnPostEndWindowAsync(string id, bool live)
    {
        TempData["toast"] = await consoleApi.EndWindowAsync(id)
            ? live ? "Window ended. The tiles it took off are back on for every partner." : "Window cancelled. Nothing goes off, and its notice is out of the bell."
            : Messages.Admin.WindowAlreadyEnded;
        return RedirectToPage();
    }

    /// <summary>Takes a notice out of the bell.</summary>
    public async Task<IActionResult> OnPostRemoveNoticeAsync(string id)
    {
        TempData["toast"] = await consoleApi.RemoveAnnouncementAsync(id)
            ? "Notice removed. Partners no longer see it in the bell."
            : Messages.Admin.NoticeAlreadyGone;
        return RedirectToPage();
    }

    // A moment as the page posts it: yyyy-MM-ddTHH:mm, local time.
    private static DateTime? ParseLocalDateTime(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : null;
}
