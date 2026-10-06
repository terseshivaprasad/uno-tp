using System.Text.RegularExpressions;

namespace UnoTP.Tests;

/// <summary>
/// Holds the pages to the design's UI guidelines (docs/BRAND_GUIDELINES.md): the
/// page frame and the type are the design's numbers, text is a size on the scale,
/// and a view carries no styles of its own. Reads the stylesheets and the views as
/// files; no running app is needed.
/// </summary>
public class UiGuidelineTests
{
    /// <summary>Text sizes the design uses: label, body, title, and the one lead of a dashboard or a dialog.</summary>
    private static readonly string[] TextSizes = { "12px", "14px", "16px", "20px" };

    /// <summary>
    /// Sizes off the scale that were in the stylesheets before the guidelines came.
    /// Each goes when its page is moved onto the guidelines. Nothing is added here.
    /// </summary>
    private static readonly string[] TextSizesStillToMove =
    {
        "css/shared/layout-and-controls.css: 9.5px",
        "css/pages/deposit.css: 28px",
        "css/pages/investor.css: 13px",
        "css/pages/renew.css: 13px",
    };

    /// <summary>
    /// Views that still carry a style of their own from before the guidelines came.
    /// Each goes when its page is moved onto the guidelines. Nothing is added here.
    /// </summary>
    private static readonly string[] ViewsStillToMove =
    {
        "Views/Admin/Index.cshtml",
        "Views/Applications/Index.cshtml",
        "Views/Investor/Index.cshtml",
        "Views/Links/Index.cshtml",
        "Views/PayInSlips/Index.cshtml",
        "Views/Payment/Index.cshtml",
        "Views/Shared/_ClassicSteps.cshtml",
        "Views/Shared/_Deposits.cshtml",
        "Views/Shared/_RequiredDocs.cshtml",
    };

    [Theory]
    // The design's Spacing board, drawn at a 1360px window.
    [InlineData("--header-height", "46px")]
    [InlineData("--page-gap-top", "32px")]
    [InlineData("--page-gap-side", "47px")]
    [InlineData("--page-gap-bottom", "24px")]
    [InlineData("--rail-width", "242px")]
    [InlineData("--rail-gap", "24px")]
    [InlineData("--card-gap", "18px")]
    [InlineData("--card-padding", "24px")]
    [InlineData("--field-gap", "32px")]
    [InlineData("--action-bar-height", "64px")]
    // The design's Typography board: weight, size / line height.
    [InlineData("--font-title", "500 16px/19px var(--font-family)")]
    [InlineData("--font-label", "500 12px/24px var(--font-family)")]
    [InlineData("--font-value", "500 14px/24px var(--font-family)")]
    [InlineData("--font-question", "500 14px/20px var(--font-family)")]
    [InlineData("--font-option", "400 14px/17px var(--font-family)")]
    public void A_design_token_holds_the_designs_value(string token, string expected)
    {
        var tokens = DesignTokens();

        Assert.True(tokens.ContainsKey(token), $"{token} is missing from :root in css/shared/layout-and-controls.css.");
        Assert.Equal(expected, tokens[token]);
    }

    [Fact]
    public void Text_is_a_size_on_the_scale()
    {
        var found = new List<string>();
        foreach (var file in Stylesheets())
        {
            var css = WithoutComments(File.ReadAllText(file));
            foreach (Match match in Regex.Matches(css, @"(?<![\w-])font-size:\s*([^;}]+)"))
            {
                var size = match.Groups[1].Value.Trim();
                if (size == "inherit" || TextSizes.Contains(size))
                {
                    continue;
                }
                found.Add($"{Named(file)}: {size}");
            }
        }

        var unexpected = found.Except(TextSizesStillToMove).ToList();
        Assert.True(unexpected.Count == 0,
            "Text sizes off the design's scale (12, 14, 16, 20px; better, one of the --font- tokens):\n" + string.Join("\n", unexpected));
    }

    [Fact]
    public void A_text_size_once_moved_is_taken_off_the_list()
    {
        var found = new List<string>();
        foreach (var file in Stylesheets())
        {
            var css = WithoutComments(File.ReadAllText(file));
            foreach (Match match in Regex.Matches(css, @"(?<![\w-])font-size:\s*([^;}]+)"))
            {
                found.Add($"{Named(file)}: {match.Groups[1].Value.Trim()}");
            }
        }

        var gone = TextSizesStillToMove.Except(found).ToList();
        Assert.True(gone.Count == 0,
            "These are on the scale now. Take them out of TextSizesStillToMove:\n" + string.Join("\n", gone));
    }

    [Fact]
    public void A_view_carries_no_styles_of_its_own()
    {
        var found = new List<string>();
        foreach (var file in Views())
        {
            var view = File.ReadAllText(file);
            if (view.Contains("style=\"") || view.Contains("<style"))
            {
                found.Add(Named(file));
            }
        }

        var unexpected = found.Except(ViewsStillToMove).ToList();
        Assert.True(unexpected.Count == 0,
            "Views with a style= attribute or a <style> block (put the rule in the page's stylesheet):\n" + string.Join("\n", unexpected));

        var gone = ViewsStillToMove.Except(found).ToList();
        Assert.True(gone.Count == 0,
            "These carry no styles now. Take them out of ViewsStillToMove:\n" + string.Join("\n", gone));
    }

    /// <summary>The names and values in the first :root block of layout-and-controls.css, where the design tokens are.</summary>
    private static Dictionary<string, string> DesignTokens()
    {
        var css = WithoutComments(File.ReadAllText(Path.Combine(AppFolder(), "wwwroot", "css", "shared", "layout-and-controls.css")));
        var root = Regex.Match(css, @":root\s*\{([^}]*)\}").Groups[1].Value;

        var tokens = new Dictionary<string, string>();
        foreach (Match match in Regex.Matches(root, @"(--[\w-]+):\s*([^;]+);"))
        {
            tokens[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }
        return tokens;
    }

    /// <summary>The app's own stylesheets: css/shared and css/pages. Bootstrap's (lib) is not ours to hold to the scale.</summary>
    private static IEnumerable<string> Stylesheets()
    {
        var css = Path.Combine(AppFolder(), "wwwroot", "css");
        return Directory.EnumerateFiles(css, "*.css", SearchOption.AllDirectories).OrderBy(file => file);
    }

    /// <summary>Every Razor view.</summary>
    private static IEnumerable<string> Views()
    {
        var views = Path.Combine(AppFolder(), "Views");
        return Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories).OrderBy(file => file);
    }

    /// <summary>A file as the lists above name it: from the app's folder (wwwroot left off), with forward slashes.</summary>
    private static string Named(string file)
    {
        var name = Path.GetRelativePath(AppFolder(), file).Replace('\\', '/');
        if (name.StartsWith("wwwroot/"))
        {
            name = name.Substring("wwwroot/".Length);
        }
        return name;
    }

    /// <summary>A stylesheet with its comments blanked, so a size named in a comment is not taken for a rule.</summary>
    private static string WithoutComments(string css)
    {
        return Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
    }

    /// <summary>The UnoTP project folder, found by walking up from where the tests run to the folder that holds UnoTP.sln.</summary>
    private static string AppFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "UnoTP.sln")))
        {
            folder = folder.Parent;
        }
        if (folder is null)
        {
            throw new DirectoryNotFoundException("UnoTP.sln was not found above " + AppContext.BaseDirectory);
        }
        return Path.Combine(folder.FullName, "UnoTP");
    }
}
