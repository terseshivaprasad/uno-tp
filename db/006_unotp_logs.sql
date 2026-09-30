/* =============================================================================
   Uno TP - 006: the error log.

   t_Unotp_Logs  every error and critical error Uno TP writes (ILogger at Error and
                 above), one row each, written in the background by UnoTP.Data's
                 SqlErrorLog so a request never waits on it. c_App_Name names the
                 application, so rows read the same wherever they are gathered.

     c_App_Name        the application: UnoTP
     c_Environment     Production, Staging, Development ...
     c_Level           Error or Critical
     c_Category        where it was logged (the class)
     c_Message         the message, as formatted
     c_Exception       the exception and its stack, when there was one
     c_Trace_Id        the request's trace id, as the error page shows it
     c_Request_*       the request it happened in; the path carries no PAN or name,
                       and the query string is not kept
     c_App_No          the application the request was about, when there was one
     c_Created_By      the partner signed in, or 'system' outside a request

   Append-only: a row is never updated by the app. c_Updated_By / d_Updated_On are
   for whoever marks a row looked into; f_Active = 0 takes it out of reports.
   Re-runnable. No foreign keys.
   ============================================================================= */
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.t_Unotp_Logs', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Logs
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_Name           VARCHAR(50)    NOT NULL,
    c_Environment        VARCHAR(30)    NOT NULL CONSTRAINT DF_Unotp_Logs_Env DEFAULT (''),
    c_Level              VARCHAR(12)    NOT NULL,
    c_Category           VARCHAR(200)   NOT NULL CONSTRAINT DF_Unotp_Logs_Category DEFAULT (''),
    n_Event_Id           INT            NOT NULL CONSTRAINT DF_Unotp_Logs_Event DEFAULT (0),
    c_Message            NVARCHAR(4000) NOT NULL,
    c_Exception          NVARCHAR(MAX)  NULL,
    c_Trace_Id           VARCHAR(64)    NULL,
    c_Request_Method     VARCHAR(10)    NULL,
    c_Request_Path       NVARCHAR(400)  NULL,
    c_App_No             VARCHAR(20)    NULL,
    c_Client_Ip          VARCHAR(45)    NULL,
    c_Machine_Name       VARCHAR(100)   NOT NULL CONSTRAINT DF_Unotp_Logs_Machine DEFAULT (''),
    d_Logged_At          DATETIME2(3)   NOT NULL,   -- when it happened, on the app's clock

    c_Created_By         VARCHAR(50)    NOT NULL CONSTRAINT DF_Unotp_Logs_Created_By DEFAULT ('system'),
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Unotp_Logs_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(50)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Unotp_Logs_Active DEFAULT (1),
    CONSTRAINT PK_Unotp_Logs PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Unotp_Logs_Level CHECK (c_Level IN ('Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Unotp_Logs_App_Created')
    CREATE INDEX IX_Unotp_Logs_App_Created ON dbo.t_Unotp_Logs (c_App_Name, d_Created_On DESC) INCLUDE (c_Level, f_Active);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Unotp_Logs_Trace')
    CREATE INDEX IX_Unotp_Logs_Trace ON dbo.t_Unotp_Logs (c_Trace_Id);
GO
