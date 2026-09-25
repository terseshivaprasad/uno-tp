using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

[RequiresFeature("view-app")]
public class ViewApplicationController(IApplicationApi applications, Lookups lookups) : Controller
{
    [HttpGet("Apps/UnoTp/Classic/ViewApplication")]
    public async Task<IActionResult> Index() =>
        View(new ViewApplicationViewModel(await applications.ListAsync(), (await lookups.ConfigAsync()).CancellationDays));
}
