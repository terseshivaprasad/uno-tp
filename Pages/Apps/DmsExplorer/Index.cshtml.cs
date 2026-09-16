using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.DmsExplorer;

/// <summary>One label and value in the viewer's details.</summary>
public record DmsFact(string Label, string Value);

/// <summary>
/// What the viewer shows for one version of one document. Facts are the fields the
/// upload form captured for the document's class; Filed, Dms and Versions follow them
/// for every class.
/// </summary>
public record DmsViewerEntry(
    string Key,
    string DocKey,
    string File,
    List<DmsFact> Facts,
    string Filed,
    string Dms,
    string Versions,
    bool Current);

/// <summary>A way to look a filing up: the identifiers the upload form captures.</summary>
public record DmsLookup(string Key, string Label, string Placeholder);

/// <summary>One line of a control number's history. Only an upload carries a file to open.</summary>
public record DmsHistoryEntry(
    DateTime At,
    string Event,
    string Detail,
    DmsDocument Document,
    DmsViewerEntry Version,
    int VersionNo,
    string By,
    bool IsUpload);

public class IndexModel : PageModel
{
    // Arriving without a lookup opens the board's filing; an explicit empty ?no= is
    // what Clear sends, and shows the empty search.
    public const string DefaultNumber = "123456";

    public static readonly string[] HolderTypeOrder = { "Investor", "Joint holder 1", "Joint holder 2", "Not holder-specific" };

    public static readonly List<DmsLookup> Lookups = new()
    {
        new("control", "Control no", "Control number"),
        new("application", "Application no", "Application number, current or earlier"),
        new("pan", "PAN no", "PAN of a holder on an FD document"),
        new("folio", "Folio no", "Folio number"),
        new("fdr", "FDR no", "FD receipt number"),
    };

    public string Number { get; private set; } = "";

    /// <summary>The lookup key the number is matched on (?by=), control number by default.</summary>
    public DmsLookup By { get; private set; } = Lookups[0];

    /// <summary>True when the page opens on History (?view=history) rather than the current documents.</summary>
    public bool ShowHistory { get; private set; }

    /// <summary>The filing the number resolves to, or null when nothing is filed under it.</summary>
    public DmsFiling? Filing { get; private set; }

    /// <summary>Every version of every document, keyed as the rows and the viewer refer to them.</summary>
    public Dictionary<string, DmsViewerEntry> Viewer { get; } = new();

    /// <summary>Newest first: every upload, replacement and DMS filing under the number.</summary>
    public List<DmsHistoryEntry> History { get; } = new();

    public void OnGet()
    {
        Number = Request.Query.ContainsKey("no") ? Request.Query["no"].ToString().Trim() : DefaultNumber;
        By = Lookups.FirstOrDefault(l => l.Key == Request.Query["by"]) ?? Lookups[0];
        ShowHistory = string.Equals(Request.Query["view"], "history", StringComparison.OrdinalIgnoreCase);

        var filing = MockData.DmsFiling;
        if (Number.Length > 0 && Matches(filing, By.Key, Number))
        {
            Filing = filing;
            BuildVersions(filing);
        }
    }

    /// <summary>
    /// Whether a filing answers a lookup. Application, PAN, folio and FDR numbers match
    /// any version, so a number from an earlier deposit still finds the control number.
    /// </summary>
    private static bool Matches(DmsFiling filing, string by, string number)
    {
        bool Same(string? value) => string.Equals(value, number, StringComparison.OrdinalIgnoreCase);
        var versions = filing.Documents.SelectMany(VersionsOf).ToList();
        var fd = versions.Select(v => v.Fd).OfType<DmsFdDetails>().ToList();
        return by switch
        {
            "application" => Same(filing.ApplicationNo) || versions.Any(v => Same(v.ApplicationNo)),
            "pan" => fd.Any(f => Same(f.Pan)),
            "folio" => fd.Any(f => Same(f.FolioNo)),
            "fdr" => fd.Any(f => Same(f.FdrNo)),
            _ => Same(filing.ControlNo),
        };
    }

