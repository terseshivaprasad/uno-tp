namespace UnoTP.Data;

/// <summary>
/// The SQL of the Dashboard's own work list. Its other counts come from the pages
/// they lead to (ShortUrlQueries, PayInSlipQueries). Nothing but SQL is in this file.
/// </summary>
internal static class DashboardQueries
{
    /// <summary>
    /// The partner's open applications - not booked, not cancelled by the app nor by the FD system
    /// (t_FD_BT_Cancelled_Application_Dtl) - raised from @From up
    /// to, and not on, @Until: the days that leave an unpaid application close to
    /// cancelling. An application is raised the day it is submitted; one not yet
    /// submitted, the day it was opened. Only the number and the day are read.
    /// </summary>
    public const string CloseToCancel = """
        SELECT m.f_App_No AS AppNo, COALESCE(m.f_Submitted_On, m.f_Created_On) AS Applied
        FROM dbo.t_Unotp_Application_Mst m
        WHERE m.f_Partner_Id = @Partner AND m.f_Active = 1
          AND m.f_Booked_On IS NULL AND m.f_Cancelled_On IS NULL
          AND NOT EXISTS (SELECT 1 FROM MMFSL_APP_FD_BTP.dbo.t_FD_BT_Cancelled_Application_Dtl c WITH (NOLOCK)
                          WHERE c.f_Appl_No = m.f_App_No AND c.f_Active = 1)
          AND COALESCE(m.f_Submitted_On, m.f_Created_On) >= @From
            AND COALESCE(m.f_Submitted_On, m.f_Created_On) < @Until
        ORDER BY Applied
        """;
}
