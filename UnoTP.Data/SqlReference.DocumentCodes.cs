using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace UnoTP.Data;

/// <summary>How a document is coded when it is filed: its type and sub-type, with their names.</summary>
public sealed record DocumentCode(string TypeCode, string? TypeName, string SubTypeCode, string? SubTypeName);

// What a save looks up to code a row as the FD system does: a document's codes, and a CMS location's code.
public sealed partial class SqlReference
{
    /// <summary>
    /// How each document the app files is coded, from three lists in t_Unotp_Ref_List:
    ///
    ///   'filedDocuments'    the document as the app knows it - "pan", "poa:Passport",
    ///                       "empproof:Employee ID card" - under the sub-type it is filed as
    ///   'documentSubTypes'  the sub-type's code and name, under its document type
    ///   'documentTypes'     the type's code and name
    ///
    /// A document not on the first list, or whose sub-type or type is not on the
    /// others, is not here.
    /// </summary>
    public Task<IReadOnlyDictionary<string, DocumentCode>> DocumentCodesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync<IReadOnlyDictionary<string, DocumentCode>>("documentCodes", async entry =>
        {
            (entry.AbsoluteExpirationRelativeToNow, entry.Size) = (KeptFor, 1);

            await using var connection = await db.OpenAsync(ct);
            var lists = (await connection.QueryAsync<Entry>("""
                SELECT f_List AS List, f_Code AS Code, f_Name AS Name, f_Parent AS Parent, f_Attrs AS Attrs
                FROM dbo.t_Unotp_Ref_List
                WHERE f_List IN ('filedDocuments', 'documentSubTypes', 'documentTypes') AND f_Active = 1
                """)).ToLookup(e => e.List);

            var codes = new Dictionary<string, DocumentCode>();
            foreach (var document in lists["filedDocuments"])
            {
                var subType = lists["documentSubTypes"].FirstOrDefault(s => s.Code == document.Parent);
                if (subType is null) continue;
                var type = lists["documentTypes"].FirstOrDefault(t => t.Code == subType.Parent);
                if (type is null) continue;
                codes[document.Code] = new DocumentCode(type.Code, type.Name, subType.Code, subType.Name);
            }
            return codes;
        })!;
}
