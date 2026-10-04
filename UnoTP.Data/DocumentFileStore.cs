using System.Globalization;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// Where a filed copy is kept, and what it is called. Each copy is a file of its own
/// in its application's folder under the store's root (Dms:Root):
///
///   a holder with no folio   {ApplNo}_{HolderType}_{DocSubType}_{yyyyMMddHHmmssfff}.ext
///   a holder on a folio      {Folio}_{ApplNo}_{HolderType}_{DocSubType}_{yyyyMMddHHmmssfff}.ext
///
/// The time in the name makes every upload a new file, so nothing is overwritten.
/// t_FD_BT_KYC_document records the name (f_Doc_FileName) and the full path (f_Doc_Filepath).
/// </summary>
public static class DmsPaths
{
    /// <summary>The store's root: Dms:Root, or a "dms" folder beside the app when none is set.</summary>
    public static string Root(IConfiguration config)
    {
        var configured = config["Dms:Root"];
        if (string.IsNullOrEmpty(configured)) configured = Path.Combine(AppContext.BaseDirectory, "dms");
        return Path.GetFullPath(configured);
    }

    /// <summary>The name a copy is filed under. The extension is the uploaded file's own.</summary>
    /// <param name="folio">The holder's folio; empty for a holder with none, and then left out of the name.</param>
    /// <param name="subType">The document's sub-type code, as the document master has it.</param>
    /// <param name="filedAt">When it is filed, to the millisecond.</param>
    public static string FileName(string folio, string appNo, string holderType, string subType, DateTime filedAt, string uploadedName)
    {
        var parts = new List<string>();
        if (folio.Trim().Length > 0) parts.Add(SafePathSegment(folio.Trim()));
        parts.Add(SafePathSegment(appNo));
        parts.Add(SafePathSegment(holderType));
        parts.Add(SafePathSegment(subType));
        parts.Add(filedAt.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
        return string.Join("_", parts) + Extension(uploadedName);
    }

    /// <summary>Where a copy is kept in full: the root, the application's folder, the file.</summary>
    public static string Under(string root, string appNo, string fileName) =>
        Path.GetFullPath(Path.Combine(root, SafePathSegment(appNo), SafePathSegment(fileName)));

    // The uploaded file's own extension, so the copy opens as what it is.
    private static string Extension(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() is { Length: > 1 and <= 6 } ext && ext[1..].All(char.IsAsciiLetterOrDigit) ? ext : "";

    // A path segment from the request, with nothing in it that climbs out of the root.
    public static string SafePathSegment(string segment)
    {
        var clean = new string(segment.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.').ToArray()).Trim('.');
        return clean.Length > 0 ? clean : "_";
    }
}

/// <summary>
/// DMS, until the real one is wired in: each copy is a file of its own under
/// Dms:Root, in its application's folder, named by <see cref="DmsPaths.FileName"/>.
/// Nothing filed is replaced or deleted. A refused copy is kept aside under
/// Dms:Root/refused for a week. Only the partner's own application's copies are touched.
/// </summary>
public sealed class FileDocuments(Db db, IPartner partner, SqlReference reference, IConfiguration config) : IDocumentApi
{
    private readonly string root = DmsPaths.Root(config);

    public async Task<string> FileAsync(string appNo, DocumentLabel label, UploadFile file, CancellationToken ct = default)
    {
        if (!await MineAsync(appNo, ct)) throw new InvalidOperationException($"Application {appNo} is not the partner's: nothing filed.");

        var name = DmsPaths.FileName(label.Folio, appNo, label.HolderType, await SubTypeOfAsync(label.Document, ct), DateTime.Now, file.FileName);
        var path = DmsPaths.Under(root, appNo, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, file.Bytes, ct);
        return name;
    }

    // The sub-type code the document master files a document under; the document's
    // own name where the master does not list it.
    private async Task<string> SubTypeOfAsync(string document, CancellationToken ct)
    {
        var codes = await reference.DocumentCodesAsync(ct);
        if (codes.TryGetValue(document, out var code)) return code.SubTypeCode;
        return document;
    }

    public async Task<RefusedCopy> KeepRefusedAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default)
    {
        var keptAs = "REJ-" + DateTime.UtcNow.ToString("yyMMddHHmmssfff");
        var dir = Path.Combine(root, "refused", keptAs);
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, DmsPaths.SafePathSegment(file.FileName)), file.Bytes, ct);
        return new RefusedCopy(keptAs, DateTime.Today.AddDays(7));
    }

    public async Task<UploadFile?> CopyAsync(string appNo, string fileName, CancellationToken ct = default)
    {
        if (!await MineAsync(appNo, ct)) return null;
        var path = DmsPaths.Under(root, appNo, fileName);
        if (!File.Exists(path)) return null;
        return new UploadFile(Path.GetFileName(path), "application/octet-stream", await File.ReadAllBytesAsync(path, ct));
    }

    private async Task<bool> MineAsync(string appNo, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.t_Unotp_Application_Mst WHERE f_App_No = @AppNo AND f_Partner_Id = @Partner AND f_Active = 1",
            new { AppNo = appNo, Partner = partner.Id }) > 0;
    }
}
