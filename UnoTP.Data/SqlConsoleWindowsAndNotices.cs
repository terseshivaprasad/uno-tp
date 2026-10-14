using Dapper;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// What the console's administrator has on the books: t_Unotp_Console_Window and
/// t_Unotp_Console_Notice. Nothing is deleted: a window ended early, or a notice taken
/// down, is marked so and stays on record. Every partner sees the same schedule.
/// </summary>
public sealed class SqlConsole(Db db, IPartnerApi partners) : IConsoleApi
{
    // What is still worth sending is anything that ended within 30 days: the number is in
    // ConsoleAdminQueries.Schedule. The pages only show what has not ended; the rest is on
    // record in the tables.

    private sealed class WindowRow
    {
        public string Id { get; set; } = "";
        public string Features { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Notice { get; set; } = "";
        public string SetBy { get; set; } = "";
        public DateTime SetOn { get; set; }

        public WindowRecord Record() =>
            new(Id, Features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), From, To, Notice, SetBy, SetOn);
    }

    public async Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        using var read = await connection.QueryMultipleAsync(ConsoleAdminQueries.Schedule);
        var windows = (await read.ReadAsync<WindowRow>()).Select(w => w.Record()).ToList();
        var notices = (await read.ReadAsync<AnnouncementRecord>()).ToList();
        return new ConsoleSchedule(windows, notices);
    }

    public async Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QuerySingleAsync<WindowRow>(ConsoleAdminQueries.AddWindow, new { Features = string.Join(',', window.Features), window.From, window.To, window.Notice, By = by })).Record();
    }

    public async Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleAsync<AnnouncementRecord>(ConsoleAdminQueries.AddNotice, new { announcement.Kind, announcement.Title, announcement.At, announcement.Detail, By = by });
    }

    // Ends a window that is on, or cancels one still to come; one already over is not touched.
    public async Task<bool> EndWindowAsync(string id, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync(ConsoleAdminQueries.EndWindow, new { Id = id, By = by }) > 0;
    }

    public async Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync(ConsoleAdminQueries.RemoveNotice, new { Id = id, By = by }) > 0;
    }
}
