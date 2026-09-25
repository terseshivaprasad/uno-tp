using Microsoft.AspNetCore.Mvc;
using UnoTP.Features;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>
/// Console Admin. No sign-in in this mock: the page is open while the Admin
/// feature is on, and FeatureGate sends anyone back to the dashboard while it is off.
/// </summary>
[RequiresFeature("admin")]
public class AdminController(FeatureSet features, ConsoleState console) : Controller
{
    [HttpGet("Apps/UnoTp/Classic/Admin")]
    public async Task<IActionResult> Index() =>
        View(new AdminViewModel(features, await console.BoardAsync()));
}
