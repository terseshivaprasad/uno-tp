using UnoTP.Models;
using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>
/// The search a list page sends to the database: what an address asks for, and what
/// is answered when the address asks for something the page could not have sent.
/// </summary>
public class ApplicationSearchTests
{
    private const int WindowDays = 14;
    private const string Iso = "yyyy-MM-dd";

    private static DateTime Today => DateTime.Today;

    private static DateTime Earliest => Today.AddDays(-(WindowDays - 1));

    private static ApplicationSearch Asked(string? by = null, string? typed = null, DateTime? from = null, DateTime? to = null, bool folioToo = true) =>
        ListSearch.Asked(by, typed, from?.ToString(Iso), to?.ToString(Iso), WindowDays, folioToo);

    private static void AssertOpeningList(ApplicationSearch search)
    {
        Assert.Equal(ApplicationSearch.ByDates, search.By);
        Assert.Equal(Earliest, search.From);
        Assert.Equal(Today, search.To);
    }

    [Fact]
    public void An_address_with_no_search_opens_on_the_window()
    {
        AssertOpeningList(Asked());
    }

    [Fact]
    public void Days_inside_the_window_are_searched_as_asked()
    {
        var search = Asked(ApplicationSearch.ByDates, from: Today.AddDays(-5), to: Today.AddDays(-2));

        Assert.Equal(ApplicationSearch.ByDates, search.By);
        Assert.Equal(Today.AddDays(-5), search.From);
        Assert.Equal(Today.AddDays(-2), search.To);
    }

    [Fact]
    public void Days_before_the_window_are_not_searched()
    {
        AssertOpeningList(Asked(ApplicationSearch.ByDates, from: Earliest.AddDays(-1), to: Today));
    }

    [Fact]
    public void A_day_still_to_come_is_not_searched()
    {
        AssertOpeningList(Asked(ApplicationSearch.ByDates, from: Earliest, to: Today.AddDays(1)));
    }

    [Fact]
    public void A_first_day_after_the_last_is_not_searched()
    {
        AssertOpeningList(Asked(ApplicationSearch.ByDates, from: Today, to: Today.AddDays(-1)));
    }

    [Fact]
    public void A_date_that_is_not_one_is_not_searched()
    {
        AssertOpeningList(ListSearch.Asked(ApplicationSearch.ByDates, null, "31-02-2026", "x", WindowDays, folioToo: true));
    }

    [Fact]
    public void A_number_is_searched_from_four_characters()
    {
        var search = Asked(ApplicationSearch.ByNumber, " 0138 ");

        Assert.Equal(ApplicationSearch.ByNumber, search.By);
        Assert.Equal("0138", search.Text);
        Assert.Null(search.From);
    }

    [Fact]
    public void A_number_of_three_characters_is_not_searched()
    {
        AssertOpeningList(Asked(ApplicationSearch.ByNumber, "013"));
    }

    [Fact]
    public void Characters_a_number_never_has_are_not_searched_for()
    {
        Assert.Equal("0138", Asked(ApplicationSearch.ByNumber, "0_1%3[8").Text);
        AssertOpeningList(Asked(ApplicationSearch.ByNumber, "____"));
    }

    [Fact]
    public void A_folio_is_searched_from_four_characters()
    {
        var search = Asked(ApplicationSearch.ByFolio, "MF004");

        Assert.Equal(ApplicationSearch.ByFolio, search.By);
        Assert.Equal("MF004", search.Text);
    }

    [Fact]
    public void A_folio_of_three_characters_is_not_searched()
    {
        AssertOpeningList(Asked(ApplicationSearch.ByFolio, "MF0"));
    }

    [Fact]
    public void A_folio_is_not_searched_on_a_page_with_no_folio_search()
    {
        AssertOpeningList(Asked(ApplicationSearch.ByFolio, "MF004", folioToo: false));
        Assert.Equal(ApplicationSearch.ByNumber, Asked(ApplicationSearch.ByNumber, "0138", folioToo: false).By);
    }

    [Fact]
    public void The_opening_list_is_known_as_such()
    {
        var found = new ApplicationsFound([], MoreExist: false);

        Assert.True(new ApplicationsViewModel(found, WindowDays, Asked()).IsOpeningList);
        Assert.False(new ApplicationsViewModel(found, WindowDays, Asked(ApplicationSearch.ByDates, from: Today, to: Today)).IsOpeningList);
        Assert.False(new ApplicationsViewModel(found, WindowDays, Asked(ApplicationSearch.ByNumber, "0138")).IsOpeningList);
    }
}