    private void BuildVersions(DmsFiling filing)
    {
        var entries = new List<(DmsHistoryEntry Entry, int Order)>();

        for (var i = 0; i < filing.Documents.Count; i++)
        {
            var d = filing.Documents[i];
            var versions = VersionsOf(d);
            var total = versions.Count;

            for (var v = 0; v < total; v++)
            {
                var version = versions[v];
                var no = v + 1;
                var current = no == total;
                var otherApp = version.ApplicationNo is not null && version.ApplicationNo != filing.ApplicationNo
                    ? version.ApplicationNo
                    : null;

                var versionsLine = current
                    ? total switch
                    {
                        1 => "1 · no earlier version",
                        2 => "2 · earlier version kept in the store",
                        _ => $"{total} · {total - 1} earlier versions kept in the store",
                    }
                    : $"{no} of {total} · superseded {Stamp(versions[v + 1].Uploaded)}"
                        + (otherApp is null ? "" : " · application " + otherApp);

                var entry = new DmsViewerEntry(
                    Key: $"d{i}v{no}",
                    DocKey: $"d{i}",
                    File: version.File,
                    Facts: FactsOf(d, version, version.ApplicationNo ?? filing.ApplicationNo),
                    Filed: $"{filing.FiledBy} · {Stamp(version.Uploaded)}",
                    Dms: version.DmsId is null || version.InDms is null
                        ? d.Remark ?? "Not yet in DMS"
                        : $"{version.DmsId} · {Stamp(version.InDms.Value)}",
                    Versions: versionsLine,
                    Current: current);
                Viewer[entry.Key] = entry;

                var uploadDetail = no == 1 ? "version 1" : $"version {no} · replaces version {no - 1}";
                if (no > 1 && versions[v - 1].ReplacedBecause is { } why) uploadDetail += " — " + why;
                if (otherApp is not null) uploadDetail += " · application " + otherApp;

                entries.Add((new DmsHistoryEntry(version.Uploaded, no == 1 ? "Uploaded" : "Replaced", uploadDetail,
                    d, entry, no, filing.FiledBy, IsUpload: true), entries.Count));

                entries.Add(version.InDms is { } inDms && version.DmsId is not null
                    ? (new DmsHistoryEntry(inDms, "Filed in DMS", version.DmsId, d, entry, no, "DMS", IsUpload: false), entries.Count)
                    : (new DmsHistoryEntry(version.Uploaded, "Queued for DMS", d.Remark ?? "not yet in DMS", d, entry, no, "Sync queue", IsUpload: false), entries.Count));
            }
        }

        // Newest first; of two steps in the same minute, the later one leads.
        History.AddRange(entries.OrderByDescending(e => e.Entry.At).ThenByDescending(e => e.Order).Select(e => e.Entry));
    }

    /// <summary>
    /// The fields the upload form captured for a document's class, in the form's order:
    /// KYC, FD or Open (the upload's "Other document", filed under a typed type).
    /// </summary>
    public static List<DmsFact> FactsOf(DmsDocument d, DmsVersion version, string applicationNo)
    {
        var facts = new List<DmsFact> { new("Class", ClassLabel(d.Class)) };
        switch (d.Class)
        {
            case DmsClass.Kyc:
                var (docType, subType) = KycTypeOf(d.Type);
                facts.Add(new("Application no", applicationNo));
                facts.Add(new("Holder type", d.HolderType));
                facts.Add(new("Doc type", docType));
                facts.Add(new("Doc sub-type", subType));
                facts.Add(new("Doc ref no", RefLine(d)));
                facts.Add(new("Doc expiry date", d.Expiry ?? "—"));
                break;
            case DmsClass.Fd:
                var fd = version.Fd;
                facts.Add(new("Holder type", d.HolderType));
                facts.Add(new("Document type", d.Type));
                facts.Add(new("FIN year", fd?.FinYear ?? "—"));
                facts.Add(new("Period", fd?.Period ?? "—"));
                facts.Add(new("PAN no", fd is null ? "—" : MaskPan(fd.Pan) + " · unmasked on open"));
                facts.Add(new("Folio no", fd?.FolioNo ?? "—"));
                facts.Add(new("FDR no", fd?.FdrNo ?? "—"));
                if (d.Expiry is not null) facts.Add(new("Expiry", d.Expiry));
                break;
            default:
                facts.Add(new("Holder type", d.HolderType));
                facts.Add(new("Document type", d.Type + " · typed at upload"));
                facts.Add(new("Application no", applicationNo));
                break;
        }
        return facts;
    }

