namespace UnoTP.Models;

/// <summary>
/// Where an application's documents are filed (DMS). Filing is all this does: the
/// checks a copy goes through first - identification, masking, OCR, verification,
/// the PAN-Aadhaar link - are services of their own (see the External folder).
///
/// Every copy is filed as a file of its own, named after the application, the
/// holder and the document (see <see cref="DocumentLabel"/>). Nothing is replaced
/// or deleted: a copy filed again leaves the earlier one where it is, for audit.
/// </summary>
public interface IDocumentApi
{
    /// <summary>Files a copy as a new file on the application, and gives back the name it is kept under.</summary>
    Task<string> FileAsync(string appNo, DocumentLabel label, UploadFile file, CancellationToken ct = default);

    /// <summary>
    /// Keeps a refused copy aside for analysis, off the application, and says the
    /// reference it is kept under and when it is deleted.
    /// </summary>
    Task<RefusedCopy> KeepRefusedAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default);

    /// <summary>The copy kept on the application under this name, or null.</summary>
    Task<UploadFile?> CopyAsync(string appNo, string fileName, CancellationToken ct = default);
}

/// <summary>What a filed copy's name is made from, beside its application number.</summary>
/// <param name="Folio">The holder's folio; empty for a holder with none.</param>
/// <param name="HolderType">01, 02 or 03. The application's own documents go under 01, as their rows do.</param>
/// <param name="Document">The document as the app knows it: "pan", "photo", "poa:Passport", "payment".
/// Its sub-type code in the document master goes in the name.</param>
public sealed record DocumentLabel(string Folio, string HolderType, string Document);

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
