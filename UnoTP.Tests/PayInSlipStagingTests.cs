using UnoTP.Models;

namespace UnoTP.Tests;

/// <summary>The row a cheque application is put in the pay-in slip staging table with (Models/PayInSlipStaging.cs).</summary>
public class PayInSlipStagingTests
{
    private static readonly Dictionary<string, BankBranch> Branches = new()
    {
        ["TEST0001234"] = new BankBranch("TEST0001234", "Test Bank", "Shivajinagar", "411000001"),
    };

    private static Application ChequeApplication() => new()
    {
        AppNo = "DEVBB26F10001",
        Holder = new Holder("ABCPA1001A", "01-01-1980", "Asha Test", "", PanFiled: true),
        Payment = new PaymentDetails(
            new BankAccount(" test0001234 ", "000111222333"), Repayment: null, RepaymentSameAsPayment: true,
            new ChequeDetails("123456", "05-10-2026", "Pune Camp", "PN01")),
        Deposit = new DepositDetails(250000, 12, "M", AutoRenewal: false, RenewInstruction: "", NoTds: false, DeliveryType: ""),
    };

    [Fact]
    public void The_row_waits_in_the_queue_as_PEN()
    {
        var row = PayInSlipStagingRow.For(ChequeApplication(), Branches, "AG001");

        Assert.Equal("PEN", row.QueueStatus);
    }

    [Fact]
    public void The_row_names_the_application_the_applicant_and_the_agency()
    {
        var row = PayInSlipStagingRow.For(ChequeApplication(), Branches, "AG001");

        Assert.Equal("DEVBB26F10001", row.ApplicationNumber);
        Assert.Equal("Asha Test", row.ApplicantName);
        Assert.Equal("AG001", row.AgencyCode);
    }

    [Fact]
    public void The_applicant_is_named_as_NSDL_verified_them_when_it_did()
    {
        var app = ChequeApplication();
        app.Upload = new UploadState { Name = "ASHA VERIFIED TEST" };

        var row = PayInSlipStagingRow.For(app, Branches, "AG001");

        Assert.Equal("ASHA VERIFIED TEST", row.ApplicantName);
    }

    [Fact]
    public void The_row_carries_the_cheque_and_the_deposit_amount_as_digits()
    {
        var row = PayInSlipStagingRow.For(ChequeApplication(), Branches, "AG001");

        Assert.Equal("123456", row.ChequeNumber);
        Assert.Equal("05-10-2026", row.ChequeDate);
        Assert.Equal("250000", row.ChequeAmount);
    }

    [Fact]
    public void The_bank_drawn_on_is_the_branch_the_IFSC_names()
    {
        var row = PayInSlipStagingRow.For(ChequeApplication(), Branches, "AG001");

        Assert.Equal("Test Bank", row.DrawnBankName);
        Assert.Equal("Shivajinagar", row.DrawnBranchName);
        Assert.Equal("411000001", row.DrawnBankMicr);
        Assert.Equal("test0001234", row.DrawnBankIfsc);
    }

    [Fact]
    public void The_cheque_is_presented_at_the_Axis_CMS_location_picked()
    {
        var row = PayInSlipStagingRow.For(ChequeApplication(), Branches, "AG001");

        Assert.Equal("Axis Bank", row.CmsBankName);
        Assert.Equal("PN01", row.CmsLocationCode);
        Assert.Equal("Pune Camp", row.CmsLocationName);
    }

    [Fact]
    public void What_the_application_does_not_hold_is_left_empty()
    {
        var bare = new Application { AppNo = "DEVBB26F10002", Holder = new Holder("ABCPA1001A", "01-01-1980", "Asha Test", "", PanFiled: true) };

        var row = PayInSlipStagingRow.For(bare, new Dictionary<string, BankBranch>(), agencyCode: "");

        Assert.Equal("PEN", row.QueueStatus);
        Assert.Null(row.AgencyCode);
        Assert.Null(row.ChequeNumber);
        Assert.Null(row.ChequeDate);
        Assert.Null(row.ChequeAmount);
        Assert.Null(row.DrawnBankName);
        Assert.Null(row.DrawnBankIfsc);
        Assert.Null(row.CmsBankName);
        Assert.Null(row.CmsLocationCode);
    }
}
