using System.Text;
using UnoTP.Infrastructure.Pdf;
using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>The pay-in slip as a PDF (ViewModels/PayInSlipPdf.cs), and the page it is drawn on (Infrastructure/Pdf).</summary>
public class PayInSlipPdfTests
{
    private static readonly SlipPrint Slip = new()
    {
        SlipNo = "AXPIS0007", AppNo = "DEVBB26F10001", ApplicationDate = new DateTime(2025, 11, 18),
        CustomerName = "Asha Test", Amount = 50000, ChequeNumber = "123123",
        BankName = "Test Bank", BankIfsc = "TEST0001180", BranchName = "SHIVAJINAGAR", SolId = "866", AccountNumber = "123456789012",
    };

    // The file as text: what is drawn is not packed, so the words on the slip can be read in it.
    private static string Drawn() => Encoding.Latin1.GetString(PayInSlipPdf.Draw(Slip, "23093276", logo: null));

    private static int Times(string text, string part) => text.Split(part).Length - 1;

    [Fact]
    public void The_file_is_a_PDF_whose_table_points_at_itself()
    {
        var pdf = Drawn();

        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.EndsWith("%%EOF\n", pdf);
        // A reader finds the objects through the table whose place the file's last lines give.
        var tableAt = int.Parse(pdf[(pdf.LastIndexOf("startxref\n") + "startxref\n".Length)..pdf.LastIndexOf("\n%%EOF")]);
        Assert.Equal("xref", pdf.Substring(tableAt, 4));
    }

    [Fact]
    public void The_sheet_carries_the_banks_copy_and_the_customers()
    {
        var pdf = Drawn();

        Assert.Equal(1, Times(pdf, "(Bank Copy)"));
        Assert.Equal(1, Times(pdf, "(Customer Copy)"));
        // Only the bank's copy is titled.
        Assert.Equal(1, Times(pdf, "(CHEQUE PAY-IN-SLIP)"));
        Assert.Equal(2, Times(pdf, "(To be routed through \"EASYPAY\" only)"));
        Assert.Equal(2, Times(pdf, "(Note: To be accepted at the all Axis Bank Branch)"));
    }

    [Fact]
    public void Each_copy_says_the_slip_the_application_and_the_cheque()
    {
        var pdf = Drawn();

        foreach (var value in new[]
        {
            "(Application Number: DEVBB26F10001)", "(Application Date: 18-Nov-2025)", "(23093276)", "(AXPIS0007)", "(Asha Test)",
            "(50000)", "(Test Bank)", "(TEST0001180)", "(SHIVAJINAGAR)", "(123456789012)", "(123123)", "(866)", "(FIFTY THOUSAND)",
        })
        {
            Assert.Equal(2, Times(pdf, value));
        }
    }

    [Fact]
    public void The_two_copies_word_their_labels_as_the_FD_systems_slip_does()
    {
        var pdf = Drawn();

        Assert.Equal(1, Times(pdf, "(Customers Bank Name)"));
        Assert.Equal(1, Times(pdf, "(Customer's Bank Name)"));
        Assert.Equal(1, Times(pdf, "(Customers Cheque Number)"));
        Assert.Equal(1, Times(pdf, "(Customer's Cheque Number)"));
        Assert.Equal(2, Times(pdf, "(Customer's Bank IFSC)"));
    }

    [Theory]
    [InlineData(50000, "FIFTY THOUSAND")]
    [InlineData(2000000, "TWENTY LAKH")]
    [InlineData(125000, "ONE LAKH TWENTY-FIVE THOUSAND")]
    public void The_amount_is_written_in_words_in_capitals(long amount, string words)
    {
        Assert.Equal(words, PayInSlipPdf.InWords(amount));
    }

    [Fact]
    public void The_file_is_named_after_the_day_and_the_Axis_branch()
    {
        Assert.Equal("PIS_18112025_ERO_866_.pdf", PayInSlipPdf.FileName(Slip, new DateTime(2025, 11, 18)));
    }

    [Fact]
    public void Brackets_in_a_name_do_not_break_the_text()
    {
        var page = new PdfPage(100, 100);
        page.Text(10, 10, "A (B) C", 8);

        Assert.Contains("(A \\(B\\) C)", Encoding.Latin1.GetString(page.Save()));
    }

    [Fact]
    public void Text_is_centred_by_the_width_of_its_letters()
    {
        // In Helvetica a digit is 556 thousandths of the size wide.
        Assert.Equal(5 * 0.556 * 8, PdfPage.WidthOf("50000", 8, bold: false), 3);
    }

    [Fact]
    public void A_logo_is_read_from_its_SVG_as_filled_shapes()
    {
        var logo = SvgPicture.Read("""
            <svg width="20" height="10" viewBox="0 0 20 10" fill="none">
            <path d="M1 1H9V9H1Z" fill="#4D4D4F"/>
            <path fill-rule="evenodd" clip-rule="evenodd" d="M11 1L19 1L19 9Z" fill="#E31837"/>
            </svg>
            """)!;

        Assert.Equal((20, 10, 2), (logo.Width, logo.Height, logo.Shapes.Count));
        Assert.False(logo.Shapes[0].EvenOdd);
        Assert.True(logo.Shapes[1].EvenOdd);

        var page = new PdfPage(100, 100);
        page.Picture(logo, 0, 0, 20);
        var drawn = Encoding.Latin1.GetString(page.Save());
        // The first shape: moved to its start, three lines, closed and filled.
        Assert.Contains("1 99 m\n9 99 l\n9 91 l\n1 91 l\nh\nf\n", drawn);
        Assert.Contains("f*\n", drawn);
    }
}
