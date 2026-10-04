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
    // What is still worth sending: anything that ended within this many days. The
    // pages only show what has not ended; the rest is on record in the tables.
    private const int KeptDays = 30;

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

    private const string WindowColumns = """
        f_Window_Id AS Id, f_Features AS Features, f_From AS [From],
        CASE WHEN f_Ended_On IS NOT NULL AND f_Ended_On < f_To THEN f_Ended_On ELSE f_To END AS [To],
        f_Notice AS Notice, f_Set_By AS SetBy, f_Set_On AS SetOn
        """;

    private const string NoticeColumns = """
        f_Notice_Id AS Id, f_Kind AS Kind, f_Title AS Title, f_At AS At, f_Detail AS Detail, f_Set_By AS SetBy, f_Set_On AS SetOn
        """;

    public async Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        using var read = await connection.QueryMultipleAsync($"""
            SELECT {WindowColumns} FROM dbo.t_Unotp_Console_Window
            WHERE f_Active = 1 AND (f_Ended_On IS NULL OR f_Ended_On > f_From)
              AND f_To > DATEADD(DAY, -{KeptDays}, SYSDATETIME())
            ORDER BY f_From;
            SELECT {NoticeColumns} FROM dbo.t_Unotp_Console_Notice
            WHERE f_Active = 1 AND f_Removed_On IS NULL AND f_At > DATEADD(DAY, -{KeptDays}, SYSDATETIME())
            ORDER BY f_At;
            """);
        var windows = (await read.ReadAsync<WindowRow>()).Select(w => w.Record()).ToList();
        var notices = (await read.ReadAsync<AnnouncementRecord>()).ToList();
        return new ConsoleSchedule(windows, notices);
    }

    public async Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QuerySingleAsync<WindowRow>($"""
            DECLARE @Seq INT = NEXT VALUE FOR dbo.s_Console_Id;
            INSERT dbo.t_Unotp_Console_Window (f_Window_Id, f_Features, f_From, f_To, f_Notice, f_Set_By)
            OUTPUT inserted.f_Window_Id AS Id, inserted.f_Features AS Features, inserted.f_From AS [From], inserted.f_To AS [To],
                inserted.f_Notice AS Notice, inserted.f_Set_By AS SetBy, inserted.f_Set_On AS SetOn
            VALUES ('W-' + FORMAT(@From, 'ddMM') + '-' + RIGHT('00' + CAST(@Seq AS VARCHAR(10)), 3), @Features, @From, @To, @Notice, @By)
            """, new { Features = string.Join(',', window.Features), window.From, window.To, window.Notice, By = by })).Record();
    }

    public async Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleAsync<AnnouncementRecord>("""
            DECLARE @Seq INT = NEXT VALUE FOR dbo.s_Console_Id;
            INSERT dbo.t_Unotp_Console_Notice (f_Notice_Id, f_Kind, f_Title, f_At, f_Detail, f_Set_By)
            OUTPUT inserted.f_Notice_Id AS Id, inserted.f_Kind AS Kind, inserted.f_Title AS Title, inserted.f_At AS At,
                inserted.f_Detail AS Detail, inserted.f_Set_By AS SetBy, inserted.f_Set_On AS SetOn
            VALUES ('N-' + FORMAT(@At, 'ddMM') + '-' + RIGHT('00' + CAST(@Seq AS VARCHAR(10)), 3), @Kind, @Title, @At, @Detail, @By)
            """, new { announcement.Kind, announcement.Title, announcement.At, announcement.Detail, By = by });
    }

    // Ends a window that is on, or cancels one still to come; one already over is not touched.
    public async Task<bool> EndWindowAsync(string id, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Console_Window SET f_Ended_On = SYSDATETIME(), f_Ended_By = @By
            WHERE f_Window_Id = @Id AND f_Active = 1 AND f_Ended_On IS NULL AND f_To > SYSDATETIME()
            """, new { Id = id, By = by }) > 0;
    }

    public async Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Console_Notice SET f_Removed_On = SYSDATETIME(), f_Removed_By = @By
            WHERE f_Notice_Id = @Id AND f_Active = 1 AND f_Removed_On IS NULL
            """, new { Id = id, By = by }) > 0;
    }
}
