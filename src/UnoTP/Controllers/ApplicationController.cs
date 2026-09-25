using Microsoft.AspNetCore.Mvc;
using UnoTP.Features;

namespace UnoTP.Controllers;

/// <summary>
/// The wizard's later steps, after Investor Information: Bank Details &amp;
/// Payment, FD Configuration, Review Summary and Submitted. Each step's form moves
/// on to the next.
/// </summary>
[RequiresFeature("new-fd")]
[Route("Apps/UnoTp/Application")]
public class ApplicationController : Controller
{
    [HttpGet("BankDetails")]
    public IActionResult BankDetails() => View();

    [HttpPost("BankDetails")]
    public IActionResult BankDetailsPost() => RedirectToAction(nameof(FdConfiguration));

    [HttpGet("FdConfiguration")]
    public IActionResult FdConfiguration() => View();

    [HttpPost("FdConfiguration")]
    public IActionResult FdConfigurationPost() => RedirectToAction(nameof(ReviewSummary));

    [HttpGet("ReviewSummary")]
    public IActionResult ReviewSummary() => View();

    // The "Send payment link" action inside the review's dialog moves on to the
    // submitted, payment-pending confirmation.
    [HttpPost("ReviewSummary/Send")]
    public IActionResult Send() => RedirectToAction(nameof(Submitted));

    [HttpGet("Submitted")]
    public IActionResult Submitted() => View();
}
