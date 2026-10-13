namespace UnoTP.Data;

/// <summary>
/// The SQL for the error log, t_Unotp_Logs. Nothing but SQL is in this file.
/// </summary>
internal static class ErrorLogQueries
{
    /// <summary>
    /// One entry of the error log.
    /// </summary>
    public const string Insert = """
        INSERT INTO dbo.t_Unotp_Logs (f_App_Name, f_Environment, f_Level, f_Category, f_Event_Id, f_Message, f_Exception,
            f_Trace_Id, f_Request_Method, f_Request_Path, f_App_No, f_Client_Ip, f_Machine_Name, f_Logged_At, f_Created_By)
        VALUES (@AppName, @Environment, @Level, @Category, @EventId, @Message, @Exception,
            @TraceId, @RequestMethod, @RequestPath, @AppNo, @ClientIp, @MachineName, @LoggedAt, @CreatedBy)
        """;
}
