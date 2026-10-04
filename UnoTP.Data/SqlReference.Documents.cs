using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace UnoTP.Data;

/// <summary>How a document is coded in the FD system's document master: its type and sub-type, with their names.</summary>
public sealed record DocumentCode(string TypeCode, string? TypeName, string SubTypeCode, string? SubTypeName);

// What a save looks up to code a row as the FD system does: the document master, and a CMS location's code.
public sealed partial class SqlReference
{
    /// <summary>The depositors the app takes: individuals, as the document master codes them.</summary>
    private const string DepositorStatus = "IND";

    /// <summary>
    /// How each document the app files is coded in the FD system's document master
    /// (T_FD_CMN_KYC_Document_Sub_Type_Mst and T_FD_CMN_KYC_Document_Type_Mst, in its
    /// common database). The 'documentSubTypes' list in t_Unotp_Ref_List says which
    /// sub-type each is: its code there is the document as the app knows it - "pan",
    /// "poa:Passport", "empproof:Employee ID card" - and its name the master's
    /// sub-type code. The type and the names are read off the master. A document
    /// not on the list, or whose sub-type the master does not hold, is not here.
    /// </summary>
    public Task<IReadOnlyDictionary<string, DocumentCode>> DocumentCodesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync<IReadOnlyDictionary<string, DocumentCode>>("documentCodes", async entry =>
        {
            (entry.AbsoluteExpirationRelativeToNow, entry.Size) = (KeptFor, 1);

            await using var masters = await db.OpenAsync(Db.Masters, ct);
            var listed = await masters.QueryAsync<(string Document, string SubTypeCode)>(
                "SELECT c_Code, c_Name FROM dbo.t_Unotp_Ref_List WHERE c_List = 'documentSubTypes' AND f_Active = 1");

            await using var common = await db.OpenAsync(Db.Common, ct);
            var master = (await common.QueryAsync<DocumentCode>("""
                SELECT s.f_KYC_Document_Type_Code AS TypeCode, t.f_KYC_Document_Type_Desc AS TypeName,
                       s.f_KYC_Document_Sub_Type_Code AS SubTypeCode, s.f_KYC_Document_Sub_Type_Desc AS SubTypeName
                FROM dbo.T_FD_CMN_KYC_Document_Sub_Type_Mst s
                LEFT JOIN dbo.T_FD_CMN_KYC_Document_Type_Mst t
                    ON t.f_KYC_Document_Type_Code = s.f_KYC_Document_Type_Code AND t.f_Depositor_Status_Code = s.f_Depositor_Status_Code
                WHERE s.f_Depositor_Status_Code = @DepositorStatus AND s.f_IsActive = 1
                """, new { DepositorStatus })).ToList();

            var codes = new Dictionary<string, DocumentCode>();
            foreach (var (document, subTypeCode) in listed)
            {
                var code = master.FirstOrDefault(m => m.SubTypeCode == subTypeCode);
                if (code is not null) codes[document] = code;
            }
            return codes;
        })!;

    /// <summary>
    /// The code of an Axis CMS location, by the name the page offers it under (the
    /// 'cmsLocations' list); null for a name not on the list.
    /// </summary>
    public async Task<string?> CmsLocationCodeAsync(string name, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(Db.Masters, ct);
        return await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT c_Code FROM dbo.t_Unotp_Ref_List WHERE c_List = 'cmsLocations' AND c_Name = @Name AND f_Active = 1",
            new { Name = name });
    }
}
