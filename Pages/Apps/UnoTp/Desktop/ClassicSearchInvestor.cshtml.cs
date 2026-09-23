using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.UnoTpApp.Desktop;

public class ClassicSearchInvestorModel : PageModel
{
    // The rail down the left of every classic wizard step. This page is the first
    // of them; the ones after it carry an empty check until the wizard fills them.
    public static readonly string[] Steps =
    {
        "Investor Identification",
        "Upload Documents",
        "Investor Information",
        "Bank Details & Payment",
        "FD Configuration",
    };

    // One saved application the partner can pick up again. The register masks a
    // holder's identifiers the way the consent tracker does: a PAN keeps its
    // first five and last character, a date of birth keeps only its year.
    public record Draft(string AppNo, string Name, string Pan, string Dob, string Amount);

    // The in-flight rows the console lists, minus the ones now with Operations or
    // awaiting realisation, which are no longer the partner's to finish. A draft
    // has no PAN or date of birth of its own in this mock, so both are derived
    // from the application number and stay the same from one load to the next.
    public List<Draft> Drafts =>
        MockData.InFlightApplications
            .Where(a => a.StatusKey == "needs-you")
            .Take(5)
            .Select(a =>
            {
                var seed = a.AppNo.Sum(c => c);
                var pan = $"{(char)('A' + seed % 20)}{(char)('B' + seed * 3 % 22)}{(char)('C' + seed * 7 % 23)}P{(char)('B' + seed * 5 % 22)}\u2022\u2022\u2022\u2022{(char)('A' + seed * 11 % 26)}";
                var year = 1962 + seed % 34;
                return new Draft(a.AppNo, a.HolderMask, pan, $"\u2022\u2022/\u2022\u2022/{year}", a.Amount);
            })
            .ToList();

    // ----- The mock data the page runs on -------------------------------------
    // The card at the foot of the page and the script that answers the search are
    // both written from these lists, so what a demo is told to type is always
    // what the page holds. Every PAN starts XXXX and they share one date of
    // birth, so nothing here can be mistaken for a real one.
    public const string DemoDob = "14-08-1988";

    public record MockDocs(bool Pan, bool Photo, bool Poa);

    /// <summary>A folio the register already holds.</summary>
    public record MockFolio(
        string Pan, string Dob, string Folio, string Name, string Gender,
        string Address, MockDocs Docs, string Note, string Shows);

    /// <summary>A PAN with no folio behind it, answered by the mock NSDL.
    /// Outcome is one of all, name, pan-dob.</summary>
    public record MockPan(
        string Pan, string Dob, string Name, string Ocr, string Outcome, string Shows);

    public static readonly MockFolio[] Folios =
    {
        new("XXXXA1001A", DemoDob, "TS003027", "SHIVAPRASAD SUBHASH TERSE", "Male",
            "Flat 12, Shantiniketan CHS, Baner Road, Pune, Maharashtra 411045",
            new MockDocs(true, true, true), "CKYC available with us",
            "Folio found and complete. Nothing is asked for again during entry, and Proceed opens straight away."),

        new("XXXXB1002B", DemoDob, "MF0051187", "RAHUL SUDHIR TAMBE", "Male",
            "", new MockDocs(true, false, false), "CKYC available with us",
            "Folio found, but the photograph, proof of address and address are missing. They are listed as collected later, and Proceed still opens."),
    };

    public static readonly MockPan[] Pans =
    {
        new("XXXXC1003C", DemoDob, "ANJALI VIKRAM PATIL", "ANJALI VIKRAM PATIL", "all",
            "No folio, so a new application number opens. Upload the PAN copy, OCR reads it, NSDL matches all three, and Proceed opens."),

        new("XXXXD1004D", DemoDob, "KARAN DEEPAK MEHTA", "KARAN D MEHTA", "name",
            "PAN and date of birth match, but OCR misreads the name as KARAN D MEHTA. Type KARAN DEEPAK MEHTA and retry, and Proceed opens."),

        new("XXXXE1005E", DemoDob, "", "ROHIT SANJAY KULKARNI", "pan-dob",
            "NSDL holds no such PAN and date of birth. The check stops there, Proceed stays shut, and the only way on is to search again."),
    };

    /// <summary>The same two lists, as the script reads them.</summary>
    public string MockJson => System.Text.Json.JsonSerializer.Serialize(
        new { folios = Folios, pans = Pans },
        new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });

    public void OnGet()
    {
    }
}
