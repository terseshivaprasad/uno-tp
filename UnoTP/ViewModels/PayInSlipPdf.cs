using System.Globalization;
using UnoTP.Infrastructure.Pdf;
using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// The pay-in slip as a PDF, laid out as the FD system's own slip is: one sheet of A4
/// on its side with two copies beside each other, the bank's on the left and the
/// customer's on the right. Each copy is a ruled table: the two logos, how the
/// cheque is routed, the company, the application, then a line for each detail of
/// the slip and the cheque, with room for the depositor's and the cashier's signatures.
/// </summary>
public static class PayInSlipPdf
{
    // A4 on its side, in points.
    private const double PageWidth = 842, PageHeight = 595;

    // A copy: where it starts from the top, how wide it is, and where its two columns meet.
    private const double Top = 36, CopyWidth = 380, LabelWidth = 190;
    private const double BankCopyLeft = 28, CustomerCopyLeft = 434;

    // Row heights.
    private const double LogoRow = 60, Row = 13.5, SpacerRow = 12, SignatureRow = 36;

    // How far text stands in from a rule, and how far above the rule under it.
    private const double Inset = 3, AboveRule = 4;

    private const double TextSize = 8;

    /// <summary>The company the cheque is drawn in favour of, as the slip names it.</summary>
    public const string Company = "Mahindra & Mahindra Financial Services Ltd-Employee";

    private const string Routing = "To be routed through \"EASYPAY\" only";
    private const string Note = "Note: To be accepted at the all Axis Bank Branch";

    // The labels down each copy. The two copies of the FD system's slip word four of
    // them differently, and they are kept as they are.
    private static readonly string[] BankCopyLabels =
    [
        "MF User ID", "PAY-IN-SLIP No", "Customers Name", "Amount:", "Depositor's Signature",
        "Customers Bank Name", "Customer's Bank IFSC", "Customers Branch Name", "Customers Cheque Number",
        "Axis Branch Sol Id", "Total Amount in Words", "Verified By", "Cashier Signatures/Stamp",
    ];

    private static readonly string[] CustomerCopyLabels =
    [
        "MF User ID", "PAY-IN-SLIP No", "Customers Name", "Amount:", "Depositor's Signature",
        "Customer's Bank Name", "Customer's Bank IFSC", "Customer's Branch Name", "Customer's Cheque Number",
        "Axis Branch Sol Id", "Total Amount in Words", "Verified By", "Cashier Signatures/Stamp",
    ];

    // The two rows left tall and empty, to be signed.
    private static readonly string[] SignedRows = ["Depositor's Signature", "Cashier Signatures/Stamp"];

    /// <param name="mfUserId">The id of the user the slip is made by.</param>
    /// <param name="logo">The company's logo; without one its name is written.</param>
    public static byte[] Draw(SlipPrint slip, string mfUserId, SvgPicture? logo)
    {
        var page = new PdfPage(PageWidth, PageHeight);
        var values = ValuesOf(slip, mfUserId);
        DrawCopy(page, BankCopyLeft, "Bank Copy", "CHEQUE PAY-IN-SLIP", BankCopyLabels, values, slip, logo);
        // The customer's copy carries no title beside the routing line.
        DrawCopy(page, CustomerCopyLeft, "Customer Copy", "", CustomerCopyLabels, values, slip, logo);
        return page.Save();
    }

    /// <summary>The name the slip's file is saved under: PIS_18112025_ERO_866_.pdf.</summary>
    public static string FileName(SlipPrint slip, DateTime on) =>
        $"PIS_{on:ddMMyyyy}_ERO_{new string(slip.SolId.Where(char.IsLetterOrDigit).ToArray())}_.pdf";

    /// <summary>An amount in words as the slip writes it: FIFTY THOUSAND.</summary>
    public static string InWords(long amount) =>
        Money.InWords(amount).Replace(" rupees", "").ToUpperInvariant();

    // What stands against each label, in the order of the labels. The signed rows and
    // "Verified By" are left empty.
    private static string[] ValuesOf(SlipPrint slip, string mfUserId) =>
    [
        mfUserId, slip.SlipNo, slip.CustomerName, slip.Amount.ToString(CultureInfo.InvariantCulture), "",
        slip.BankName, slip.BankIfsc, slip.BranchName, slip.ChequeNumber,
        slip.SolId, InWords(slip.Amount), "", "",
    ];

    private static void DrawCopy(PdfPage page, double left, string copy, string title, string[] labels, string[] values, SlipPrint slip, SvgPicture? logo)
    {
        var right = left + CopyWidth;
        var middle = left + LabelWidth;
        var y = Top;

        // The logos: the bank's on the left, the company's on the right.
        page.Text(left + Inset, y + 9, copy, 6.5, bold: true);
        page.TextCentred(left, middle, y + 40, "AXIS BANK", 15, bold: true);
        DrawCompanyLogo(page, middle, right, y, logo);
        page.Line(middle, y, middle, y + LogoRow);
        y = RuleUnder(page, left, right, y + LogoRow);

        // The slip's title, and how the cheque is routed.
        var titleRight = left + CopyWidth / 3;
        page.TextCentred(left, titleRight, y + Row - AboveRule, title, TextSize);
        page.TextCentred(titleRight, right, y + Row - AboveRule, Routing, TextSize);
        page.Line(titleRight, y, titleRight, y + Row);
        y = RuleUnder(page, left, right, y + Row);

        page.TextCentred(left, right, y + Row - AboveRule, Company, 9, bold: true);
        y = RuleUnder(page, left, right, y + Row);

        page.Text(left + Inset, y + Row - AboveRule, "Application Number: " + slip.AppNo, TextSize);
        page.TextRight(right - Inset, y + Row - AboveRule, "Application Date: " + slip.ApplicationDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture), TextSize);
        y = RuleUnder(page, left, right, y + Row);
        y = RuleUnder(page, left, right, y + SpacerRow);

        // A line for each detail: its label on the left, what it is in the middle of the right.
        var detailsTop = y;
        for (var i = 0; i < labels.Length; i++)
        {
            var rowHeight = SignedRows.Contains(labels[i]) ? SignatureRow : Row;
            page.Text(left + Inset, y + rowHeight - AboveRule, labels[i], TextSize);
            page.TextCentred(middle, right, y + rowHeight - AboveRule, values[i], TextSize);
            y = RuleUnder(page, left, right, y + rowHeight);
        }
        page.Line(middle, detailsTop, middle, y);

        page.TextCentred(left, right, y + Row - AboveRule, Note, TextSize);
        y += Row;

        page.Box(left, Top, CopyWidth, y - Top);
    }

    // The company's logo in the middle of its cell; its name where there is no logo to draw.
    private static void DrawCompanyLogo(PdfPage page, double left, double right, double top, SvgPicture? logo)
    {
        if (logo is null)
        {
            page.TextCentred(left, right, top + 36, "mahindra FINANCE", 13, bold: true);
            return;
        }
        const double logoWidth = 150;
        var logoHeight = logoWidth * logo.Height / logo.Width;
        page.Picture(logo, left + (right - left - logoWidth) / 2, top + (LogoRow - logoHeight) / 2, logoWidth);
    }

    // A rule across the copy at y, which is where the next row starts.
    private static double RuleUnder(PdfPage page, double left, double right, double y)
    {
        page.Line(left, y, right, y);
        return y;
    }
}
