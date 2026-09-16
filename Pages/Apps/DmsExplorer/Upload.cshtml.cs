using Microsoft.AspNetCore.Mvc.RazorPages;
using UnoTp.Models;

namespace UnoTp.Pages.Apps.DmsExplorer;

/// <summary>
/// A holder type as the store codes it in a file name: Investor files as 01. A KYC
/// document always belongs to a holder; an FD or Open document can instead be filed
/// as not holder-specific (00), as the cheque and the application form are.
/// </summary>
public record DmsHolderOption(string Label, string Code, bool KycToo = true);

/// <summary>
/// One document the upload form can file. Label is the type the view shows ("Proof of
/// address · passport"); Token is the part of the file name that names it; HasRef and
/// HasExpiry decide whether the reference and expiry fields apply.
/// </summary>
public record DmsTypeOption(string Label, string Token, bool HasRef = false, bool HasExpiry = false);

public record DmsKycGroup(string DocType, List<(string SubType, DmsTypeOption Option)> SubTypes);

public class UploadModel : PageModel
{
    public const int MaxFileMb = 5;
    public const string Accept = ".pdf,.jpg,.jpeg,.png,.tif,.tiff";

    public static readonly List<DmsHolderOption> Holders = new()
    {
        new("Investor", "01"),
        new("Joint holder 1", "02"),
        new("Joint holder 2", "03"),
        new("Not holder-specific", "00", KycToo: false),
    };

    // The KYC types and sub-types the old upload screen offered, named as the view
    // names them so an upload lands against the same document type.
    public static readonly List<DmsKycGroup> KycTypes = new()
    {
        new("Identity proof", new()
        {
            ("PAN", new("Identity proof · PAN", "PAN", HasRef: true)),
        }),
        new("Proof of address", new()
        {
            ("Passport", new("Proof of address · passport", "Passport", HasRef: true, HasExpiry: true)),
            ("Aadhaar", new("Proof of address · Aadhaar", "AadharCard", HasRef: true)),
            ("Driving licence", new("Proof of address · driving licence", "DrivingLicence", HasRef: true, HasExpiry: true)),
            ("Voter ID", new("Proof of address · voter ID", "VoterId", HasRef: true)),
            ("Utility bill", new("Proof of address · utility bill", "UtilityBill")),
        }),
        new("Photograph", new()
        {
            ("Photograph", new("Photograph", "Photograph")),
        }),
    };

    public static readonly List<DmsTypeOption> FdTypes = new()
    {
        new("Application form · signed", "FDForm"),
        new("Payment instrument · cheque", "Cheque"),
        new("Tax declaration · Form 15G", "Form15G"),
        new("Tax declaration · Form 15H", "Form15H"),
    };

    public static readonly string[] FinYears = { "2026-27", "2025-26" };

    /// <summary>
    /// What is already filed, for the form to say when an upload will replace a
    /// document rather than add one: holder type and type map to the version in force.
    /// </summary>
    public object Filed
    {
        get
        {
            var f = MockData.DmsFiling;
            return new
            {
                f.ControlNo,
                f.ApplicationNo,
                Documents = f.Documents.Select(d => new
                {
                    Holder = d.HolderType,
                    d.Type,
                    Version = IndexModel.VersionsOf(d).Count,
                    d.File,
                }),
            };
        }
    }

    public void OnGet()
    {
    }
}
