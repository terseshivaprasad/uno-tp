namespace UnoTP.Data;

/// <summary>
/// The SQL for Console Admin: t_Unotp_Console_Window and t_Unotp_Console_Notice.
/// Nothing but SQL is in this file.
/// </summary>
internal static class ConsoleQueries
{
    /// <summary>
    /// A window's columns, named after the properties of the window's row.
    /// </summary>
    public const string WindowColumns = """
        f_Window_Id AS Id, f_Features AS Features, f_From AS [From],
        CASE WHEN f_Ended_On IS NOT NULL AND f_Ended_On < f_To THEN f_Ended_On ELSE f_To END AS [To],
        f_Notice AS Notice, f_Set_By AS SetBy, f_Set_On AS SetOn
        """;

    /// <summary>
    /// A notice's columns, named after the properties of a notice.
    /// </summary>
    public const string NoticeColumns = """
        f_Notice_Id AS Id, f_Kind AS Kind, f_Title AS Title, f_At AS At, f_Detail AS Detail, f_Set_By AS SetBy, f_Set_On AS SetOn
        """;

    /// <summary>
    /// The windows and the notices still worth showing: those in use and not older than keptDays days.
    /// </summary>
    public static string Schedule(int keptDays) =>
        $"""
        SELECT {WindowColumns} FROM dbo.t_Unotp_Console_Window
        WHERE f_Active = 1 AND (f_Ended_On IS NULL OR f_Ended_On > f_From)
          AND f_To > DATEADD(DAY, -{keptDays}, SYSDATETIME())
        ORDER BY f_From;
        SELECT {NoticeColumns} FROM dbo.t_Unotp_Console_Notice
        WHERE f_Active = 1 AND f_Removed_On IS NULL AND f_At > DATEADD(DAY, -{keptDays}, SYSDATETIME())
        ORDER BY f_At;
        """;

    /// <summary>
    /// A window added, under a number of its own. Gives the row back.
    /// </summary>
    public const string AddWindow = $"""
        DECLARE @Seq INT = NEXT VALUE FOR dbo.s_Console_Id;
        INSERT dbo.t_Unotp_Console_Window (f_Window_Id, f_Features, f_From, f_To, f_Notice, f_Set_By)
        OUTPUT inserted.f_Window_Id AS Id, inserted.f_Features AS Features, inserted.f_From AS [From], inserted.f_To AS [To],
            inserted.f_Notice AS Notice, inserted.f_Set_By AS SetBy, inserted.f_Set_On AS SetOn
        VALUES ('W-' + FORMAT(@From, 'ddMM') + '-' + RIGHT('00' + CAST(@Seq AS VARCHAR(10)), 3), @Features, @From, @To, @Notice, @By)
        """;

    /// <summary>
    /// A notice added, under a number of its own. Gives the row back.
    /// </summary>
    public const string AddNotice = """
        DECLARE @Seq INT = NEXT VALUE FOR dbo.s_Console_Id;
        INSERT dbo.t_Unotp_Console_Notice (f_Notice_Id, f_Kind, f_Title, f_At, f_Detail, f_Set_By)
        OUTPUT inserted.f_Notice_Id AS Id, inserted.f_Kind AS Kind, inserted.f_Title AS Title, inserted.f_At AS At,
            inserted.f_Detail AS Detail, inserted.f_Set_By AS SetBy, inserted.f_Set_On AS SetOn
        VALUES ('N-' + FORMAT(@At, 'ddMM') + '-' + RIGHT('00' + CAST(@Seq AS VARCHAR(10)), 3), @Kind, @Title, @At, @Detail, @By)
        """;

    /// <summary>
    /// A window ended early.
    /// </summary>
    public const string EndWindow = """
        UPDATE dbo.t_Unotp_Console_Window SET f_Ended_On = SYSDATETIME(), f_Ended_By = @By
        WHERE f_Window_Id = @Id AND f_Active = 1 AND f_Ended_On IS NULL AND f_To > SYSDATETIME()
        """;

    /// <summary>
    /// A notice taken off.
    /// </summary>
    public const string RemoveNotice = """
        UPDATE dbo.t_Unotp_Console_Notice SET f_Removed_On = SYSDATETIME(), f_Removed_By = @By
        WHERE f_Notice_Id = @Id AND f_Active = 1 AND f_Removed_On IS NULL
        """;
}
