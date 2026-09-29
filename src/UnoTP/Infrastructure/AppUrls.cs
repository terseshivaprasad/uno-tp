namespace UnoTP;

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
    public string Url(string route) => Base(OwnerOf(route)) + route;

    public static string OwnerOf(string route)
    {
        var path = route.Split('?', '#')[0];
        if (path.StartsWith("/Apps/UnoTp", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/Dashboard", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Purchase/", StringComparison.OrdinalIgnoreCase)) return "UnoTP";
        if (path.StartsWith("/Apps/DmsExplorer", StringComparison.OrdinalIgnoreCase)) return "DmsExplorer";
        if (path.StartsWith("/Apps/OvdExplorer", StringComparison.OrdinalIgnoreCase)) return "OvdExplorer";
        if (path.Equals("/Classic", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/New", StringComparison.OrdinalIgnoreCase)) return "eSarathiConsole";
        return "eSarathiLogin";
    }

    private string Base(string app) => (config[$"Apps:{app}"] ?? "").TrimEnd('/');
}
