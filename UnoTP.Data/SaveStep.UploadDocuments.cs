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
    string UserName = "", string Folio = "");

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
    public static async Task WriteUploadAsync(IDbConnection db, IDbTransaction tx, Stamp at, UploadState upload,
        IReadOnlyDictionary<string, DocumentCode> codes)
    {
        await db.ExecuteAsync("""
            INSERT dbo.t_Unotp_Upload_State (c_App_No, n_App_Version, c_Status, j_Upload, c_Created_By)
            VALUES (@AppNo, @Version, @Status, @Json, @By)
            """, new { at.AppNo, at.Version, at.Status, Json = JsonSerializer.Serialize(upload, Json), at.By }, tx);

        // One row per document on the application, coded as the FD system's document
        // master codes it, with what the outside checks made of it (SaveStep.DocumentCheckFlags.cs).
        // Its size and the words of its check stay on the upload step's JSON:
        // t_FD_BT_KYC_document has no column for them.
        await RetireAsync(db, tx, at, "t_FD_BT_KYC_document", "f_UpdatedDate");
        var sequences = new Dictionary<(string, string), int>();
        foreach (var d in Documents(upload))
        {
            var code = codes.GetValueOrDefault(MasterKey(d));
            var holderType = FiledUnder(d.HolderType);
            var flags = DocumentFlags.Of(d.Doc);
            await db.ExecuteAsync("""
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
                at.AppNo, HolderType = holderType, code?.TypeCode, code?.SubTypeCode, code?.TypeName, code?.SubTypeName,
                FileName = Cut(d.Doc.FileName, 250),
                FilePath = d.Doc.Before ? null : DmsPaths.Of(at.AppNo, d.HolderType, d.DocType, d.Doc.FileName),
                Folio = FolioOf(d.HolderType, at, upload),
                Sequence = NextSequence(sequences, holderType, code?.TypeCode ?? d.DocType),
                // A document that came over from the folio or the step before was not uploaded here.
                UploadedFrom = d.Doc.Before ? null : Source,
                flags.Number, flags.Expiry, flags.Masked, flags.OcrAsked, flags.Read, flags.Verified,
                flags.Identified, flags.IdentifiedAs, flags.FaceCompared, flags.FaceFound, flags.FaceScore,
                Source, at.Status, CreatedBy = at.UserClusterId, at.UserName, at.Ip, at.SessionId,
            }, tx);
        }
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
