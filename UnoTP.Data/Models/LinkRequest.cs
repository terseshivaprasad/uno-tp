namespace UnoTP.Models;

/// <summary>
/// One row of a link request table: the short link an application's investor is
/// sent. A purchase's is a row of t_FD_PGW_PaymentUrlRequest_dtl, a renewal's of
/// T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL; the columns written are the same in
/// both. Each property is one column, named in its note. What the application does
/// not hold is left empty.
/// </summary>
public sealed record LinkRequestRow
{
    /// <summary>
    /// Hours a link is open for once it is sent again. Regenerate makes no new link:
    /// the same short link goes to the investor again, open for this long from then.
    /// </summary>
    public const int RegeneratedForHours = 24;

    /// <summary>f_AppNo.</summary>
    public required string AppNo { get; init; }

    /// <summary>f_InvestorName: the first holder's name.</summary>
    public string? InvestorName { get; init; }

    /// <summary>f_InvestmentAmount: the deposit's amount, rupees.</summary>
    public long? InvestmentAmount { get; init; }

    /// <summary>f_Investor_Pan.</summary>
    public string? InvestorPan { get; init; }

    /// <summary>F_ApplicationDate: when the application was submitted.</summary>
    public DateTime? ApplicationDate { get; init; }

    /// <summary>f_ShotrURL: the short link. Empty when the link was not shortened, or none was built.</summary>
    public string? ShortUrl { get; init; }

    /// <summary>f_FullURL: the page the short link opens.</summary>
    public string? FullUrl { get; init; }

    /// <summary>f_Link_Validity: hours the link is open for from when it is sent. f_Link_Expiry_Date is worked out from it.</summary>
    public required int ValidityHours { get; init; }

    /// <summary>The row for a submitted application's link.</summary>
    /// <param name="link">The link as built and shortened; null when the app builds none.</param>
    public static LinkRequestRow For(Application app, PaymentLink? link, int validityHours)
    {
        // The investor takes the name NSDL verified, when they came with no folio:
        // the name their KYC row is written with.
        var investor = app.Holder.Name;
        if (app.Upload is { Name.Length: > 0 } upload) investor = upload.Name;

        return new LinkRequestRow
        {
            AppNo = app.AppNo,
            InvestorName = EmptyAsNull(investor),
            InvestmentAmount = app.Deposit?.Amount,
            InvestorPan = EmptyAsNull(app.Holder.Pan),
            ApplicationDate = app.Submitted?.At,
            ShortUrl = EmptyAsNull(link?.ShortUrl),
            FullUrl = EmptyAsNull(link?.Url),
            ValidityHours = validityHours,
        };
    }

    // Nothing to write leaves the column empty.
    private static string? EmptyAsNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value;
    }
}
