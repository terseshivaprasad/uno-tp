using Microsoft.AspNetCore.Mvc;
using UnoTP.Backend;
using UnoTP.Features;
using UnoTP.ViewModels;

namespace UnoTP.Controllers;

[RequiresFeature("pis")]
public class PayInSlipController(IPayInSlipApi slips) : Controller
{
    [HttpGet("Apps/UnoTp/Classic/PayInSlip")]
    public async Task<IActionResult> Index() => View(new PayInSlipViewModel(await slips.SlipsAsync()));
}