    /// <summary>A KYC type as the upload form's doc type and sub-type, e.g. Proof of address, Passport.</summary>
    public static (string DocType, string SubType) KycTypeOf(string label)
    {
        foreach (var group in UploadModel.KycTypes)
        {
            foreach (var (sub, option) in group.SubTypes)
            {
                if (option.Label == label) return (group.DocType, sub);
            }
        }
        var parts = label.Split(" · ", 2);
        return (parts[0], parts.Length > 1 ? parts[1] : parts[0]);
    }

    public static string MaskPan(string pan) => pan.Length == 10 ? pan[..5] + "••••" + pan[^1] : pan;

    /// <summary>A document's versions, oldest first, ending with the one in force.</summary>
    public static List<DmsVersion> VersionsOf(DmsDocument d)
    {
        var versions = new List<DmsVersion>(d.Earlier ?? new List<DmsVersion>());
        versions.Add(new DmsVersion(d.File, d.Uploaded, d.InDms, d.DmsId, Fd: d.Fd));
        return versions;
    }

    /// <summary>The folio the filing's current FD documents name, if any.</summary>
    public string? Folio => Filing?.Documents.Select(d => d.Fd?.FolioNo).FirstOrDefault(f => f is not null);

    public DmsViewerEntry CurrentOf(DmsDocument d) =>
        Viewer[$"d{Filing!.Documents.IndexOf(d)}v{VersionsOf(d).Count}"];

    public IEnumerable<IGrouping<string, DmsDocument>> Groups => Filing!.Documents
        .GroupBy(d => d.HolderType)
        .OrderBy(g => Array.IndexOf(HolderTypeOrder, g.Key));

    public IEnumerable<IGrouping<DateOnly, DmsHistoryEntry>> HistoryDays => History
        .GroupBy(e => DateOnly.FromDateTime(e.At));

    public int EarlierVersionCount => Filing!.Documents.Sum(d => d.Earlier?.Count ?? 0);

    public DateTime HistoryFrom => History.Min(e => e.At);

    public int Count(DmsClass cls) => Filing!.Documents.Count(d => d.Class == cls);

    public int HistoryCount(DmsClass cls) => History.Count(e => e.Document.Class == cls);

    public static string Stamp(DateTime at) => at.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture);

    public static string Time(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Day(DateOnly day) => day.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string ClassLabel(DmsClass cls) => cls switch
    {
        DmsClass.Kyc => "KYC",
        DmsClass.Fd => "FD",
        _ => "Open",
    };

    public static string ClassChip(DmsClass cls) => cls switch
    {
        DmsClass.Kyc => "chip chip--primary",
        DmsClass.Fd => "chip chip--success",
        _ => "chip chip--warn",
    };

    /// <summary>
    /// The line under a document's type: its reference, expiry and any note, and for an
    /// FD document the financial year and receipt it was filed for.
    /// </summary>
    public static string SubLine(DmsDocument d) => string.Join(" · ", new[]
    {
        d.Ref is null ? null : "ref " + d.Ref,
        d.Fd is null ? null : "FY " + d.Fd.FinYear,
        d.Fd is null ? null : "FDR " + d.Fd.FdrNo,
        d.Expiry is null ? null : "expires " + d.Expiry,
        d.Note,
    }.Where(p => p is not null));

    public static string RefLine(DmsDocument d) => d.Ref is null ? "—" : d.Ref + " · unmasked on open";

    /// <summary>The footer spells the count out: "Fourteen documents filed under this number".</summary>
    public static string CountWord(int n)
    {
        var words = new[]
        {
            "No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen", "Twenty",
        };
        return n < words.Length ? words[n] : n.ToString(CultureInfo.InvariantCulture);
    }

    public static string DocumentCount(int n) => n == 1 ? "1 document" : $"{n} documents";

    public static string EventCount(int n) => n == 1 ? "1 event" : $"{n} events";
}
