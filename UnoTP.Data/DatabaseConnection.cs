using System.Globalization;
using Microsoft.Data.SqlClient;

namespace UnoTP.Data;

/// <summary>
/// The one connection, ConnectionStrings:UnoTP, which must be set.
/// A table in another database on the same server is named in full in its query:
/// OtherDb.dbo.Table.
/// </summary>
public sealed class Db(IConfiguration config)
{
    private readonly string main = config.GetConnectionString("UnoTP") is { Length: > 0 } cs
        ? cs
        : throw new InvalidOperationException("ConnectionStrings:UnoTP is not set.");

    /// <summary>An open connection to the database.</summary>
    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(main);
        await connection.OpenAsync(ct);
        return connection;
    }
}

/// <summary>Where a detail row stands: saved on a step, or part of the application as submitted.</summary>
public static class RowStatus
{
    public const string Pending = "PEN";

    public const string Approved = "APR";

    /// <summary>
    /// Submitted with the investor's KYC fetched from CKYC: the FD system's rows are
    /// held as this, not as approved. The app's own tables still say APR, for submitted.
    /// </summary>
    public const string PendingCkyc = "PEN_E";
}

/// <summary>How an application is signed, as Upload Documents and f_ApplicationDeclarationType name it.</summary>
public static class ApplicationType
{
    public const string Digital = "DIGITAL";

    public const string Physical = "PHYSICAL";
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
