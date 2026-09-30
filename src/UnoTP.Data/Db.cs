using System.Globalization;
using Microsoft.Data.SqlClient;

namespace UnoTP.Data;

/// <summary>
/// The databases. ConnectionStrings:UnoTP holds the applications and everything
/// else, unless an area is given a database of its own, as the old portal has them:
///   UnoTP_Masters   the brokers, staff, IFSC and PIN code masters, the rate card, and
///                   the config, feature and reference lists (db/002's lists, db/004)
///   UnoTP_Folios    the investor folios (db/004)
///   UnoTP_Links     the payment links behind Short URL (db/002)
///   UnoTP_Errors    the error log (db/006)
/// An area's connection string left blank means its tables are in the main database.
/// No query joins across areas, so each may be anywhere.
/// </summary>
public sealed class Db(IConfiguration config)
{
    public const string Main = "UnoTP";
    public const string Masters = "UnoTP_Masters";
    public const string Folios = "UnoTP_Folios";
    public const string Links = "UnoTP_Links";
    public const string Errors = "UnoTP_Errors";

    private readonly string mainConnectionString = config.GetConnectionString(Main) is { Length: > 0 } cs
        ? cs
        : throw new InvalidOperationException("ConnectionStrings:UnoTP is not set.");

    /// <summary>The main database: the applications and their parts, partners, sessions, slips, the console.</summary>
    public Task<SqlConnection> OpenAsync(CancellationToken ct) => OpenAsync(Main, ct);

    /// <summary>An area's database, or the main one where the area has none of its own.</summary>
    public async Task<SqlConnection> OpenAsync(string area, CancellationToken ct)
    {
        var connectionString = config.GetConnectionString(area);
        if (string.IsNullOrWhiteSpace(connectionString)) connectionString = mainConnectionString;
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}

/// <summary>Where a detail row stands: saved on a step, or part of the application as submitted.</summary>
public static class RowStatus
{
    public const string Pending = "PEN";

    public const string Approved = "APR";
}

/// <summary>
/// Dates as the web app writes them - dd-MM-yyyy - and as the tables hold them.
/// A date that does not parse is held as NULL, and read back as empty.
/// </summary>
internal static class Dates
{
    private const string Format = "dd-MM-yyyy";

    /// <summary>A dd-MM-yyyy date as the database stores it, or null when it is not one.</summary>
    public static DateTime? ParseDdMmYyyy(string? value) =>
        DateTime.TryParseExact((value ?? "").Trim(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    public static string FromDb(DateTime? value) => value?.ToString(Format, CultureInfo.InvariantCulture) ?? "";
}
