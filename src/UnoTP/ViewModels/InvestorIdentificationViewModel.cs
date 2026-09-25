using System.Text.RegularExpressions;
using UnoTP.Backend;
using UnoTP.Backend.External;

namespace UnoTP.ViewModels;

/// <summary>
/// Investor Identification: the investor is checked against the register before
/// the application can go on, and Proceed waits for that match. Everything is
/// answered on the server - the register lookup through the backend and the checks
/// on what was typed here - so the page needs no script of its own. A PAN with no
/// folio goes on as it stands: its PAN copy is read by OCR and put to NSDL on
/// Upload Documents, as a joint holder's is.
///
/// Every step is a post, and every post redirects back to the bare page address
/// (see <see cref="Controllers.InvestorIdentificationController"/>): what was typed
/// and what the check found are kept in the session, never in the address, so no
/// PAN, date of birth or name reaches a log, the history or a Referer header. The
/// page drawn after the redirect runs the check again from what the session holds.
/// </summary>
public partial class InvestorIdentificationViewModel(IInvestorApi investors)
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

    /// <summary>The standing Note beside the search, worded as the old page words it.</summary>
    public static readonly string[] Notes =
    [
        "Only individual depositors 18 years and above are allowed to make investments.",
        "We strongly advice the depositor(s) to avail the nomination",
        "For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com",
    ];

    /// <summary>
    /// The test data card at the foot of the page, switched in appsettings. It
    /// describes the mock backend, so it is only ever shown while that answers.
    /// </summary>
    public bool ShowDemoData { get; set; }

    // ----- What was typed ------------------------------------------------------

    /// <summary>"pan" to search by PAN and date of birth, "folio" by folio number.</summary>
    public string By { get; set; } = "pan";

    public string? Pan { get; set; }

    public string? Dd { get; set; }

    public string? Mm { get; set; }

    public string? Yyyy { get; set; }

    public string? Folio { get; set; }

    // ----- What the page shows -------------------------------------------------

    public enum Stage
    {
        /// <summary>The search fields, blank or with what is wrong in them.</summary>
        Search,
        /// <summary>A folio the register holds.</summary>
        Found,
        /// <summary>A PAN with no folio: a new investor, put to NSDL on Upload Documents.</summary>
        Pending,
    }

    public Stage At { get; private set; } = Stage.Search;

    /// <summary>Found on the register.</summary>
    public bool Identified => At is Stage.Found;

    /// <summary>Whether Proceed opens: a folio found, or a PAN with no folio.</summary>
    public bool CanProceed => At is Stage.Found or Stage.Pending && Record is not null;

    public string? PanError { get; private set; }
    public string? DobError { get; private set; }
    public string? FolioError { get; private set; }

    /// <summary>
    /// Set when the register holds the investor in a way only Operations can fix -
    /// two folios against one PAN, or no date of birth - so the page says what to
    /// take to them rather than asking the partner to type something else.
    /// </summary>
    public string? OpsError { get; private set; }

    /// <summary>The record on the card, once a check has found or opened one.</summary>
    public Holder? Record { get; private set; }

    /// <summary>"Existing customer", "Not identified" or "New investor".</summary>
    public string Kind { get; private set; } = "";

    /// <summary>How the record was found, or what happens next for a new PAN.</summary>
    public string Detail { get; private set; } = "";

    /// <summary>The date of birth as the register keeps it, dd-MM-yyyy.</summary>
    public string Dob => $"{Pad(Dd)}-{Pad(Mm)}-{Yyyy}";

    // ----- Filled from the session -------------------------------------------

    /// <summary>What was typed, as the session holds it.</summary>
    public void Fill(string by, string? pan, string? dd, string? mm, string? yyyy, string? folio) =>
        (By, Pan, Dd, Mm, Yyyy, Folio) = (by, pan, dd, mm, yyyy, folio);

    /// <summary>
    /// Turns a holder the register found back to the search fields, saying why under
    /// the field that was searched - as when the PAN is already on the application.
    /// </summary>
    public void Reject(string why)
    {
        if (By == "folio") FolioError = why; else PanError = why;
        Record = null;
        At = Stage.Search;
    }

    /// <summary>A name typed from the PAN card, the way NSDL is sent it.</summary>
    public static string NormaliseName(string? typed) => Spaces().Replace(typed ?? "", " ").Trim().ToUpperInvariant();

    // ----- The check -----------------------------------------------------------

    // Validates what was typed and looks it up. False when the fields are wrong,
    // and the page goes back to them with what is wrong said under each.
    public async Task<bool> CheckAsync()
    {
        By = By == "folio" ? "folio" : "pan";

        if (By == "folio")
        {
            Folio = Clean(Folio);
            if (Folio.Length == 0) { FolioError = "Enter the folio number"; return false; }
            var byFolio = await investors.FolioAsync(Folio);
            if (byFolio is null)
            {
                FolioError = "No record against that folio number — check it, or search by PAN instead";
                return false;
            }
            // Without a date of birth the record cannot be told apart from a minor's,
            // and the partner cannot supply one: only Operations can correct it.
            if (byFolio.Dob.Length == 0)
            {
                FolioError = "The date of birth is not available with us";
                OpsError = NoDob(byFolio);
                return false;
            }
            ShowFolio(byFolio, "Matched on folio number");
            return true;
        }

        Pan = Clean(Pan);
        PanError = Pan.Length == 0 ? "Enter the PAN"
            : PanPattern().IsMatch(Pan) ? null : "Enter a valid PAN, like ABCDE1234F";
        DobError = DobProblem();
        if (PanError is not null || DobError is not null) return false;

        var onRecord = await investors.FoliosByPanAsync(Pan);

        // More than one folio against a PAN is a record Operations has to merge:
        // a search by PAN cannot say which of them the deposit is booked against.
        // Each folio can still be searched by its number.
        if (onRecord.Count > 1)
        {
            PanError = $"{onRecord.Count} folios are held against this PAN";
            var folios = onRecord.Select(f => f.Folio).ToList();
            OpsError = $"Folios {string.Join(", ", folios.Take(folios.Count - 1))} and {folios[^1]} are all held against PAN {Pan}. "
                + "Operations has to merge them before a deposit can be booked against the PAN. "
                + "Until then, each can still be searched by its folio number.";
            return false;
        }

        if (onRecord.Count == 1)
        {
            var only = onRecord[0];
            // The register has the PAN but no date of birth to check the one typed
            // against, and only Operations can put one on the record.
            if (only.Dob.Length == 0)
            {
                DobError = "The date of birth is not available with us";
                OpsError = NoDob(only);
                return false;
            }
            if (only.Dob == Dob)
            {
                ShowFolio(only, "Matched on PAN and date of birth");
                return true;
            }
            // A PAN on record against another date of birth is a typo far more often
            // than a second investor, so it goes back to the field rather than opening
            // as a new folio.
            DobError = "This PAN is on record, but against a different date of birth";
            return false;
        }

        Record = new Holder(Pan, Dob, "", "", "", "", new DocsOnRecord(false, false, false), "");
        Kind = "New investor";
        Detail = "No folio against this PAN: a new application opens, and the PAN copy is checked with NSDL on Upload Documents.";
        At = Stage.Pending;
        return true;
    }

    private static string NoDob(FolioRecord f) =>
        $"The register holds PAN {f.Pan} (folio {f.Folio}) without a date of birth, so the investor's age cannot be checked. "
        + "Operations has to add it to the record before a deposit can be booked.";

    private void ShowFolio(FolioRecord f, string how)
    {
        Record = new Holder(f.Pan, f.Dob, f.Folio, f.Name, f.Gender, f.Address, f.Docs, f.Note);
        Kind = "Existing customer";
        Detail = how;
        At = Stage.Found;
    }

    private string? DobProblem()
    {
        Dd = Digits(Dd); Mm = Digits(Mm); Yyyy = Digits(Yyyy);
        if (Dd.Length == 0 || Mm.Length == 0 || Yyyy.Length == 0) return "Enter the date of birth";
        if (Yyyy.Length < 4) return "Enter the year in full";
        if (!DateTime.TryParseExact(Dob, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var when))
            return "Enter a valid date";
        if (when > DateTime.Today) return "The date of birth cannot be in the future";
        // The note on this page: an investor under 18 cannot hold a deposit.
        if (when.AddYears(18) > DateTime.Today) return "The depositor must be 18 years or above";
        return null;
    }

    // ----- The record on the card ----------------------------------------------

    /// <summary>
    /// The investor the card shows. A PAN with no folio has no name, gender or
    /// address until its PAN copy is read and NSDL verifies it.
    /// </summary>
    public sealed record Holder(string Pan, string Dob, string Folio, string Name, string Gender, string Address, DocsOnRecord Docs, string Note)
    {
        /// <summary>The name, or the PAN while a new investor has none.</summary>
        public string Heading => Name.Length > 0 ? Name : Pan;

        /// <summary>The number the record goes by: its folio. A new investor has none
        /// until the application opens and the backend gives it its number.</summary>
        public string? Id => Folio.Length > 0 ? $"Folio {Folio}" : null;

        public string Initials
        {
            get
            {
                var words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return words.Length == 0 ? "" : words.Length == 1 ? words[0][..1] : $"{words[0][0]}{words[^1][0]}";
            }
        }

        // The register masks a holder's identifiers: a PAN keeps its first five and
        // last character, a date of birth its year.
        public string MaskedPan => Pan.Length == 10 ? Pan[..5] + "••••" + Pan[9..] : Pan;

        public string MaskedDob => Dob.Length == 10 ? "••-••-" + Dob[6..] : Dob;

        /// <summary>What the register holds against the record, item by item.</summary>
        public IReadOnlyList<(string Label, bool Held)> OnRecord =>
        [
            ("PAN card", Docs.Pan),
            ("Photograph", Docs.Photo),
            ("Proof of address", Docs.Poa),
            ("Address", Address.Length > 0),
        ];

        public int Missing => OnRecord.Count(i => !i.Held);

        // A first-time investor has no folio at all, so nothing is "missing" from a
        // record that does not exist yet - it is all collected from scratch.
        public string OnRecordTitle => Missing == 0 ? "Everything is on record"
            : Folio.Length == 0 ? "Collected during entry"
            : Missing == 1 ? "1 item missing" : $"{Missing} items missing";

        // The documents are collected at Upload Documents and the address at
        // Investor Information, so the line names the two separately.
        public string OnRecordFoot
        {
            get
            {
                if (Missing == 0) return "Nothing is asked for again during entry.";
                var docs = OnRecord.Take(3).Count(i => !i.Held);
                var where = new List<string>();
                if (docs > 0) where.Add($"the {(docs == 1 ? "document" : "documents")} at Upload Documents");
                if (Address.Length == 0) where.Add("the address at Investor Information");
                return $"You will be asked for {string.Join(" and ", where)}. Carry on with Proceed, or search again if this is not the right {(Folio.Length > 0 ? "record" : "PAN")}.";
            }
        }
    }

    private static string Clean(string? value) => NotAlphanumeric().Replace((value ?? "").ToUpperInvariant(), "");

    private static string Digits(string? value) => NotDigit().Replace(value ?? "", "");

    private static string Pad(string? value) => value is { Length: 1 } ? "0" + value : value ?? "";

    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex PanPattern();

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NotAlphanumeric();

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NotDigit();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // ----- Drafts --------------------------------------------------------------

    /// <summary>
    /// The partner's saved applications, masked. Continue carries only the number:
    /// the backend holds the holder behind it.
    /// </summary>
    public IReadOnlyList<DraftSummary> Drafts { get; set; } = [];
}
