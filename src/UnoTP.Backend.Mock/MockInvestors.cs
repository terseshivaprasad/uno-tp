namespace UnoTP.Backend.Mock;

/// <summary>
/// A mock investor register. Investor Identification's test data card is written
/// from the lists below, and the mock NSDL and OCR answer from them too, so what
/// a demo is told to type is always what the mock holds. Every PAN starts XXXX
/// and they share one date of birth, so nothing here can be mistaken for a real one.
/// </summary>
public sealed class MockInvestors : IInvestorApi
{
    public Task<IReadOnlyList<FolioRecord>> FoliosByPanAsync(string pan, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FolioRecord>>(Folios.Where(f => f.Pan == pan).Select(f => f.Record).ToList());

    public Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default) =>
        Task.FromResult(Folios.FirstOrDefault(f => f.Folio == folio)?.Record);

    // Any PAN off the lists gets the same made-up answers every time: this is
    // what they are made up from.
    internal static int Seed(string value)
    {
        var n = 0;
        for (var i = 0; i < value.Length; i++) n += value[i] * (i + 1);
        return n;
    }

    // ----- The test data -------------------------------------------------------

    public const string DemoDob = "14-08-1988";

    /// <summary>A folio the register already holds, and what searching for it shows.</summary>
    public sealed record MockFolio(
        string Pan, string Dob, string Folio, string Name, string Gender,
        string Address, DocsOnRecord Docs, string Note, string Shows)
    {
        public FolioRecord Record => new(Pan, Dob, Folio, Name, Gender, Address, Docs, Note);
    }

    /// <summary>A PAN with no folio behind it, answered by the mock NSDL.
    /// Outcome is one of all, name, pan-dob.</summary>
    public sealed record MockPan(
        string Pan, string Dob, string Name, string Ocr, string Outcome, string Shows);

    public static readonly MockFolio[] Folios =
    {
        new("XXXXA1001A", DemoDob, "TS003027", "SHIVAPRASAD SUBHASH TERSE", "Male",
            "Flat 12, Shantiniketan CHS, Baner Road, Pune, Maharashtra 411045",
            new DocsOnRecord(true, true, true), "CKYC available with us",
            "Folio found and complete. Nothing is asked for again during entry, and Proceed opens straight away."),

        new("XXXXB1002B", DemoDob, "MF0051187", "RAHUL SUDHIR TAMBE", "Male",
            "", new DocsOnRecord(true, false, false), "CKYC available with us",
            "Folio found, but the photograph, proof of address and address are missing. They are listed as collected later, and Proceed still opens."),

        // Two folios against one PAN: refused by PAN, found by either folio number.
        new("XXXXF1006F", DemoDob, "MF0062210", "NEHA SURESH KULKARNI", "Female",
            "22 Sai Residency, Kothrud, Pune, Maharashtra 411038",
            new DocsOnRecord(true, true, true), "CKYC available with us",
            "Two folios against one PAN. The PAN search is refused and sends the partner to Operations to merge them; either folio number still finds its record."),
        new("XXXXF1006F", DemoDob, "MF0062211", "NEHA SURESH KULKARNI", "Female",
            "22 Sai Residency, Kothrud, Pune, Maharashtra 411038",
            new DocsOnRecord(true, true, false), "CKYC available with us", ""),

        // A folio with no date of birth on the register.
        new("XXXXG1007G", "", "MF0073345", "PRAKASH VINAYAK GOKHALE", "Male",
            "5 Laxmi Niwas, Dombivli East, Thane, Maharashtra 421201",
            new DocsOnRecord(true, true, true), "CKYC available with us",
            "The register holds no date of birth against this PAN. Searching by PAN or by folio is refused, and the partner is sent to Operations to have it updated."),
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

    /// <summary>
    /// Any PAN off the lists above still reaches every NSDL outcome, decided by its
    /// last digit. OCR reads one of these names off its copy, and NSDL holds the
    /// next one along when the outcome is a name mismatch.
    /// </summary>
    public static readonly string[] OcrNames =
        ["AMIT KUMAR SHARMA", "PRIYA RAMESH IYER", "SUNIL DATTA JOSHI", "MEERA ANAND NAIR"];
}
