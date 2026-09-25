namespace UnoTP.Backend;

/// <summary>
/// Where an application's documents are filed (DMS). Filing is all this does: the
/// checks a copy goes through first - identification, masking, OCR, verification,
/// the PAN-Aadhaar link - are services of their own (see the External folder).
///
/// A document is filed under a holder type and a slot: a KYC document - the PAN
/// copy, the photograph, the proof of address - under the holder it belongs to,
/// and everything else under <see cref="HolderType.None"/>.
/// </summary>
public interface IDocumentApi
{
    /// <summary>Files a copy against the holder's slot. A slot holds one copy:
    /// one filed before is deleted first (see <see cref="DeleteAsync"/>).</summary>
    Task FileAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default);

    /// <summary>Deletes the copy filed against the holder's slot, if there is one.</summary>
    Task DeleteAsync(string appNo, string holder, string slot, CancellationToken ct = default);

    /// <summary>
    /// Keeps a refused copy aside for analysis, off the application, and says the
    /// reference it is kept under and when it is deleted.
    /// </summary>
    Task<RefusedCopy> KeepRefusedAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default);

    /// <summary>The copy filed against the holder's slot, or null.</summary>
    Task<UploadFile?> CopyAsync(string appNo, string holder, string slot, CancellationToken ct = default);
}

/// <summary>The holder type a document is filed under, as DMS codes it.</summary>
public static class HolderType
{
    /// <summary>Not holder-specific: the application form, the instrument, an employee proof.</summary>
    public const string None = "00";

    public const string Investor = "01";

    public const string Second = "02";

    public const string Third = "03";
}

/// <summary>A file as it was handed over.</summary>
public sealed record UploadFile(string FileName, string ContentType, byte[] Bytes);

public sealed record RefusedCopy(string Ref, DateTime KeptUntil);
