using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

public sealed partial class SqlReference
{
    // ----- The FD system's own masters ------------------------------------------------
    //
    // Marital status, the nominee's and the employee's relation, the occupation and
    // the source of funds are offered as the FD system's masters list them, and saved
    // as its codes. The query that reads each one is in MasterQueries. Only active rows
    // are offered.

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
        // Each master's own query is in MasterQueries: these sort what they give back.
        await using var marital = await db.OpenMastersAsync(ct);
        var maritalStatuses = (await marital.QueryAsync<Option>(
            $"SELECT m.Code, m.Name FROM ({MasterQueries.MaritalStatuses}) m ORDER BY m.Code")).ToList();

        await using var employee = await db.OpenMastersAsync(ct);
        var employeeRows = (await employee.QueryAsync<EmployeeRelationRow>(
            $"SELECT e.Code, e.Name, e.Active FROM ({MasterQueries.EmployeeRelations}) e ORDER BY e.Name")).ToList();

        await using var nominee = await db.OpenMastersAsync(ct);
        var nomineeRelations = (await nominee.QueryAsync<Option>(
            $"SELECT n.Code, n.Name FROM ({MasterQueries.NomineeRelations}) n ORDER BY n.Name")).ToList();

        var leftOut = occupationTypesLeftOut.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await using var occupation = await db.OpenMastersAsync(ct);
        var occupations = (await occupation.QueryAsync<OccupationRow>($"""
            SELECT o.TypeCode, o.TypeName, o.SubTypeCode, o.SubTypeName, o.OccupationCode, o.OccupationName
            FROM ({MasterQueries.Occupations}) o
            ORDER BY TRY_CAST(o.TypeCode AS INT), TRY_CAST(o.SubTypeCode AS INT)
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
        await using var connection = await db.OpenMastersAsync(ct);
        return (await connection.QueryAsync<Option>(
            $"SELECT s.Code, s.Name FROM ({MasterQueries.SourcesOfFunds}) s ORDER BY s.Code")).ToList();
    }
}
