/* =============================================================================
   Uno TP - the tables behind the dashboard and the rest of the platform
   (SQL Server 2016 or later). Run after 001_unotp_tables.sql.

   t_App_Config         the rules the pages keep (GET config), key by key
   t_Feature_Mst        the console's features: the dashboard tiles and menu keys
   t_Ref_List           every list the pages offer (GET reference)
   t_Partner_Mst        the partners who sign in, as the portal sends them
   t_Partner_Menu       the features each partner's menu opens
   t_User_Session       a session started for a partner on entry
   t_Payment_Link       every payment or acceptance link sent to an investor
   t_Pay_In_Slip        every Axis pay-in slip generated, reprints included
   t_Console_Window     windows that take features off, with their notice
   t_Console_Notice     notices in the bell that stand on their own

   The platform's own values - features, config, reference lists - are seeded by
   003_unotp_seed.sql. Partners and their menus come from the portal's user
   master and are not seeded here.

   Safe to run again: each object is created only when it is not there yet.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ----- What happens to an application after submit -----------------------------
   Set by the investor's acceptance, the payment feed and the FD system as each
   happens. Until they are wired in, these stay NULL and the application reads as
   waiting on the investor.
   ----------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.t_Application_Mst', N'd_Accepted_On') IS NULL
    ALTER TABLE dbo.t_Application_Mst ADD
        d_Accepted_On   DATETIME2(3) NULL,   -- the investor accepted the deposit (a digital application)
        d_Paid_On       DATETIME2(3) NULL,   -- the payment was received
        d_Booked_On     DATETIME2(3) NULL,   -- the deposit was booked
        c_Fdr_No        VARCHAR(20)  NULL,   -- its receipt
        d_Cancelled_On  DATETIME2(3) NULL;   -- cancelled, unpaid past cancellationDays or by Operations
GO

/* ----- t_App_Config ----------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_App_Config', N'U') IS NULL
CREATE TABLE dbo.t_App_Config
(
    c_Key                VARCHAR(50)    NOT NULL,
    c_Value              NVARCHAR(200)  NOT NULL,
    c_Description        NVARCHAR(300)  NOT NULL CONSTRAINT DF_App_Config_Desc DEFAULT (''),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_App_Config_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_App_Config_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_App_Config PRIMARY KEY CLUSTERED (c_Key)
);
GO

/* ----- t_Feature_Mst ---------------------------------------------------------
   The console's features, in the order the dashboard lays them out.
     c_Group        the dashboard section its tile sits in
     c_Off_Reason   what its tile says while the web app has it switched off
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Feature_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Feature_Mst
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Feature_Mst_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Feature_Mst PRIMARY KEY CLUSTERED (c_Feature_Key)
);
GO

/* ----- t_Ref_List ------------------------------------------------------------
   One row per entry of a list, in n_Seq order. c_Code is what the pages post and
   save; c_Name what they show. A list's other attributes (a category's flags, a
   payout's periods a year, a document group's items) are in j_Attrs as JSON.
   A text may say {renewFromDays} and the like: it is filled in from t_App_Config.
   f_Active 0 takes an entry off the pages without losing what was saved with it.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Ref_List', N'U') IS NULL
CREATE TABLE dbo.t_Ref_List
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
    CONSTRAINT UQ_Ref_List UNIQUE (c_List, c_Code),
    CONSTRAINT CK_Ref_List_Attrs CHECK (j_Attrs IS NULL OR ISJSON(j_Attrs) = 1)
);
GO

/* ----- t_Partner_Mst ---------------------------------------------------------
   A partner who may sign in: the user id the portal sends, and how they source.
     c_Agency_Type   the sourcingAgency in t_App_Config sources as the house;
                     any other type sources as a broker under c_Broker_Code
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Partner_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Partner_Mst
(
    c_User_Id            VARCHAR(20)    NOT NULL,
    c_Name               NVARCHAR(150)  NOT NULL,
    c_Code               VARCHAR(20)    NOT NULL,
    c_Agency_Type        VARCHAR(10)    NOT NULL,
    c_Broker_Code        VARCHAR(20)    NOT NULL CONSTRAINT DF_Partner_Mst_Broker DEFAULT (''),
    c_Branch             NVARCHAR(100)  NOT NULL CONSTRAINT DF_Partner_Mst_Branch DEFAULT (''),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Partner_Mst_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Partner_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Partner_Mst PRIMARY KEY CLUSTERED (c_User_Id)
);
GO

IF OBJECT_ID(N'dbo.t_Partner_Menu', N'U') IS NULL
CREATE TABLE dbo.t_Partner_Menu
(
    c_User_Id            VARCHAR(20)    NOT NULL,
    c_Feature_Key        VARCHAR(20)    NOT NULL,
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Partner_Menu_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Partner_Menu_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Partner_Menu PRIMARY KEY CLUSTERED (c_User_Id, c_Feature_Key)
);
GO

/* ----- t_User_Session --------------------------------------------------------
   Started on entry from the portal; every call after carries it (X-Session-Id)
   and is refused with 401 once it has expired or ended.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_User_Session', N'U') IS NULL
CREATE TABLE dbo.t_User_Session
(
    c_Session_Id         CHAR(32)       NOT NULL,
    c_User_Id            VARCHAR(20)    NOT NULL,
    c_Sys_Code           VARCHAR(20)    NOT NULL,
    d_Started_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_User_Session_Started DEFAULT (SYSDATETIME()),
    d_Expires_On         DATETIME2(3)   NOT NULL,
    d_Ended_On           DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_User_Session_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_User_Session PRIMARY KEY CLUSTERED (c_Session_Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_User_Session_User')
    CREATE INDEX IX_User_Session_User ON dbo.t_User_Session (c_User_Id, d_Expires_On);
GO

/* ----- t_Payment_Link --------------------------------------------------------
   Every link sent to an investor, by SMS and e-mail both: on submit, on a resend,
   and from Short URL. Never updated: the latest for an application and purpose
   is the live one, and every one before it has stopped working.
     c_Purpose   payment, acceptance
     c_Mobile, c_Email   where it went, masked as the lists show them
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Payment_Link', N'U') IS NULL
CREATE TABLE dbo.t_Payment_Link
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    c_Purpose            VARCHAR(12)    NOT NULL,
    c_Url                VARCHAR(500)   NOT NULL CONSTRAINT DF_Payment_Link_Url DEFAULT (''),
    c_Short_Url          VARCHAR(300)   NOT NULL CONSTRAINT DF_Payment_Link_Short DEFAULT (''),
    c_Mobile             VARCHAR(20)    NOT NULL CONSTRAINT DF_Payment_Link_Mobile DEFAULT (''),
    c_Email              VARCHAR(150)   NOT NULL CONSTRAINT DF_Payment_Link_Email DEFAULT (''),
    d_Sent_On            DATETIME2(3)   NOT NULL CONSTRAINT DF_Payment_Link_Sent DEFAULT (SYSDATETIME()),
    d_Expires_On         DATETIME2(3)   NOT NULL,
    c_Sent_By            VARCHAR(20)    NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Payment_Link_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Payment_Link PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Payment_Link_Purpose CHECK (c_Purpose IN ('payment', 'acceptance'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payment_Link_App')
    CREATE INDEX IX_Payment_Link_App ON dbo.t_Payment_Link (c_App_No, c_Purpose, n_Id);
GO

/* ----- t_Pay_In_Slip ---------------------------------------------------------
   Every slip generated for an application paying by an instrument (cheque, DD);
   a reprint is a new row with a fresh number, and the latest is the one in force.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.s_Slip_No', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_Slip_No AS BIGINT START WITH 1 INCREMENT BY 1 NO CACHE;
GO
IF OBJECT_ID(N'dbo.t_Pay_In_Slip', N'U') IS NULL
CREATE TABLE dbo.t_Pay_In_Slip
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    c_Slip_No            VARCHAR(20)    NOT NULL,
    d_Generated_On       DATETIME2(3)   NOT NULL CONSTRAINT DF_Pay_In_Slip_Generated DEFAULT (SYSDATETIME()),
    c_Generated_By       VARCHAR(20)    NOT NULL,
    d_Deposited_On       DATETIME2(3)   NULL,   -- paid in at the bank, from the bank's feed
    f_Active             BIT            NOT NULL CONSTRAINT DF_Pay_In_Slip_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Pay_In_Slip PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Pay_In_Slip_No UNIQUE (c_Slip_No)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pay_In_Slip_App')
    CREATE INDEX IX_Pay_In_Slip_App ON dbo.t_Pay_In_Slip (c_App_No, n_Id);
GO

/* ----- t_Console_Window ------------------------------------------------------
   A stretch of time in which the features it names are off. Ending one early
   sets d_Ended_On; one ended before it began is cancelled, and is left out.
     c_Features   the feature keys, comma-separated
     c_Notice     the line partners are shown in the bell; '' leaves them untold
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Console_Window', N'U') IS NULL
CREATE TABLE dbo.t_Console_Window
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Console_Window_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Console_Window PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Console_Window_Id UNIQUE (c_Window_Id),
    CONSTRAINT CK_Console_Window_Span CHECK (d_To > d_From)
);
GO

/* ----- t_Console_Notice ------------------------------------------------------
   A notice in the bell that stands on its own. Taking it down sets
   d_Removed_On; it stays on record.
     c_Kind   one of the noticeKinds in t_Ref_List
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Console_Notice', N'U') IS NULL
CREATE TABLE dbo.t_Console_Notice
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Console_Notice_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Console_Notice PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Console_Notice_Id UNIQUE (c_Notice_Id)
);
GO
IF OBJECT_ID(N'dbo.s_Console_Id', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_Console_Id AS INT START WITH 1 INCREMENT BY 1 NO CACHE;
GO
