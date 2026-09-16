using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.DmsExplorer;

/// <summary>What the viewer shows for one version of one document.</summary>
public record DmsViewerEntry(
    string Key,
    string DocKey,
    string File,
    string Holder,
    string Type,
    string Ref,
    string Expiry,
    string Filed,
    string Dms,
    string Versions,
    bool Current);

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

    public string Number { get; private set; } = "";

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
        ShowHistory = string.Equals(Request.Query["view"], "history", StringComparison.OrdinalIgnoreCase);

        var filing = MockData.DmsFiling;
        if (Number.Equals(filing.ControlNo, StringComparison.OrdinalIgnoreCase)
            || Number.Equals(filing.ApplicationNo, StringComparison.OrdinalIgnoreCase))
        {
            Filing = filing;
            BuildVersions(filing);
        }
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
                    Holder: d.HolderType,
                    Type: ClassAndType(d),
                    Ref: RefLine(d),
                    Expiry: d.Expiry ?? "—",
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

    /// <summary>A document's versions, oldest first, ending with the one in force.</summary>
    public static List<DmsVersion> VersionsOf(DmsDocument d)
    {
        var versions = new List<DmsVersion>(d.Earlier ?? new List<DmsVersion>());
        versions.Add(new DmsVersion(d.File, d.Uploaded, d.InDms, d.DmsId));
        return versions;
    }

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

    /// <summary>The line under a document's type: its reference, expiry and any note.</summary>
    public static string SubLine(DmsDocument d) => string.Join(" · ", new[]
    {
        d.Ref is null ? null : "ref " + d.Ref,
        d.Expiry is null ? null : "expires " + d.Expiry,
        d.Note,
    }.Where(p => p is not null));

    /// <summary>The viewer's "Class · type" line, e.g. "KYC · proof of address · passport".</summary>
    public static string ClassAndType(DmsDocument d) =>
        ClassLabel(d.Class) + " · " + char.ToLowerInvariant(d.Type[0]) + d.Type[1..];

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
