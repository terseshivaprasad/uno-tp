using Microsoft.AspNetCore.Mvc;
using UnoTP.Features;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

/// <summary>The classic dashboard. <c>off</c> names a feature FeatureGate closed on the way here.</summary>
public class DashboardController(FeatureSet features, ConsoleState console) : Controller
{
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Index(string? off) =>
        View(new DashboardViewModel(features, await console.BoardAsync(), off));
}
