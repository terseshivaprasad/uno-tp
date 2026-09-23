using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Desktop;

/// <summary>
/// The old E-Sarathi login (WA_FD_ESARATHI_LOGIN), the page in front of the
/// console: the User ID and Password pair, and the OTP the site asks for once
/// that pair checks out.
///
/// Nothing is authenticated here. The page is a mock, so the one pair it knows
/// and the OTP it expects are constants rendered into the page and checked in
/// the browser (wwwroot/js/classic-login.js); the note under the form says what
/// they are, because a login nobody can get past is no use as a reference.
/// </summary>
public class ClassicLoginModel : PageModel
{
    /// <summary>The only pair the mock lets through: the entity the rest of the mock is filed under.</summary>
    public const string DemoUserId = MockData.EntityCode;
    public const string DemoPassword = "Mahindra@123";
    public const string DemoOtp = "123456";

    /// <summary>Where the live site says it sent the OTP, masked as it masks them.</summary>
    public const string OtpMobile = "+91 •••••• 4821";
    public const string OtpEmail = "s••••••••e@mahindrafinance.com";

    public void OnGet()
    {
    }
}
