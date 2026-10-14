using System.Data;
using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// What a list's search asks SQL Server for. A list has one query for every search:
/// each condition in it reads (@Value IS NULL OR ...), so the search's own values are
/// given and the others are sent as NULL.
/// </summary>
internal static class SearchParameters
{
    /// <summary>
    /// The days and the number a search asks for, added to a query's parameters: @From
    /// and @Until for a search by dates, @Number for one by application number. Those
    /// the search does not use are NULL.
    /// </summary>
    public static void AddSearch(this DynamicParameters asked, ApplicationSearch? search)
    {
        asked.Add("From", search?.From, DbType.DateTime2);
        asked.Add("Until", search?.To?.AddDays(1), DbType.DateTime2);
        asked.Add("Number", Holding(search, ApplicationSearch.ByNumber), DbType.AnsiString);
    }

    /// <summary>
    /// The LIKE pattern for a number that holds what was typed anywhere in it, when the
    /// search is the kind asked for; NULL when it is another kind. LIKE's own characters
    /// are left out, so what was typed is only ever looked for as it reads.
    /// </summary>
    public static string? Holding(ApplicationSearch? search, string by)
    {
        if (search is null || search.By != by) return null;
        var text = search.Text.Replace("%", "").Replace("_", "").Replace("[", "");
        return "%" + text + "%";
    }
}
