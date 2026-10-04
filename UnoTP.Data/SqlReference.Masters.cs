using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

public sealed partial class SqlReference
{
    // ----- The FD system's own masters ------------------------------------------------
    //
    // Marital status, the nominee's and the employee's relation, the occupation and
    // the source of funds are offered as the FD system's masters list them, and saved
    // as its codes. The t_FD_BT_ and t_FD_MMFSL_ masters are in the main database, the
    // t_FD_CMN_ ones in the FD system's common database. Only active rows are offered.

    private sealed record EmployeeRelationRow(string Code, string Name, bool Active);

    /// <param name="selfRelation">
    /// The employee relation's code for the employee themselves (the setting
    /// employeeSelfRelation). It is not chosen on the page - it stands when the employee
    /// is the first holder - so it is taken though the master has it inactive, and
    /// comes first on the list.
    /// </param>
    /// <param name="occupationTypesLeftOut">
    /// The occupation master's type codes the pages do not offer, a semicolon between
    /// (the setting occupationTypesLeftOut): those that are not an individual's.
    /// </param>
    private async Task<MasterLists> MasterListsAsync(string selfRelation, string occupationTypesLeftOut, CancellationToken ct)
    {
        await using var main = await db.OpenAsync(ct);
        var maritalStatuses = (await main.QueryAsync<Option>("""
            SELECT RTRIM(f_MaritalStatus_Code) AS Code, RTRIM(f_MaritalStatus_Name) AS Name
            FROM dbo.t_FD_BT_Marital_Status_Mst WHERE f_Active = 1 ORDER BY f_MaritalStatus_Code
            """)).ToList();
        var employeeRows = (await main.QueryAsync<EmployeeRelationRow>("""
            SELECT RTRIM(f_Relation_Code) AS Code, RTRIM(f_Relation_Name) AS Name, CAST(ISNULL(f_Active, 0) AS BIT) AS Active
            FROM dbo.t_FD_MMFSL_Employee_Relation_Mst ORDER BY f_Relation_Name
            """)).ToList();

        await using var common = await db.OpenAsync(Db.Common, ct);
        var nomineeRelations = (await common.QueryAsync<Option>("""
            SELECT RTRIM(f_Relation_Code) AS Code, RTRIM(f_Relation_Name) AS Name
            FROM dbo.t_FD_CMN_Relation_Mst WHERE f_Active = 1 ORDER BY f_Relation_Name
            """)).ToList();

        var leftOut = occupationTypesLeftOut.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var occupations = (await common.QueryAsync<OccupationRow>("""
            SELECT RTRIM(ISNULL(f_Ckyc_CustSeg_Type_Code, '')) AS TypeCode, RTRIM(ISNULL(f_Ckyc_CustSeg_Type_Desc, '')) AS TypeName,
                   RTRIM(ISNULL(f_Ckyc_CustSeg_SubType_Code, '')) AS SubTypeCode, RTRIM(ISNULL(f_Ckyc_CustSeg_SubType_Desc, '')) AS SubTypeName,
                   RTRIM(ISNULL(f_Ckyc_Occupation_Code, '')) AS OccupationCode, RTRIM(ISNULL(f_Ckyc_Occupation_Desc, '')) AS OccupationName
            FROM dbo.t_FD_CMN_Ckyc_CustSeg_Mst
            ORDER BY TRY_CAST(f_Ckyc_CustSeg_Type_Code AS INT), TRY_CAST(f_Ckyc_CustSeg_SubType_Code AS INT)
            """)).Where(o => !leftOut.Contains(o.TypeCode)).ToList();

        var employeeRelations = new List<Option>();
        foreach (var row in employeeRows)
        {
            if (row.Code == selfRelation) employeeRelations.Insert(0, new Option(row.Code, row.Name));
            else if (row.Active) employeeRelations.Add(new Option(row.Code, row.Name));
        }
        return new MasterLists(maritalStatuses, nomineeRelations, employeeRelations, occupations);
    }

    private async Task<IReadOnlyList<Option>> SourcesOfFundsAsync(CancellationToken ct)
    {
        await using var common = await db.OpenAsync(Db.Common, ct);
        return (await common.QueryAsync<Option>("""
            SELECT RTRIM(f_AML_Source_Of_Funds_Code) AS Code, RTRIM(f_AML_Source_Of_Funds_Desc) AS Name
            FROM dbo.t_FD_CMN_AML_Source_Of_Funds_Mst WHERE f_Active = 1 ORDER BY f_AML_Source_Of_Funds_Code
            """)).ToList();
    }
}
