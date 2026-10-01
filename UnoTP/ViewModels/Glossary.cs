using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace UnoTP.ViewModels;

/// <summary>
/// The trade's words a partner meets on the steps - CKYC, NSDL, POA and the rest -
/// each drawn with a dotted underline that explains it on hover or focus, and read
/// out in full to a screen reader. Written once here, so every page says the same.
/// </summary>
public static class Glossary
{
    private static readonly Dictionary<string, string> Meanings = new()
    {
        ["CKYC"] = "Central KYC: the investor's KYC record, held by CERSAI",
        ["CERSAI"] = "The central registry that keeps Central KYC (CKYC) records",
        ["NSDL"] = "The PAN registry: it confirms the PAN, date of birth and name belong together",
        ["POA"] = "Proof of address",
        ["CMS"] = "Axis Bank's cash-management branch, where a cheque is paid in",
        ["IFSC"] = "The 11-character code of a bank branch",
        ["MICR"] = "The 9-digit code on a cheque's bottom line",
        ["OCR"] = "Reading the text off an uploaded copy",
        ["TDS"] = "Tax deducted at source from the interest",
        ["121"] = "Form 121: the investor's signed declaration that no tax is due on the interest, so no TDS is deducted",
        ["FATCA"] = "Foreign tax residency, declared under the US FATCA rules",
        ["PEP"] = "Politically exposed person",
    };

    /// <summary>The term with its meaning on hover and focus; the plain term if it is not in the glossary.</summary>
    public static IHtmlContent Term(string term, string? shown = null)
    {
        var text = HtmlEncoder.Default.Encode(shown ?? term);
        if (!Meanings.TryGetValue(term, out var meaning)) return new HtmlString(text);
        var tip = HtmlEncoder.Default.Encode(meaning);
        return new HtmlString($"<abbr class=\"hover-hint\" tabindex=\"0\" data-tip=\"{tip}\">{text}<span class=\"visually-hidden\"> ({tip})</span></abbr>");
    }
}
