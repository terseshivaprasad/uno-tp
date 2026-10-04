using UnoTP.Models;

namespace UnoTP.Tests;

public class FolioCheckTests
{
    private const string Pan = "ABCPK1234F";
    private const string Dob = "14-03-1980";
    private static readonly DateOnly Today = new(2026, 10, 4);

    private static FolioDeposit On(string folio, string dob = Dob, string pan = Pan) => new(folio, pan, dob);

    [Fact]
    public void A_pan_with_no_deposit_has_no_folio_and_is_a_new_investor()
    {
        var answer = FolioCheck.ByPan(Pan, Dob, [], Today);

        Assert.Equal("", answer.Folio);
        Assert.Null(answer.Problem);
    }

    [Fact]
    public void A_pan_and_date_of_birth_that_match_give_the_folio()
    {
        var answer = FolioCheck.ByPan(Pan, Dob, [On("MF001"), On("MF001")], Today);

        Assert.Equal("MF001", answer.Folio);
        Assert.Null(answer.Problem);
    }

    [Fact]
    public void A_pan_that_is_not_a_persons_is_refused()
    {
        var answer = FolioCheck.ByPan("ABCCK1234F", Dob, [On("MF001", pan: "ABCCK1234F")], Today);

        Assert.Equal(FolioCheck.NonIndividual, answer.Problem);
        Assert.False(answer.AboutDob);
    }

    [Fact]
    public void A_pan_that_is_not_a_persons_is_refused_with_no_folio_too()
    {
        var answer = FolioCheck.ByPan("ABCCK1234F", Dob, [], Today);

        Assert.Equal("", answer.Folio);
        Assert.Equal(FolioCheck.NonIndividual, answer.Problem);
    }

    [Fact]
    public void A_folio_with_no_date_of_birth_is_refused_on_the_date_of_birth()
    {
        var answer = FolioCheck.ByPan(Pan, Dob, [On("MF001", dob: "")], Today);

        Assert.Equal(FolioCheck.NoDob, answer.Problem);
        Assert.True(answer.AboutDob);
    }

    [Fact]
    public void Another_date_of_birth_than_the_folios_is_refused_on_the_date_of_birth()
    {
        var answer = FolioCheck.ByPan(Pan, "15-03-1980", [On("MF001")], Today);

        Assert.Equal(FolioCheck.DobMismatch, answer.Problem);
        Assert.True(answer.AboutDob);
    }

    [Fact]
    public void Two_folios_against_one_pan_are_refused_and_both_are_named()
    {
        var answer = FolioCheck.ByPan(Pan, Dob, [On("MF001"), On("MF002")], Today);

        Assert.Equal(FolioCheck.ManyFolios, answer.Problem);
        Assert.Equal(["MF001", "MF002"], answer.Folios);
    }

    [Fact]
    public void A_folio_number_with_no_deposit_is_not_found()
    {
        Assert.Equal("", FolioCheck.ByFolio([], [], Today).Folio);
    }

    [Fact]
    public void A_folio_number_is_refused_for_a_pan_that_is_not_a_persons_or_no_date_of_birth()
    {
        Assert.Equal(FolioCheck.NonIndividual, FolioCheck.ByFolio([On("MF001", pan: "ABCCK1234F")], [], Today).Problem);
        Assert.Equal(FolioCheck.NoDob, FolioCheck.ByFolio([On("MF001", dob: "")], [On("MF001", dob: "")], Today).Problem);
    }

    [Fact]
    public void A_folio_number_with_a_persons_pan_and_a_date_of_birth_is_found()
    {
        var answer = FolioCheck.ByFolio([On("MF001")], [On("MF001")], Today);

        Assert.Equal("MF001", answer.Folio);
        Assert.Null(answer.Problem);
    }

    [Fact]
    public void A_folio_number_is_refused_when_its_pan_is_on_another_folio_too()
    {
        var answer = FolioCheck.ByFolio([On("MF001")], [On("MF001"), On("MF002")], Today);

        Assert.Equal(FolioCheck.ManyFolios, answer.Problem);
        Assert.Equal(["MF001", "MF002"], answer.Folios);
    }
}
