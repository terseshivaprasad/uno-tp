using UnoTP.Data;
using UnoTP.ViewModels;

namespace UnoTP.Tests;

/// <summary>
/// The cheque date on Bank Details &amp; Payment: today or within BankForm.ChequeDateWithinDays
/// before it, never after today.
/// </summary>
public class ChequeDateTests
{
    private static string? Said(DateTime date)
    {
        var form = new BankForm { Cheque = { Number = "123456", Date = date.ToString("dd-MM-yyyy"), CmsLocation = "Pune", CmsCode = "P1" } };
        return form.Problems(byCheque: true, paymentBranch: null, repaymentBranch: null, cmsLocationOnMaster: true).GetValueOrDefault("Cheque.Date");
    }

    [Fact]
    public void Today_is_taken()
    {
        Assert.Null(Said(DateTime.Today));
    }

    [Fact]
    public void Tomorrow_is_after_today()
    {
        Assert.Equal(Messages.BankDetails.ChequeDateInFuture, Said(DateTime.Today.AddDays(1)));
    }

    [Fact]
    public void The_last_day_of_the_window_is_taken()
    {
        Assert.Null(Said(DateTime.Today.AddDays(-BankForm.ChequeDateWithinDays)));
    }

    [Fact]
    public void A_day_before_the_window_is_too_old()
    {
        Assert.Equal(Messages.BankDetails.ChequeDateTooOld(BankForm.ChequeDateWithinDays), Said(DateTime.Today.AddDays(-BankForm.ChequeDateWithinDays - 1)));
    }

    [Fact]
    public void No_date_is_still_asked_for()
    {
        var form = new BankForm { Cheque = { Number = "123456" } };
        Assert.Equal(Messages.BankDetails.ChequeDateRequired, form.Problems(true, null, null, true)["Cheque.Date"]);
    }
}
