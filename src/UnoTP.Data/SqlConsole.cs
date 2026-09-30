using Dapper;
using UnoTP.Backend;

namespace UnoTP.Data;

/// <summary>
/// What the console's administrator has on the books: t_Unotp_Console_Window and
/// t_Unotp_Console_Notice. Nothing is deleted: a window ended early, or a notice taken
/// down, is marked so and stays on record. Every partner sees the same schedule.
/// </summary>
public sealed class SqlConsole(Db db, SqlPartners partners) : IConsoleApi
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
        c_Window_Id AS Id, c_Features AS Features, d_From AS [From],
        CASE WHEN d_Ended_On IS NOT NULL AND d_Ended_On < d_To THEN d_Ended_On ELSE d_To END AS [To],
        c_Notice AS Notice, c_Set_By AS SetBy, d_Set_On AS SetOn
        """;

    private const string NoticeColumns = """
        c_Notice_Id AS Id, c_Kind AS Kind, c_Title AS Title, d_At AS At, c_Detail AS Detail, c_Set_By AS SetBy, d_Set_On AS SetOn
        """;

    public async Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        using var read = await connection.QueryMultipleAsync($"""
            SELECT {WindowColumns} FROM dbo.t_Unotp_Console_Window
            WHERE f_Active = 1 AND (d_Ended_On IS NULL OR d_Ended_On > d_From)
              AND d_To > DATEADD(DAY, -{KeptDays}, SYSDATETIME())
            ORDER BY d_From;
            SELECT {NoticeColumns} FROM dbo.t_Unotp_Console_Notice
            WHERE f_Active = 1 AND d_Removed_On IS NULL AND d_At > DATEADD(DAY, -{KeptDays}, SYSDATETIME())
            ORDER BY d_At;
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
            INSERT dbo.t_Unotp_Console_Window (c_Window_Id, c_Features, d_From, d_To, c_Notice, c_Set_By)
            OUTPUT inserted.c_Window_Id AS Id, inserted.c_Features AS Features, inserted.d_From AS [From], inserted.d_To AS [To],
                inserted.c_Notice AS Notice, inserted.c_Set_By AS SetBy, inserted.d_Set_On AS SetOn
            VALUES ('W-' + FORMAT(@From, 'ddMM') + '-' + RIGHT('00' + CAST(@Seq AS VARCHAR(10)), 3), @Features, @From, @To, @Notice, @By)
            """, new { Features = string.Join(',', window.Features), window.From, window.To, window.Notice, By = by })).Record();
    }

    public async Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleAsync<AnnouncementRecord>("""
            DECLARE @Seq INT = NEXT VALUE FOR dbo.s_Console_Id;
            INSERT dbo.t_Unotp_Console_Notice (c_Notice_Id, c_Kind, c_Title, d_At, c_Detail, c_Set_By)
            OUTPUT inserted.c_Notice_Id AS Id, inserted.c_Kind AS Kind, inserted.c_Title AS Title, inserted.d_At AS At,
                inserted.c_Detail AS Detail, inserted.c_Set_By AS SetBy, inserted.d_Set_On AS SetOn
            VALUES ('N-' + FORMAT(@At, 'ddMM') + '-' + RIGHT('00' + CAST(@Seq AS VARCHAR(10)), 3), @Kind, @Title, @At, @Detail, @By)
            """, new { announcement.Kind, announcement.Title, announcement.At, announcement.Detail, By = by });
    }

    // Ends a window that is on, or cancels one still to come; one already over is not touched.
    public async Task<bool> EndWindowAsync(string id, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Console_Window SET d_Ended_On = SYSDATETIME(), c_Ended_By = @By
            WHERE c_Window_Id = @Id AND f_Active = 1 AND d_Ended_On IS NULL AND d_To > SYSDATETIME()
            """, new { Id = id, By = by }) > 0;
    }

    public async Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default)
    {
        var by = (await partners.MeAsync(ct)).Name;
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteAsync("""
            UPDATE dbo.t_Unotp_Console_Notice SET d_Removed_On = SYSDATETIME(), c_Removed_By = @By
            WHERE c_Notice_Id = @Id AND f_Active = 1 AND d_Removed_On IS NULL
            """, new { Id = id, By = by }) > 0;
    }
}
