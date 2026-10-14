using System.Globalization;
using System.Text.RegularExpressions;

namespace UnoTP.Infrastructure.Pdf;

/// <summary>One filled shape of a picture: its outline as an SVG path, and its colour.</summary>
/// <param name="EvenOdd">How a shape with holes in it is filled (the SVG's fill-rule).</param>
public sealed record SvgShape(string Path, PdfColour Fill, bool EvenOdd);

/// <summary>
/// A small picture kept as an SVG file - the logo - read for a PDF page to draw.
/// Only what such a file is made of is read: its size (viewBox) and its filled paths.
/// </summary>
public sealed partial record SvgPicture(double Width, double Height, IReadOnlyList<SvgShape> Shapes)
{
    /// <summary>The picture in an SVG file's text; null when it has no size or no paths.</summary>
    public static SvgPicture? Read(string svg)
    {
        var size = ViewBox().Match(svg);
        if (!size.Success) return null;

        var shapes = new List<SvgShape>();
        foreach (Match path in PathElement().Matches(svg))
        {
            var attributes = path.Groups[1].Value;
            var outline = Attribute(attributes, "d");
            var fill = Attribute(attributes, "fill");
            if (outline.Length == 0 || !fill.StartsWith('#')) continue;
            shapes.Add(new SvgShape(outline, PdfColour.FromHex(fill), Attribute(attributes, "fill-rule") == "evenodd"));
        }
        if (shapes.Count == 0) return null;

        return new SvgPicture(Number(size.Groups[1].Value), Number(size.Groups[2].Value), shapes);
    }

    private static string Attribute(string attributes, string name)
    {
        var found = Regex.Match(attributes, $"(?<![\\w-]){name}=\"([^\"]*)\"");
        return found.Success ? found.Groups[1].Value : "";
    }

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex("viewBox=\"[\\d.]+ [\\d.]+ ([\\d.]+) ([\\d.]+)\"")]
    private static partial Regex ViewBox();

    [GeneratedRegex("<path ([^>]*)/>")]
    private static partial Regex PathElement();
}
