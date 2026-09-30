using System.Text.Json;
using Dapper;
using UnoTP.Backend;

namespace UnoTP.Data;

/// <summary>
/// Where a filed copy is kept, relative to the store's root: {appNo}/{holder}/{slot}{extension}.
/// The store files it there, and t_Unotp_Kyc_Documents records it (c_File_Path).
/// </summary>
public static class DmsPaths
{
    /// <summary>Where a document is kept: application / holder / slot, with the file's own extension.</summary>
    public static string Of(string appNo, string holder, string slot, string fileName) =>
        $"{SafePathSegment(appNo)}/{SafePathSegment(holder)}/{SafePathSegment(slot)}{Extension(fileName)}";

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
/// DMS, until the real one is wired in: each copy is a file under Dms:Root at
/// <see cref="DmsPaths.Of"/>, with its name and type beside it. A refused copy is
/// kept aside under Dms:Root/refused for a week. Only the partner's own
/// application's copies are touched.
/// </summary>
public sealed class FileDocuments(Db db, IPartner partner, IConfiguration config) : IDocumentApi
{
    private readonly string root = Path.GetFullPath(config["Dms:Root"] is { Length: > 0 } r ? r : Path.Combine(AppContext.BaseDirectory, "dms"));

    /// <summary>What is kept beside a stored file: its original name and content type.</summary>
    private sealed record Meta(string FileName, string ContentType);

    // A slot holds one copy: whatever was filed there before goes first.
    public async Task FileAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default)
    {
        if (!await MineAsync(appNo, ct)) throw new InvalidOperationException($"Application {appNo} is not the partner's: nothing filed.");
        foreach (var old in CopiesIn(appNo, holder, slot)) Remove(old);
        var path = Path.Combine(root, DmsPaths.Of(appNo, holder, slot, file.FileName));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, file.Bytes, ct);
        await File.WriteAllTextAsync(path + ".json", JsonSerializer.Serialize(new Meta(file.FileName, file.ContentType)), ct);
    }

    // Deleting a copy that is not there is not a failure.
    public async Task DeleteAsync(string appNo, string holder, string slot, CancellationToken ct = default)
    {
        if (await MineAsync(appNo, ct)) CopiesIn(appNo, holder, slot).ToList().ForEach(Remove);
    }

    public async Task<RefusedCopy> KeepRefusedAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default)
    {
        var reference = "REJ-" + DateTime.UtcNow.ToString("yyMMddHHmmssfff");
        var dir = Path.Combine(root, "refused", reference);
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, DmsPaths.SafePathSegment(file.FileName)), file.Bytes, ct);
        return new RefusedCopy(reference, DateTime.Today.AddDays(7));
    }

    public async Task<UploadFile?> CopyAsync(string appNo, string holder, string slot, CancellationToken ct = default)
    {
        if (!await MineAsync(appNo, ct) || CopiesIn(appNo, holder, slot).FirstOrDefault() is not { } path) return null;
        var meta = File.Exists(path + ".json") ? JsonSerializer.Deserialize<Meta>(await File.ReadAllTextAsync(path + ".json", ct)) : null;
        return new UploadFile(meta?.FileName ?? Path.GetFileName(path), meta?.ContentType ?? "application/octet-stream", await File.ReadAllBytesAsync(path, ct));
    }

    // The copy in a slot, whatever its extension.
    private IEnumerable<string> CopiesIn(string appNo, string holder, string slot)
    {
        var dir = Path.Combine(root, DmsPaths.SafePathSegment(appNo), DmsPaths.SafePathSegment(holder));
        var name = DmsPaths.SafePathSegment(slot);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir).Where(f => !f.EndsWith(".json", StringComparison.Ordinal)
                && (Path.GetFileNameWithoutExtension(f) == name || Path.GetFileName(f) == name))
            : [];
    }

    private static void Remove(string path)
    {
        File.Delete(path);
        File.Delete(path + ".json");
    }

    private async Task<bool> MineAsync(string appNo, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.t_Unotp_Application_Mst WHERE c_App_No = @AppNo AND c_Partner_Id = @Partner AND f_Active = 1",
            new { AppNo = appNo, Partner = partner.Id }) > 0;
    }
}
