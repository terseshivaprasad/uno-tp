using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.Infrastructure;

/// <summary>
/// The "Menu" section of appsettings: which console feature each page of the
/// portal's menu opens.
/// </summary>
public sealed class MenuOptions
{
    public const string Section = "Menu";

    /// <summary>The page name the portal's menu gives (PageName) -> the console feature key it opens.</summary>
    public Dictionary<string, string> Pages { get; set; } = [];

    /// <summary>The console features the user's menu opens, each once.</summary>
    public List<string> FeatureKeys(IEnumerable<MenuItem> menu)
    {
        var keys = new List<string>();
        foreach (var item in menu)
        {
            foreach (var (pageName, featureKey) in Pages)
            {
                if (!string.Equals(pageName, item.PageName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!FeatureSet.MenuKeys.Contains(featureKey)) continue;
                if (!keys.Contains(featureKey)) keys.Add(featureKey);
            }
        }
        return keys;
    }
}
