using System.Globalization;
using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>
/// The search of a list page (View Application, Pay In Slip Generation) as its address
/// asks for it. The page sends the search to the server, and the database reads what
/// it reaches: a list is never read whole and searched in the browser.
/// </summary>
public static class ListSearch
{
    /// <summary>The rows a page of a list holds.</summary>
    public const int PerPage = 8;

    /// <summary>The oldest day a list opens on: the days an unpaid application stands, today included.</summary>
    public static DateTime Earliest(int windowDays) => DateTime.Today.AddDays(-(windowDays - 1));

    /// <summary>The search a list opens on: every day of the window.</summary>
    public static ApplicationSearch Opening(int windowDays) =>
        ApplicationSearch.RaisedBetween(Earliest(windowDays), DateTime.Today);

    /// <summary>Whether a search is the one the list opens on.</summary>
    public static bool IsOpening(ApplicationSearch search, int windowDays) => search == Opening(windowDays);

    /// <summary>
    /// The search an address asks for. One the page could not have sent - too few
    /// characters, a date that is not one, days outside the window, a folio on a page
    /// that has no folio search - is answered with the list the page opens on.
    /// </summary>
    public static ApplicationSearch Asked(string? by, string? typed, string? from, string? to, int windowDays, bool folioToo)
    {
        var opening = Opening(windowDays);

        if (by == ApplicationSearch.ByNumber || (by == ApplicationSearch.ByFolio && folioToo))
        {
            var text = NumberTyped(typed);
            if (text.Length < ApplicationSearch.FromLength) return opening;
            if (by == ApplicationSearch.ByNumber) return ApplicationSearch.Number(text);
            return ApplicationSearch.Folio(text);
        }

        if (DayOf(from) is not { } first || DayOf(to) is not { } last) return opening;
        if (first > last || first < opening.From || last > opening.To) return opening;
        return ApplicationSearch.RaisedBetween(first, last);
    }

    /// <summary>What a page says when a search reaches more than it lists.</summary>
    public static string MoreExistNote =>
        $"This search reaches more than {ApplicationSearch.Most} applications. The newest {ApplicationSearch.Most} are listed: enter more of the number, or fewer days, to reach the others.";

    // What was typed of a number, as its box takes it: letters, digits, - and /.
    private static string NumberTyped(string? typed) =>
        new((typed ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '/').ToArray());

    // A day as the page sends it, yyyy-MM-dd; null when it is not one.
    private static DateTime? DayOf(string? value)
    {
        var read = DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day);
        return read ? day : null;
    }
}
