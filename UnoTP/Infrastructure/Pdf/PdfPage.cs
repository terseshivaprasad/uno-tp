using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnoTP.Infrastructure.Pdf;

/// <summary>A colour in a PDF: red, green and blue, each from 0 to 1.</summary>
public readonly record struct PdfColour(double Red, double Green, double Blue)
{
    public static readonly PdfColour Black = new(0, 0, 0);

    /// <summary>A colour written as a web page writes it: #E31837.</summary>
    public static PdfColour FromHex(string hex)
    {
        var digits = hex.TrimStart('#');
        double Part(int at) => int.Parse(digits.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return new PdfColour(Part(0), Part(2), Part(4));
    }
}

/// <summary>
/// One page of a PDF, written by hand with nothing but what .NET ships with: lines,
/// boxes, text and filled shapes. Everything is measured in points (72 to an inch)
/// from the page's top left corner, the way a page is read. Text is in Helvetica or
/// Helvetica-Bold, the two fonts every PDF reader has, so no font goes in the file.
/// </summary>
public sealed partial class PdfPage(double width, double height)
{
    private const double LineWidth = 0.5;

    // What is drawn, in the PDF's own drawing words.
    private readonly StringBuilder drawing = new();

    public void Line(double x1, double y1, double x2, double y2) =>
        drawing.Append($"{N(x1)} {N(Up(y1))} m {N(x2)} {N(Up(y2))} l S\n");

    /// <summary>The outline of a box, its top left corner at (x, y).</summary>
    public void Box(double x, double y, double boxWidth, double boxHeight) =>
        drawing.Append($"{N(x)} {N(Up(y + boxHeight))} {N(boxWidth)} {N(boxHeight)} re S\n");

    /// <summary>Text starting at x, standing on the line at baseline.</summary>
    public void Text(double x, double baseline, string text, double size, bool bold = false, PdfColour? colour = null)
    {
        if (text.Length == 0) return;
        var font = bold ? "F2" : "F1";
        drawing.Append($"BT /{font} {N(size)} Tf {Fill(colour ?? PdfColour.Black)} {N(x)} {N(Up(baseline))} Td ({Escaped(text)}) Tj ET\n");
    }

    /// <summary>Text set in the middle of the space between left and right.</summary>
    public void TextCentred(double left, double right, double baseline, string text, double size, bool bold = false, PdfColour? colour = null) =>
        Text(left + (right - left - WidthOf(text, size, bold)) / 2, baseline, text, size, bold, colour);

    /// <summary>Text ending at right.</summary>
    public void TextRight(double right, double baseline, string text, double size, bool bold = false) =>
        Text(right - WidthOf(text, size, bold), baseline, text, size, bold);

    /// <summary>How wide a piece of text is when drawn at a size.</summary>
    public static double WidthOf(string text, double size, bool bold)
    {
        var thousandths = 0;
        foreach (var letter in Printable(text)) thousandths += HelveticaWidths.Of(letter, bold);
        return thousandths * size / 1000;
    }

    /// <summary>A picture drawn pictureWidth wide, its top left corner at (x, y).</summary>
    public void Picture(SvgPicture picture, double x, double y, double pictureWidth)
    {
        var scale = pictureWidth / picture.Width;
        foreach (var shape in picture.Shapes) Shape(shape, x, y, scale);
    }

    // One filled shape of a picture. Its outline is an SVG path of whole commands
    // (M, L, H, V, C, Z), which is all a picture read by SvgPicture has.
    private void Shape(SvgShape shape, double x, double y, double scale)
    {
        string Point(double px, double py) => $"{N(x + px * scale)} {N(Up(y + py * scale))}";

        var tokens = PathToken().Matches(shape.Path).Select(match => match.Value).ToList();
        var at = 0;
        double Number() => double.Parse(tokens[at++], CultureInfo.InvariantCulture);

        drawing.Append(Fill(shape.Fill)).Append('\n');
        double currentX = 0, currentY = 0;
        var command = "";
        while (at < tokens.Count)
        {
            if (char.IsLetter(tokens[at][0])) command = tokens[at++];
            switch (command)
            {
                case "M":
                    (currentX, currentY) = (Number(), Number());
                    drawing.Append($"{Point(currentX, currentY)} m\n");
                    // More points after a move are lines to them.
                    command = "L";
                    break;
                case "L":
                    (currentX, currentY) = (Number(), Number());
                    drawing.Append($"{Point(currentX, currentY)} l\n");
                    break;
                case "H":
                    currentX = Number();
                    drawing.Append($"{Point(currentX, currentY)} l\n");
                    break;
                case "V":
                    currentY = Number();
                    drawing.Append($"{Point(currentX, currentY)} l\n");
                    break;
                case "C":
                    var first = Point(Number(), Number());
                    var second = Point(Number(), Number());
                    (currentX, currentY) = (Number(), Number());
                    drawing.Append($"{first} {second} {Point(currentX, currentY)} c\n");
                    break;
                case "Z":
                    drawing.Append("h\n");
                    // Closing takes no numbers: what follows has to be the next command.
                    command = "";
                    break;
                default:
                    throw new NotSupportedException($"A picture's outline uses '{tokens[at]}', which is not drawn here.");
            }
        }
        drawing.Append(shape.EvenOdd ? "f*\n" : "f\n");
    }

    [GeneratedRegex(@"[A-Za-z]|-?\d*\.?\d+(?:[eE][-+]?\d+)?")]
    private static partial Regex PathToken();

    /// <summary>The page as a PDF file.</summary>
    public byte[] Save()
    {
        var content = Encoding.Latin1.GetBytes($"{N(LineWidth)} w\n{drawing}");
        var objects = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(width)} {N(height)}] "
                + "/Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>"),
            StreamOf(content),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
        };

        using var file = new MemoryStream();
        void Write(string text) => file.Write(Encoding.ASCII.GetBytes(text));

        Write("%PDF-1.4\n");
        // Where each object starts in the file, for the table a reader finds them by.
        var starts = new List<long>();
        for (var number = 1; number <= objects.Count; number++)
        {
            starts.Add(file.Position);
            Write($"{number} 0 obj\n");
            file.Write(objects[number - 1]);
            Write("\nendobj\n");
        }

        var table = file.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var start in starts) Write($"{start:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{table}\n%%EOF\n");
        return file.ToArray();
    }

    // The drawing as the PDF holds it: its length, then the drawing itself.
    private static byte[] StreamOf(byte[] content)
    {
        var head = Encoding.ASCII.GetBytes($"<< /Length {content.Length} >>\nstream\n");
        var foot = Encoding.ASCII.GetBytes("\nendstream");
        return [.. head, .. content, .. foot];
    }

    // A PDF measures up from the bottom of the page.
    private double Up(double fromTop) => height - fromTop;

    private static string N(double number) => number.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Fill(PdfColour colour) => $"{N(colour.Red)} {N(colour.Green)} {N(colour.Blue)} rg";

    // Text as the two fonts can show it: curly quotes and long dashes as plain ones,
    // and a letter they do not have as a question mark.
    private static string Printable(string text)
    {
        var plain = text.Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"')
            .Replace('–', '-').Replace('—', '-');
        var letters = new StringBuilder(plain.Length);
        foreach (var letter in plain)
        {
            var shown = letter is >= ' ' and <= '~' or >= ' ' and <= 'ÿ';
            letters.Append(shown ? letter : '?');
        }
        return letters.ToString();
    }

    // Brackets and the backslash mean something inside a PDF's text, so they are marked.
    private static string Escaped(string text) =>
        Printable(text).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
