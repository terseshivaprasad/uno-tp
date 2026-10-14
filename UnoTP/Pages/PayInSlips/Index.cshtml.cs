using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTP.Models;
using UnoTP.Infrastructure;
using UnoTP.Infrastructure.Pdf;
using UnoTP.ViewModels;

namespace UnoTP.Pages;

/// <summary>
/// Pay In Slip Generation: the applications paying on paper a search reaches, from the
/// backend. The search is in the address - <c>by</c> (dates or appno), <c>from</c> and <c>to</c>
/// (yyyy-MM-dd) or <c>q</c> (what was typed) - and is run by the database.
/// Generating a slip is the backend's to do; the post says what came of it once, on
/// the page it redirects to. A digital application's acceptance link is sent from
/// Short URL, not here. A slip
/// generated is saved as a PDF as that page opens.
/// </summary>
[RequiresFeature("pis")]
public class PayInSlipsModel(IPayInSlipApi slips, Lookups lookups, IPartner partner, IWebHostEnvironment web) : PageModel
{
    // The company's logo, drawn on the slip.
    private const string LogoFile = "images/mahindra-finance-logo.svg";

    /// <summary>What the page shows.</summary>
    public PayInSlipsViewModel View { get; private set; } = null!;

    /// <summary>The application whose slip was generated just now, to save its PDF as the page opens; null otherwise.</summary>
    public string? SlipToSave { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? by, string? q, string? from, string? to)
    {
        ViewData["Toast"] = TempData["toast"];
        SlipToSave = TempData["slip"] as string;
        var windowDays = (await lookups.ConfigAsync()).CancellationDays;
        var search = ListSearch.Asked(by, q, from, to, windowDays, folioToo: false);
        View = new PayInSlipsViewModel(await slips.SearchAsync(search), windowDays, search);
        return Page();
    }

    /// <summary>Issues the application's slip - the same one again, for a reprint; its PDF is saved as the page comes back.</summary>
    public async Task<IActionResult> OnPostGenerateAsync(string appNo)
    {
        if (await slips.GenerateAsync(appNo) is { } slip)
        {
            TempData["toast"] = $"Pay-in slip {slip.SlipNo} for {slip.Branch} is ready.";
            TempData["slip"] = appNo;
        }
        else
        {
            TempData["toast"] = Messages.Lists.NoSlip;
        }
        return RedirectToPage();
    }

    /// <summary>The newest pay-in slip of one of the partner's applications, as a PDF: the bank's copy and the customer's.</summary>
    public async Task<IActionResult> OnGetSlipAsync(string appNo)
    {
        if (await slips.SlipToPrintAsync(appNo) is not { } slip) return NotFound();
        var pdf = PayInSlipPdf.Draw(slip, partner.UserClusterId, CompanyLogo());
        return File(pdf, "application/pdf", PayInSlipPdf.FileName(slip, DateTime.Today));
    }

    // The logo as the PDF draws it; null when its file is not there, and the slip writes the name.
    private SvgPicture? CompanyLogo()
    {
        var file = web.WebRootFileProvider.GetFileInfo(LogoFile);
        if (!file.Exists) return null;
        using var reader = new StreamReader(file.CreateReadStream());
        return SvgPicture.Read(reader.ReadToEnd());
    }
}
