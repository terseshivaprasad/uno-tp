using UnoTP.Models;

namespace UnoTP.Data;

public sealed partial class SqlReference
{
    // ----- The lists whose choice is saved as a code -----------------------------------
    //
    // Marital status, the nominee's and the employee's relation and the occupation are
    // rows of t_Unotp_Ref_List like every other list: f_Code is what a save writes,
    // f_Name what the page shows, and f_Seq the order they are offered in.

    /// <summary>
    /// The 'maritalStatuses', 'nomineeRelations', 'employeeRelations', 'occupations',
    /// 'subOccupations' and 'subOccupationCkyc' lists. The first of 'employeeRelations'
    /// is the employee themselves. A sub occupation's parent is the occupation it is
    /// under, and the CKYC occupation saved with it is the row under it in
    /// 'subOccupationCkyc'.
    /// </summary>
    private static MasterLists MasterListsOf(ILookup<string, Entry> lists)
    {
        var maritalStatuses = OptionsOf(lists["maritalStatuses"]);
        var nomineeRelations = OptionsOf(lists["nomineeRelations"]);
        var employeeRelations = OptionsOf(lists["employeeRelations"]);

        var occupations = new List<OccupationRow>();
        foreach (var sub in lists["subOccupations"])
        {
            var row = OccupationRowOf(sub, lists["occupations"], lists["subOccupationCkyc"]);
            if (row is not null) occupations.Add(row);
        }
        return new MasterLists(maritalStatuses, nomineeRelations, employeeRelations, occupations);
    }

    private static List<Option> OptionsOf(IEnumerable<Entry> entries)
    {
        var options = new List<Option>();
        foreach (var entry in entries) options.Add(new Option(entry.Code, entry.Name));
        return options;
    }

    // A sub occupation with the occupation it is under (its parent, a code of the
    // 'occupations' list) and the CKYC occupation saved with it (the row of
    // 'subOccupationCkyc' whose parent it is). Null where its occupation is not on the list.
    private static OccupationRow? OccupationRowOf(Entry sub, IEnumerable<Entry> occupations, IEnumerable<Entry> ckycOccupations)
    {
        var occupation = occupations.FirstOrDefault(o => o.Code == sub.Parent);
        if (occupation is null) return null;
        var ckyc = ckycOccupations.FirstOrDefault(c => c.Parent == sub.Code);
        return new OccupationRow(occupation.Code, occupation.Name, sub.Code, sub.Name, ckyc?.Code ?? "", ckyc?.Name ?? "");
    }
}
