using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Console Admin. No sign-in in this mock: the page is open while the Admin
/// feature is on, and FeatureGate sends anyone back to the dashboard while it is off.
/// A window or a notice is the backend's to keep: the page checks it before it is
/// sent, the backend mints its id and records who set it, and the page comes back
/// with the schedule as the backend now holds it.
/// </summary>
[RequiresFeature("admin")]
[Route("Apps/UnoTp/Classic/Admin")]
public class AdminController(FeatureSet features, ConsoleState console, IConsoleApi consoleApi, Lookups lookups) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Toast"] = TempData["toast"];
        return View(new AdminViewModel(features, await console.BoardAsync()) { NoticeKinds = (await lookups.ReferenceAsync()).NoticeKinds });
    }

    /// <summary>Takes the chosen tiles off for a window, told in the bell when it has a notice.</summary>
    [HttpPost("window")]
    public async Task<IActionResult> AddWindow(string[]? features, string? from, string? to, string? notice)
    {
        var known = ConsoleAdmin.Features.Select(f => f.Key).ToHashSet();
        var picked = (features ?? []).Where(known.Contains).Distinct().ToList();
        if (picked.Count == 0 || When(from) is not { } start || When(to) is not { } end || end <= start || start < DateTime.Now.AddMinutes(-1))
        {
            TempData["toast"] = "The window was not set: pick a tile, and a From in the future before the To.";
            return RedirectToAction(nameof(Index));
        }
        var text = (notice ?? "").Trim();
        await consoleApi.AddWindowAsync(new NewWindow(picked, start, end, text));
        TempData["toast"] = text.Length > 0
            ? "Window set, and partners can see it in the bell."
            : "Window set. Partners are not told, so only the tile will say it is off.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>A notice every partner sees in the bell.</summary>
    [HttpPost("notice")]
    public async Task<IActionResult> AddNotice(string? kind, string? at, string? title, string? detail)
    {
        var head = (title ?? "").Trim();
        if (When(at) is not { } moment || moment < DateTime.Now.AddMinutes(-1) || head.Length == 0 || string.IsNullOrWhiteSpace(kind))
        {
            TempData["toast"] = "The notice was not published: give it a heading and a moment still to come.";
            return RedirectToAction(nameof(Index));
        }
        await consoleApi.AddAnnouncementAsync(new NewAnnouncement(kind.Trim(), head, moment, (detail ?? "").Trim()));
        TempData["toast"] = "Published. Every partner sees it in the bell now.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Ends a window that is on now, or cancels one still to come; its notice goes with it.</summary>
    [HttpPost("window/end")]
    public async Task<IActionResult> EndWindow(string id, bool live)
    {
        TempData["toast"] = await consoleApi.EndWindowAsync(id)
            ? live ? "Window ended. The tiles it took off are back on for every partner." : "Window cancelled. Nothing goes off, and its notice is out of the bell."
            : "That window has already ended.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Takes a notice out of the bell.</summary>
    [HttpPost("notice/remove")]
    public async Task<IActionResult> RemoveNotice(string id)
    {
        TempData["toast"] = await consoleApi.RemoveAnnouncementAsync(id)
            ? "Notice removed. Partners no longer see it in the bell."
            : "That notice is no longer in the bell.";
        return RedirectToAction(nameof(Index));
    }

    // A moment as the page posts it: yyyy-MM-ddTHH:mm, local time.
    private static DateTime? When(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : null;
}
