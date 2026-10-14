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

    /// <summary>f_Key_GUID: the key the page opens the application by, the one on the link's query string (?key=).</summary>
    public string? Key { get; init; }

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
            Key = EmptyAsNull(link?.Key),
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

/// <summary>
/// The limits a send is held to before a row goes into the communication table
/// (t_FD_PGW_Comm_Hdr), which is what sends the SMS and the e-mail: a row written
/// there goes to the investor, so none is written on a slip of the hand or on a
/// press repeated. An SMS and an e-mail each count as one message.
/// </summary>
public static class CommLimits
{
    /// <summary>No message goes out for an application within this long of its last one.</summary>
    public const int SecondsBetweenSends = 120;

    /// <summary>The most messages that go out for one application in a day.</summary>
    public const int MostPerApplicationPerDay = 6;

    /// <summary>The most messages that go to one mobile number, or to one e-mail address, in a day, whatever the applications.</summary>
    public const int MostPerContactPerDay = 10;

    /// <summary>The day the two counts are taken over, in hours.</summary>
    public const int DayInHours = 24;
}

/// <summary>
/// Where a link goes: the investor's mobile number and e-mail address as the
/// application holds them. A message goes by a channel only when its contact is
/// usable - a mobile number of ten digits, an e-mail address with an @ and a dot
/// after it - and the unusable one is left out, as the FD system's own procedure
/// leaves out an empty one.
/// </summary>
public sealed record LinkContact(string Mobile, string Email)
{
    /// <summary>The mobile number when it is usable, else null: what the SMS row carries.</summary>
    public string? UsableMobile => Mobile.Trim().Length == 10 && Mobile.Trim().All(char.IsAsciiDigit) ? Mobile.Trim() : null;

    /// <summary>The e-mail address when it is usable, else null: what the e-mail row carries.</summary>
    public string? UsableEmail
    {
        get
        {
            var email = Email.Trim();
            var at = email.IndexOf('@');
            if (at < 1 || email.IndexOf('@', at + 1) >= 0) return null;
            if (email.IndexOf('.', at) < at + 2 || email.EndsWith('.')) return null;
            return email;
        }
    }

    /// <summary>Whether any message can go at all.</summary>
    public bool AnyUsable => UsableMobile is not null || UsableEmail is not null;
}

