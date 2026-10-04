/* =============================================================================
   Uno TP - the tables that are new with this app (SQL Server 2016 or later):
   the application header and its working state, the settings, features and lists,
   the console's windows and notices, and the error log.

       sqlcmd -d UnoTP -i db/create_new_tables.sql

   The numbered scripts beside it put the settings and lists in: run them after
   this one, in order.

   A table is created, with its indexes, only when it is not there yet. A table
   already there is not touched at all: no column, index or row of it changes,
   whatever its columns are. Schema only: no rows are put in. Safe to run again.

   Column prefixes: c_ text, n_ number, d_ date, f_ flag, j_ JSON. Every table has
   f_Active: 0 takes a row out of use without deleting it. No table has a foreign
   key or a CHECK constraint: the app keeps those rules itself.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.s_App_No', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_App_No AS BIGINT START WITH 10001 INCREMENT BY 1 NO CACHE;
GO

/* ----- t_Unotp_Application_Mst ------------------------------------------------------
   One row per application: its number, whose it is, who it was opened for, and
   where it stands. This is the one row that changes - it carries the version every
   save is checked against, so two saves of one application cannot cross. What
   was entered on the application is in the detail tables, never here.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Application_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Application_Mst
(
    n_App_Id             BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    c_Partner_Id         VARCHAR(20)    NOT NULL,   -- X-Partner-Id: the only partner who sees it
    c_Status             CHAR(3)        NOT NULL CONSTRAINT DF_Application_Mst_Status DEFAULT ('PEN'),
    n_Version            INT            NOT NULL CONSTRAINT DF_Application_Mst_Version DEFAULT (1),

    -- The version each section was last saved at: its current rows. NULL until saved.
    n_Upload_Ver         INT            NULL,
    n_Details_Ver        INT            NULL,
    n_Payment_Ver        INT            NULL,
    n_Deposit_Ver        INT            NULL,

    -- The investor, as Investor Identification found them when the application was opened.
    c_Pan                VARCHAR(10)    NOT NULL,
    d_Dob                DATE           NULL,
    c_Name               NVARCHAR(150)  NOT NULL,
    c_Folio              VARCHAR(20)    NOT NULL CONSTRAINT DF_Application_Mst_Folio DEFAULT (''),   -- '' for a new investor
    c_Gender             VARCHAR(20)    NOT NULL CONSTRAINT DF_Application_Mst_Gender DEFAULT (''),
    c_Address            NVARCHAR(500)  NOT NULL CONSTRAINT DF_Application_Mst_Address DEFAULT (''),
    c_Data_Source        VARCHAR(50)    NULL,       -- the folio's source, for an investor on one: written to f_Data_Source
    f_Pan_Filed          BIT            NOT NULL CONSTRAINT DF_Application_Mst_Pan_Filed DEFAULT (0),
    f_Rec_Pan            BIT            NULL,       -- what the folio already holds;
    f_Rec_Photo          BIT            NULL,       -- NULL for an investor with no folio
    f_Rec_Poa            BIT            NULL,

    -- The deposit a renewal renews; NULL for a new deposit.
    c_Renew_Dep_No       VARCHAR(20)    NULL,
    n_Renew_Amount       BIGINT         NULL,
    d_Renew_Matures_On   DATE           NULL,
    n_Renew_Rate         DECIMAL(5,2)   NULL,
    n_Renew_Tenure       INT            NULL,
    c_Renew_Payout       VARCHAR(20)    NULL,
    n_Renew_Principal    BIGINT         NULL,       -- the deposit's own amount: renewed when the principal only is

    -- The submission, once submitted.
    d_Submitted_On       DATETIME2(3)   NULL,
    -- The quote locked on submit, beside the rate on the deposit's 'APR' row.
    n_Quote_Interest_Each   DECIMAL(18,2) NULL,  -- interest each payout; 0 for a cumulative deposit
    n_Quote_Maturity_Amount DECIMAL(18,2) NULL,
    d_Quote_Matures_On      DATE          NULL,
    d_Quote_Rate_As_On      DATE          NULL,  -- the card the rate was read off
    c_Sub_Status         VARCHAR(30)    NULL,       -- payment-pending, then the backend's own
    c_Link_Sent_To       VARCHAR(20)    NULL,       -- masked
    c_Link_Emailed_To    VARCHAR(150)   NULL,       -- masked
    d_Link_Valid_Until   DATETIME2(3)   NULL,
    n_Resends_Left       INT            NULL,
    c_Short_Url          VARCHAR(300)   NULL,

    -- After submit: set by the investor's acceptance, the payment feed, the FD system and Operations.
    d_Accepted_On        DATETIME2(3)   NULL,
    d_Paid_On            DATETIME2(3)   NULL,
    d_Booked_On          DATETIME2(3)   NULL,
    c_Fdr_No             VARCHAR(20)    NULL,
    d_Cancelled_On       DATETIME2(3)   NULL,
    d_Penny_Drop_On      DATETIME2(3)   NULL,
    c_Penny_Drop_Status  VARCHAR(10)    NOT NULL CONSTRAINT DF_Application_Mst_Penny DEFAULT (''),
    d_Kyc_Verified_On    DATETIME2(3)   NULL,
    c_Kyc_Status         VARCHAR(10)    NOT NULL CONSTRAINT DF_Application_Mst_Kyc DEFAULT (''),

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Application_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,

    f_Active             BIT            NOT NULL CONSTRAINT DF_Application_Mst_Active DEFAULT (1),
    CONSTRAINT PK_Application_Mst PRIMARY KEY CLUSTERED (n_App_Id),
    CONSTRAINT UQ_Application_Mst_App_No UNIQUE (c_App_No)
);
CREATE INDEX IX_Application_Mst_Partner ON dbo.t_Unotp_Application_Mst (c_Partner_Id, c_Status) INCLUDE (d_Created_On, d_Updated_On);
END
GO

/* ----- t_Unotp_Upload_State ---------------------------------------------------------
   Upload Documents as a whole, as the web app saved it: the choices, every
   check's reading and the attempt log, as JSON. It holds no file and no Aadhaar
   number. t_FD_BT_KYC_document carries its documents a row each.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Upload_State', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Upload_State
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,
    j_Upload             NVARCHAR(MAX)  NOT NULL,
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Upload_State_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Upload_State_Active DEFAULT (1),
    CONSTRAINT PK_Upload_State PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Upload_State_App ON dbo.t_Unotp_Upload_State (c_App_No, n_App_Version);
END
GO

/* ----- t_Unotp_Page_State -----------------------------------------------------------
   A wizard page's working state - typed but not yet saved, a joint holder still
   being searched for - kept so a page opens where it was left. Scratch, not a
   record: it is overwritten in place and moves no version.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Page_State', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Page_State
(
    c_App_No             VARCHAR(20)    NOT NULL,
    c_Page               VARCHAR(30)    NOT NULL,
    j_State              NVARCHAR(MAX)  NOT NULL,
    d_Updated_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Page_State_Updated DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Page_State_Active DEFAULT (1),
    CONSTRAINT PK_Page_State PRIMARY KEY CLUSTERED (c_App_No, c_Page)
);
END
GO

/* ----- t_Unotp_App_Config ----------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_App_Config', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_App_Config
(
    c_Key                VARCHAR(50)    NOT NULL,
    c_Value              NVARCHAR(200)  NOT NULL,
    c_Description        NVARCHAR(300)  NOT NULL CONSTRAINT DF_App_Config_Desc DEFAULT (''),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_App_Config_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_App_Config_Active DEFAULT (1),
    CONSTRAINT PK_App_Config PRIMARY KEY CLUSTERED (c_Key)
);
END
GO

/* ----- t_Unotp_Feature_Mst ---------------------------------------------------------
   The console's features, in the order the dashboard lays them out.
     c_Group        the dashboard section its tile sits in
     c_Off_Reason   what its tile says while the web app has it switched off
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Feature_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Feature_Mst
(
    c_Feature_Key        VARCHAR(20)    NOT NULL,
    c_Name               NVARCHAR(60)   NOT NULL,
    c_Group              NVARCHAR(60)   NOT NULL,
    c_Detail             NVARCHAR(300)  NOT NULL CONSTRAINT DF_Feature_Mst_Detail DEFAULT (''),
    c_Off_Reason         NVARCHAR(60)   NOT NULL CONSTRAINT DF_Feature_Mst_Off DEFAULT (N'Unavailable'),
    n_Seq                INT            NOT NULL,
    f_Tile               BIT            NOT NULL CONSTRAINT DF_Feature_Mst_Tile DEFAULT (1),   -- 0: switched like a feature, not a tile (admin)
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Feature_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Feature_Mst_Active DEFAULT (1),
    CONSTRAINT PK_Feature_Mst PRIMARY KEY CLUSTERED (c_Feature_Key)
);
END
GO

/* ----- t_Unotp_Ref_List ------------------------------------------------------------
   One row per entry of a list, in n_Seq order. c_Code is what the pages post and
   save; c_Name what they show. A list's other attributes (a category's flags, a
   payout's periods a year, a document group's items) are in j_Attrs as JSON.
   A text may say {renewFromDays} and the like: it is filled in from t_Unotp_App_Config.
   f_Active 0 takes an entry off the pages without losing what was saved with it.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Ref_List', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Ref_List
(
    n_Id                 INT IDENTITY(1,1) NOT NULL,
    c_List               VARCHAR(40)    NOT NULL,
    n_Seq                INT            NOT NULL,
    c_Code               NVARCHAR(200)  NOT NULL,
    c_Name               NVARCHAR(1000) NOT NULL,
    j_Attrs              NVARCHAR(MAX)  NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Ref_List_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Ref_List_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Ref_List PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Ref_List UNIQUE (c_List, c_Code)
);
END
GO

/* ----- t_Unotp_Console_Window ------------------------------------------------------
   A stretch of time in which the features it names are off. Ending one early
   sets d_Ended_On; one ended before it began is cancelled, and is left out.
     c_Features   the feature keys, comma-separated
     c_Notice     the line partners are shown in the bell; '' leaves them untold
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Console_Window', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Console_Window
(
    n_Id                 INT IDENTITY(1,1) NOT NULL,
    c_Window_Id          VARCHAR(20)    NOT NULL,
    c_Features           VARCHAR(200)   NOT NULL,
    d_From               DATETIME2(0)   NOT NULL,
    d_To                 DATETIME2(0)   NOT NULL,
    c_Notice             NVARCHAR(300)  NOT NULL CONSTRAINT DF_Console_Window_Notice DEFAULT (''),
    c_Set_By             NVARCHAR(150)  NOT NULL,
    d_Set_On             DATETIME2(3)   NOT NULL CONSTRAINT DF_Console_Window_Set DEFAULT (SYSDATETIME()),
    d_Ended_On           DATETIME2(3)   NULL,
    c_Ended_By           NVARCHAR(150)  NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Console_Window_Active DEFAULT (1),
    CONSTRAINT PK_Console_Window PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Console_Window_Id UNIQUE (c_Window_Id)
);
END
GO

/* ----- t_Unotp_Console_Notice ------------------------------------------------------
   A notice in the bell that stands on its own. Taking it down sets
   d_Removed_On; it stays on record.
     c_Kind   one of the noticeKinds in t_Unotp_Ref_List
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Console_Notice', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Console_Notice
(
    n_Id                 INT IDENTITY(1,1) NOT NULL,
    c_Notice_Id          VARCHAR(20)    NOT NULL,
    c_Kind               NVARCHAR(40)   NOT NULL,
    c_Title              NVARCHAR(200)  NOT NULL,
    d_At                 DATETIME2(0)   NOT NULL,
    c_Detail             NVARCHAR(1000) NOT NULL CONSTRAINT DF_Console_Notice_Detail DEFAULT (''),
    c_Set_By             NVARCHAR(150)  NOT NULL,
    d_Set_On             DATETIME2(3)   NOT NULL CONSTRAINT DF_Console_Notice_Set DEFAULT (SYSDATETIME()),
    d_Removed_On         DATETIME2(3)   NULL,
    c_Removed_By         NVARCHAR(150)  NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Console_Notice_Active DEFAULT (1),
    CONSTRAINT PK_Console_Notice PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Console_Notice_Id UNIQUE (c_Notice_Id)
);
END
GO

IF OBJECT_ID(N'dbo.s_Console_Id', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_Console_Id AS INT START WITH 1 INCREMENT BY 1 NO CACHE;
GO

/* ----- t_Unotp_Logs -----------------------------------------------------------------
   Every error and critical error the app writes, one row each, written in the
   background so a request never waits on it. Append-only.
     c_App_Name        the application: UnoTP
     c_Environment     Production, Staging, Development ...
     c_Level           Error or Critical
     c_Category        where it was logged (the class)
     c_Exception       the exception and its stack, when there was one
     c_Trace_Id        the request's trace id, as the error page shows it
     c_Request_*       the request it happened in; the query string is not kept
     c_App_No          the application the request was about, when there was one
     c_Created_By      the partner signed in, or 'system' outside a request
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Logs', N'U') IS NULL
BEGIN
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
    CONSTRAINT PK_Unotp_Logs PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Unotp_Logs_App_Created ON dbo.t_Unotp_Logs (c_App_Name, d_Created_On DESC) INCLUDE (c_Level, f_Active);
CREATE INDEX IX_Unotp_Logs_Trace ON dbo.t_Unotp_Logs (c_Trace_Id);
END
GO
