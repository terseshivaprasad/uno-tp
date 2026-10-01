using Microsoft.AspNetCore.Mvc;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

[RequiresFeature("view-app")]
public class ApplicationsController(IApplicationApi applications, Lookups lookups) : Controller
{
    [HttpGet("ViewApplication")]
    public async Task<IActionResult> Index() =>
        View(new ApplicationsViewModel(await applications.ListAsync(), (await lookups.ConfigAsync()).CancellationDays));
}
