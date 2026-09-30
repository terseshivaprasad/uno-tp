using System.Globalization;
using Microsoft.Data.SqlClient;

namespace UnoTP.Data;

/// <summary>The UnoTP database, at ConnectionStrings:UnoTP. Tables: db/001_unotp_tables.sql.</summary>
public sealed class Db(IConfiguration config)
{
    private readonly string connectionString = config.GetConnectionString("UnoTP") is { Length: > 0 } cs
        ? cs
        : throw new InvalidOperationException("ConnectionStrings:UnoTP is not set.");

    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
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

    public static DateTime? ToDb(string? value) =>
        DateTime.TryParseExact((value ?? "").Trim(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    public static string FromDb(DateTime? value) => value?.ToString(Format, CultureInfo.InvariantCulture) ?? "";
}
