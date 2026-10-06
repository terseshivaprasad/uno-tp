namespace UnoTP.Infrastructure;

/// <summary>
/// The directory the app is deployed under and the page's path, as a request brings
/// them. An address with a doubled slash in it - a portal link that ends its own
/// address with one and starts the directory with another, say - reaches the app
/// with the doubling still there. Left in the directory, every redirect would begin
/// "//directory/Dashboard", which a browser reads as another server named
/// "directory"; left in the path, no page matches it.
/// </summary>
public static class RequestPaths
{
    /// <summary>The path with every run of slashes made one.</summary>
    public static PathString SingleSlashes(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value)) return path;
        while (value.Contains("//", StringComparison.Ordinal)) value = value.Replace("//", "/", StringComparison.Ordinal);
        return new PathString(value);
    }

    /// <summary>
    /// The PathBase setting as the app takes it: one slash in front, none behind,
    /// however it was typed ("dir", "/dir/" and "//dir" are all "/dir"). Empty for no setting.
    /// </summary>
    public static string Directory(string? setting)
    {
        var name = (setting ?? "").Trim().Trim('/');
        if (name.Length == 0) return "";
        return SingleSlashes(new PathString("/" + name)).Value!;
    }
}
