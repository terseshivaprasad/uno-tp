namespace UnoTP.Infrastructure;

/// <summary>
/// Where the other apps of the solution are served from. Each app runs on its own,
/// so a link into another one needs that app's address. No two apps answer the
/// same route, so the route alone says which app to send it to.
///
/// The addresses come from the "Apps" section of appsettings. An app left blank
/// there gets a plain route, which is right when all five sit behind one host.
/// </summary>
public sealed class AppUrls(IConfiguration config)
{
    /// <summary>The full URL of a route, on the app that owns it (see <see cref="OwnerOf"/>).</summary>
    public string Url(string route) => BaseUrlOf(OwnerOf(route)) + route;

    public static string OwnerOf(string route)
    {
        var path = route.Split('?', '#')[0];
        // Uno TP's pages, under their old names, and the old addresses that lead to them.
        string[] unoTp = ["/Home", "/Dashboard", "/SearchInvestor", "/UploadInvestorDocuments", "/InvestorInformation", "/BankDetails",
            "/FDConfiguration", "/ReviewSummary", "/ApplicationSubmitted", "/ViewApplication", "/PayInSlip", "/ShortUrl", "/RenewalDashboard",
            "/Admin", "/Error", "/Apps/UnoTp", "/Purchase/"];
        foreach (var prefix in unoTp)
        {
            if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase) || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)) return "UnoTP";
        }
        if (path.StartsWith("/Apps/DmsExplorer", StringComparison.OrdinalIgnoreCase)) return "DmsExplorer";
        if (path.StartsWith("/Apps/OvdExplorer", StringComparison.OrdinalIgnoreCase)) return "OvdExplorer";
        if (path.Equals("/Classic", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/New", StringComparison.OrdinalIgnoreCase)) return "eSarathiConsole";
        return "eSarathiLogin";
    }

    /// <summary>The base address configured for an app (Apps:{app}).</summary>
    private string BaseUrlOf(string app) => (config[$"Apps:{app}"] ?? "").TrimEnd('/');
}
