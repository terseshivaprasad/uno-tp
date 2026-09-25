namespace UnoTP.Backend;

/// <summary>
/// Where an application's documents are filed (DMS). Filing is all this does: the
/// checks a copy goes through first - identification, masking, OCR, verification,
/// the PAN-Aadhaar link - are services of their own (see the External folder).
/// </summary>
public interface IDocumentApi
{
    /// <summary>Files a copy against the application's slot. A slot holds one copy:
    /// one filed before is deleted first (see <see cref="DeleteAsync"/>).</summary>
    Task FileAsync(string appNo, string slot, UploadFile file, CancellationToken ct = default);

    /// <summary>Deletes the copy filed against a slot, if there is one.</summary>
    Task DeleteAsync(string appNo, string slot, CancellationToken ct = default);

    /// <summary>
    /// Keeps a refused copy aside for analysis, off the application, and says the
    /// reference it is kept under and when it is deleted.
    /// </summary>
    Task<RefusedCopy> KeepRefusedAsync(string appNo, string slot, UploadFile file, CancellationToken ct = default);

    /// <summary>The copy filed against a slot, or null.</summary>
    Task<UploadFile?> CopyAsync(string appNo, string slot, CancellationToken ct = default);
}

/// <summary>A file as it was handed over.</summary>
public sealed record UploadFile(string FileName, string ContentType, byte[] Bytes);

public sealed record RefusedCopy(string Ref, DateTime KeptUntil);
