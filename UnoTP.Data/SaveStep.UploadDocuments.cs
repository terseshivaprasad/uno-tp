using System.Data;
using System.Text.Json;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>Who wrote a set of rows, at which of the application's versions, and as what.</summary>
/// <param name="SessionId">The partner's backend session, when it is a number; null otherwise.</param>
/// <param name="Ip">The address the partner's browser called from; empty when not known.</param>
/// <param name="UserClusterId">The signed-in user's Agency_Usr_Clustered_ID; empty when not known.</param>
/// <param name="UserName">The signed-in user's name; empty when not known.</param>
/// <param name="Folio">The investor's folio; empty for an investor with none.</param>
internal sealed record Stamp(string AppNo, int Version, string Status, string By, long? SessionId = null, string Ip = "", string UserClusterId = "",
    string UserName = "", string Folio = "")
{
    /// <summary>
    /// What the app's own tables carry: PEN for a draft, APR once submitted, whatever
    /// status the FD system's rows are given (a CKYC application's are PEN_E).
    /// </summary>
    public string OwnStatus => Status == RowStatus.Pending ? RowStatus.Pending : RowStatus.Approved;
}

/// <summary>
/// Writes each section of an application as rows in the FD system's own tables
/// (t_FD_BT_...). Those tables have no version column, so a section's current rows
/// are the application's active ones: a save first takes the rows it replaces out
/// of use (f_Active = 0), then inserts the section afresh. Nothing is deleted, so
/// the earlier rows stay on record as the section's history.
///
/// The steps are in files of their own: SaveStep.InvestorInformation.cs, SaveStep.BankAndPayment.cs
/// and SaveStep.FdConfiguration.cs.
/// </summary>
internal static partial class Sections
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>What every row written from here carries in f_Source.</summary>
    private const string Source = "UNO_TP";

    // ----- Upload Documents: t_Unotp_Upload_State, t_FD_BT_KYC_document ----------------

    /// <param name="codes">How each document is coded in the FD system's document master (SqlReference.DocumentCodesAsync).</param>
    /// <param name="dmsRoot">The document store's root (DmsPaths.Root), which each copy's path is recorded under.</param>
    public static async Task WriteUploadAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState upload,
        IReadOnlyDictionary<string, DocumentCode> codes, string dmsRoot)
    {
        await db.ExecuteAsync("""
            INSERT dbo.t_Unotp_Upload_State (f_App_No, f_App_Version, f_Status, f_Upload, f_Created_By)
            VALUES (@AppNo, @Version, @Status, @Json, @By)
            """, new { at.AppNo, at.Version, Status = at.OwnStatus, Json = JsonSerializer.Serialize(upload, Json), at.By }, tx);

        // One row per document on the application, coded as the FD system's document
        // master codes it, with what the outside checks made of it (SaveStep.DocumentCheckFlags.cs).
        // Its size and the words of its check stay on the upload step's JSON:
        // t_FD_BT_KYC_document has no column for them.
        //
        // Only what changed is written. A document whose row already says all of this
        // is left as it is; one that is new, or says something else now (another copy,
        // a check's answer, the status on submit), has its old row taken out of use and
        // a new one inserted; one no longer on the application has its row taken out
        // of use. So a row taken out of use is always an earlier state, never a copy.
        var active = (await db.QueryAsync<DocumentRow>(DocumentRow.Select, new { at.AppNo }, tx)).ToList();
        var sequences = new Dictionary<(string, string), int>();
        foreach (var d in Documents(upload))
        {
            var code = codes.GetValueOrDefault(MasterKey(d));
            var holderType = FiledUnder(d.HolderType);
            var flags = DocumentFlags.Of(d.Doc);
            var row = new DocumentRow
            {
                HolderType = holderType, TypeCode = code?.TypeCode, SubTypeCode = code?.SubTypeCode, TypeName = code?.TypeName, SubTypeName = code?.SubTypeName,
                FileName = Cut(d.Doc.FileName, 250),
                FilePath = d.Doc.Before ? null : DmsPaths.Under(dmsRoot, at.AppNo, d.Doc.FileName),
                Folio = FolioOf(d.HolderType, at, upload),
                Sequence = NextSequence(sequences, holderType, code?.TypeCode ?? d.DocType),
                // A document that came over from the folio or the step before was not uploaded here.
                UploadedFrom = d.Doc.Before ? null : Source,
                Number = flags.Number, Expiry = flags.Expiry, Masked = flags.Masked, OcrAsked = flags.OcrAsked, Read = flags.Read, Verified = flags.Verified,
                Identified = flags.Identified, IdentifiedAs = flags.IdentifiedAs, FaceCompared = flags.FaceCompared, FaceFound = flags.FaceFound, FaceScore = flags.FaceScore,
                Status = at.Status,
            };

            // The row already there says the same: it stands.
            var same = active.FirstOrDefault(there => there with { Id = 0 } == row);
            if (same is not null)
            {
                active.Remove(same);
                continue;
            }
            await InsertDocumentAsync(db, tx, at, row);
        }

        // What is left was replaced, or is no longer on the application.
        if (active.Count > 0)
        {
            await db.ExecuteAsync("""
                UPDATE dbo.t_FD_BT_KYC_document
                SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, f_UpdatedDate = GETDATE(), f_UpdatedIP = @Ip
                WHERE f_Pk_t_FD_BT_KYC_document_ID IN @Ids
                """, new { Ids = active.Select(row => row.Id).ToList(), UpdatedBy = at.UserClusterId, at.UserName, at.Ip }, tx);
        }
    }

    private static Task InsertDocumentAsync(IDbConnection db, IDbTransaction tx, Stamp at, DocumentRow row) =>
        db.ExecuteAsync("""
            INSERT dbo.t_FD_BT_KYC_document (f_Appl_No, f_Holder_Type_Code, f_Doc_Type_Code, f_Doc_Sub_Type_Code, f_Doc_Type_Desc, f_Doc_Sub_Type_Desc,
                f_Doc_FileName, f_Doc_Filepath, f_FolioNo, f_Doc_Sequence, f_Document_Source, f_doc_source, f_Doc_Ref_No, f_Doc_Exp_Date,
                f_IsDocumentMasked, f_is_ocrextract, f_isocrdataextract, f_isocrdataverified,
                f_IsidfyDocIdentified, f_IdfyIdentifiedDocument, f_IsidfyDocDataExtracted, f_IsidfyDocDataVerified,
                f_IsidfyFaceCompared, f_IsidfyDocFaceDetected, f_IdfyFaceMatchPercentage,
                f_Source, f_Status, f_Active, f_CreatedBy, f_CreatedByUName, f_CreatedDate, f_CreatedIP, f_Session_ID)
            VALUES (@AppNo, @HolderType, @TypeCode, @SubTypeCode, @TypeName, @SubTypeName,
                @FileName, @FilePath, @Folio, @Sequence, @UploadedFrom, @UploadedFrom, @Number, @Expiry,
                @Masked, @OcrAsked, @Read, @Verified,
                @Identified, @IdentifiedAs, @Read, @Verified,
                @FaceCompared, @FaceFound, @FaceScore,
                @Source, @Status, 1, @CreatedBy, @UserName, GETDATE(), @Ip, @SessionId)
            """, new
        {
            at.AppNo, row.HolderType, row.TypeCode, row.SubTypeCode, row.TypeName, row.SubTypeName,
            row.FileName, row.FilePath, row.Folio, row.Sequence, row.UploadedFrom, row.Number, row.Expiry,
            row.Masked, row.OcrAsked, row.Read, row.Verified,
            row.Identified, row.IdentifiedAs, row.FaceCompared, row.FaceFound, row.FaceScore,
            Source, row.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
        }, tx);

    /// <summary>
    /// What a row of t_FD_BT_KYC_document says of a document, as the app writes it.
    /// Two rows that are equal but for their <see cref="Id"/> say the same thing, and
    /// the one already in the table is left as it is.
    /// </summary>
    private sealed record DocumentRow
    {
        /// <summary>The application's rows in use, read under the names below.</summary>
        public const string Select = """
            SELECT f_Pk_t_FD_BT_KYC_document_ID AS Id, f_Holder_Type_Code AS HolderType,
                   f_Doc_Type_Code AS TypeCode, f_Doc_Sub_Type_Code AS SubTypeCode, f_Doc_Type_Desc AS TypeName, f_Doc_Sub_Type_Desc AS SubTypeName,
                   f_Doc_FileName AS FileName, f_Doc_Filepath AS FilePath, f_FolioNo AS Folio, f_Doc_Sequence AS Sequence,
                   f_Document_Source AS UploadedFrom, f_Doc_Ref_No AS Number, f_Doc_Exp_Date AS Expiry,
                   f_IsDocumentMasked AS Masked, f_is_ocrextract AS OcrAsked, f_isocrdataextract AS [Read], f_isocrdataverified AS Verified,
                   f_IsidfyDocIdentified AS Identified, f_IdfyIdentifiedDocument AS IdentifiedAs,
                   f_IsidfyFaceCompared AS FaceCompared, f_IsidfyDocFaceDetected AS FaceFound, CAST(f_IdfyFaceMatchPercentage AS INT) AS FaceScore,
                   f_Status AS Status
            FROM dbo.t_FD_BT_KYC_document
            WHERE f_Appl_No = @AppNo AND f_Active = 1
            """;

        /// <summary>The row's own number in the table; 0 for one not inserted yet.</summary>
        public long Id { get; init; }
        public string? HolderType { get; init; }
        public string? TypeCode { get; init; }
        public string? SubTypeCode { get; init; }
        public string? TypeName { get; init; }
        public string? SubTypeName { get; init; }
        public string? FileName { get; init; }
        public string? FilePath { get; init; }
        public string? Folio { get; init; }
        public string? Sequence { get; init; }
        public string? UploadedFrom { get; init; }
        public string? Number { get; init; }
        public DateTime? Expiry { get; init; }
        public bool? Masked { get; init; }
        public bool? OcrAsked { get; init; }
        public bool? Read { get; init; }
        public bool? Verified { get; init; }
        public bool? Identified { get; init; }
        public string? IdentifiedAs { get; init; }
        public bool? FaceCompared { get; init; }
        public bool? FaceFound { get; init; }
        public int? FaceScore { get; init; }
        public string? Status { get; init; }
    }

    // f_Doc_Sequence: 1, 2, 3... among a holder's documents of one type.
    private static string NextSequence(Dictionary<(string, string), int> sequences, string holderType, string docType)
    {
        var next = sequences.GetValueOrDefault((holderType, docType)) + 1;
        sequences[(holderType, docType)] = next;
        return next.ToString();
    }

    // f_FolioNo on a holder's row: their own folio. The application's own rows
    // carry the investor's.
    private static string FolioOf(string holderType, Stamp at, UploadState? upload)
    {
        if (holderType == HolderType.Investor || holderType == HolderType.None) return at.Folio;
        return upload?.Joint.GetValueOrDefault(holderType)?.Holder.Folio ?? "";
    }

    // The application's own documents - the form, the cheque, an employee proof, the
    // Form 121 - go under the first holder: f_Holder_Type_Code has no "00".
    private static string FiledUnder(string holderType)
    {
        if (holderType == HolderType.None) return HolderType.Investor;
        return holderType;
    }

    // What a document is listed under in 'filedDocuments': the slot it was filed in
    // and, for a proof, which one it is. A communication address is proved by a
    // proof of address, so it is listed as one.
    private static string MasterKey(Document d)
    {
        if (d.DocType == "poa" || d.DocType == "mail") return "poa:" + d.SubType;
        if (d.DocType == "empproof") return "empproof:" + d.SubType;
        return d.DocType;
    }

    private sealed record Document(string HolderType, string DocType, string SubType, StoredDoc Doc);

    // A document's key says whose and which it is: the investor's "pan", "photo",
    // "poa", "mail"; a joint holder's "h02-pan"; anything else is the application's
    // own ("form", "payment", "empproof", "tdsform").
    private static IEnumerable<Document> Documents(UploadState u)
    {
        foreach (var (key, doc) in u.Docs.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            var (holder, type) = key.Length > 4 && key[0] == 'h' && key[3] == '-'
                ? (key[1..3], key[4..])
                : (key is "pan" or "photo" or "poa" or "mail" ? HolderType.Investor : HolderType.None, key);
            var joint = u.Joint.GetValueOrDefault(holder);
            var sub = type switch
            {
                "poa" => holder == HolderType.Investor ? u.PoaType : joint?.PoaType ?? "",
                "mail" => holder == HolderType.Investor ? u.MailPoaType : joint?.MailPoaType ?? "",
                "payment" => u.PayMode,
                "empproof" => u.EmpProofType,
                "form" => u.AppType,
                _ => "",
            };
            yield return new Document(holder, type, sub, doc);
        }
    }

    // ----- Shared ----------------------------------------------------------------

    // The rows a save replaces go out of use, so the section's current rows are the
    // application's active ones. The table and column names are this file's own.
    private static Task RetireAsync(IDbConnection db, IDbTransaction tx, Stamp at, string table, string updatedOn = "f_UpdatedOn") =>
        db.ExecuteAsync($"""
            UPDATE dbo.{table}
            SET f_Active = 0, f_UpdatedBy = @UpdatedBy, f_UpdatedByUName = @UserName, {updatedOn} = GETDATE(), f_UpdatedIP = @Ip
            WHERE f_Appl_No = @AppNo AND f_Active = 1
            """, new { at.AppNo, UpdatedBy = at.UserClusterId, at.UserName, at.Ip }, tx);

    // A value cut to what its column holds, so a long one never fails the save.
    private static string Cut(string? value, int length)
    {
        if (value is null) return "";
        if (value.Length <= length) return value;
        return value[..length];
    }
}
