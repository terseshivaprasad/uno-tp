/* =============================================================================
   Uno TP - the tables that are new with the app (SQL Server 2016 or later): the
   application header and its working state, the settings, features and lists, the
   console's windows and notices and the error log,
   with the running number for a console entry.

   The transaction tables - the FD system's own, whose schemas it gave - are in a
   script of their own:

       sqlcmd -d UnoTP -i db/create_new_tables.sql
       sqlcmd -d UnoTP -i db/insert_seed.sql      (the settings, features and lists)

   A table is created, with its indexes, only when it is not there yet. A table
   already there is not touched at all: no column, index or row of it changes,
   whatever its columns are. Schema only: no rows are put in. Safe to run again.

   Every small list the pages offer (categories, payouts, relations, occupations,
   sources of funds and the rest) is rows of t_Unotp_Ref_List, not a table of its
   own. What has a master of its own in the FD system is read where it is, each by
   one query in the file of the page that reads it (UnoTP.Data/Queries), and has no table here: folios and
   their deposits, brokers, staff, bank branches, the Axis CMS locations and the
   payment gateway's banks.

   Every column of these tables starts with f_, as the FD system's do, and each
   table has f_Active: 0 takes a row out of use without deleting it. No table has
   a foreign key or a CHECK constraint: the app keeps those rules itself.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* An application's number is not made here: the app asks the FD system's own
   procedure for it, dbo.USP_FD_BTP_GetApplicationNo, which has to be in this database. */

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
    f_App_Id             BIGINT IDENTITY(1,1) NOT NULL,
    f_App_No             VARCHAR(20)    NOT NULL,
    f_Partner_Id         VARCHAR(20)    NOT NULL,   -- X-Partner-Id: the only partner who sees it
    f_Status             CHAR(3)        NOT NULL CONSTRAINT DF_Application_Mst_Status DEFAULT ('PEN'),
    f_Version            INT            NOT NULL CONSTRAINT DF_Application_Mst_Version DEFAULT (1),

    -- The version each section was last saved at: its current rows. NULL until saved.
    f_Upload_Ver         INT            NULL,
    f_Details_Ver        INT            NULL,
    f_Payment_Ver        INT            NULL,
    f_Deposit_Ver        INT            NULL,

    -- The investor, as Investor Identification found them when the application was opened.
    f_Pan                VARCHAR(10)    NOT NULL,
    f_Dob                DATE           NULL,
    f_Name               NVARCHAR(150)  NOT NULL,
    f_Folio              VARCHAR(20)    NOT NULL CONSTRAINT DF_Application_Mst_Folio DEFAULT (''),   -- '' for a new investor
    f_Gender             VARCHAR(20)    NOT NULL CONSTRAINT DF_Application_Mst_Gender DEFAULT (''),
    f_Address            NVARCHAR(500)  NOT NULL CONSTRAINT DF_Application_Mst_Address DEFAULT (''),
    f_Data_Source        VARCHAR(50)    NULL,       -- the folio's source, for an investor on one: written to f_Data_Source
    f_Pan_Filed          BIT            NOT NULL CONSTRAINT DF_Application_Mst_Pan_Filed DEFAULT (0),
    f_Rec_Pan            BIT            NULL,       -- what the folio already holds;
    f_Rec_Photo          BIT            NULL,       -- NULL for an investor with no folio
    f_Rec_Poa            BIT            NULL,

    -- The deposit a renewal renews; NULL for a new deposit.
    f_Renew_Dep_No       VARCHAR(20)    NULL,
    f_Renew_Amount       BIGINT         NULL,
    f_Renew_Matures_On   DATE           NULL,
    f_Renew_Rate         DECIMAL(5,2)   NULL,
    f_Renew_Tenure       INT            NULL,
    f_Renew_Payout       VARCHAR(20)    NULL,
    f_Renew_Principal    BIGINT         NULL,       -- the deposit's own amount: renewed when the principal only is

    -- The submission, once submitted.
    f_Submitted_On       DATETIME2(3)   NULL,
    -- The quote locked on submit, beside the rate on the deposit's 'APR' row.
    f_Quote_Interest_Each   DECIMAL(18,2) NULL,  -- interest each payout; 0 for a cumulative deposit
    f_Quote_Maturity_Amount DECIMAL(18,2) NULL,
    f_Quote_Matures_On      DATE          NULL,
    f_Quote_Rate_As_On      DATE          NULL,  -- the card the rate was read off
    f_Sub_Status         VARCHAR(30)    NULL,       -- payment-pending, then the backend's own
    f_Link_Sent_To       VARCHAR(20)    NULL,       -- masked
    f_Link_Emailed_To    VARCHAR(150)   NULL,       -- masked
    f_Link_Valid_Until   DATETIME2(3)   NULL,
    f_Resends_Left       INT            NULL,
    f_Short_Url          VARCHAR(300)   NULL,

    -- After submit: set by the investor's acceptance, the payment feed, the FD system and Operations.
    f_Accepted_On        DATETIME2(3)   NULL,
    f_Paid_On            DATETIME2(3)   NULL,
    f_Booked_On          DATETIME2(3)   NULL,
    f_Fdr_No             VARCHAR(20)    NULL,
    f_Cancelled_On       DATETIME2(3)   NULL,
    f_Penny_Drop_On      DATETIME2(3)   NULL,
    f_Penny_Drop_Status  VARCHAR(10)    NOT NULL CONSTRAINT DF_Application_Mst_Penny DEFAULT (''),
    f_Kyc_Verified_On    DATETIME2(3)   NULL,
    f_Kyc_Status         VARCHAR(10)    NOT NULL CONSTRAINT DF_Application_Mst_Kyc DEFAULT (''),

    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Application_Mst_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,

    f_Active             BIT            NOT NULL CONSTRAINT DF_Application_Mst_Active DEFAULT (1),
    CONSTRAINT PK_Application_Mst PRIMARY KEY CLUSTERED (f_App_Id),
    CONSTRAINT UQ_Application_Mst_App_No UNIQUE (f_App_No)
);
CREATE INDEX IX_Application_Mst_Partner ON dbo.t_Unotp_Application_Mst (f_Partner_Id, f_Status) INCLUDE (f_Created_On, f_Updated_On);
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
    f_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    f_App_No             VARCHAR(20)    NOT NULL,
    f_App_Version        INT            NOT NULL,
    f_Status             CHAR(3)        NOT NULL,
    f_Upload             NVARCHAR(MAX)  NOT NULL,
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Upload_State_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Upload_State_Active DEFAULT (1),
    CONSTRAINT PK_Upload_State PRIMARY KEY CLUSTERED (f_Id)
);
CREATE INDEX IX_Upload_State_App ON dbo.t_Unotp_Upload_State (f_App_No, f_App_Version);
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
    f_App_No             VARCHAR(20)    NOT NULL,
    f_Page               VARCHAR(30)    NOT NULL,
    f_State              NVARCHAR(MAX)  NOT NULL,
    f_Updated_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Page_State_Updated DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Page_State_Active DEFAULT (1),
    CONSTRAINT PK_Page_State PRIMARY KEY CLUSTERED (f_App_No, f_Page)
);
END
GO

/* ----- t_Unotp_App_Config ----------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_App_Config', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_App_Config
(
    f_Key                VARCHAR(50)    NOT NULL,
    f_Value              NVARCHAR(200)  NOT NULL,
    f_Description        NVARCHAR(300)  NOT NULL CONSTRAINT DF_App_Config_Desc DEFAULT (''),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_App_Config_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_App_Config_Active DEFAULT (1),
    CONSTRAINT PK_App_Config PRIMARY KEY CLUSTERED (f_Key)
);
END
GO

/* ----- t_Unotp_Feature_Mst ---------------------------------------------------------
   The console's features, in the order the dashboard lays them out.
     f_Group        the dashboard section its tile sits in
     f_Off_Reason   what its tile says while the web app has it switched off
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Feature_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Feature_Mst
(
    f_Feature_Key        VARCHAR(20)    NOT NULL,
    f_Name               NVARCHAR(60)   NOT NULL,
    f_Group              NVARCHAR(60)   NOT NULL,
    f_Detail             NVARCHAR(300)  NOT NULL CONSTRAINT DF_Feature_Mst_Detail DEFAULT (''),
    f_Off_Reason         NVARCHAR(60)   NOT NULL CONSTRAINT DF_Feature_Mst_Off DEFAULT (N'Unavailable'),
    f_Seq                INT            NOT NULL,
    f_Tile               BIT            NOT NULL CONSTRAINT DF_Feature_Mst_Tile DEFAULT (1),   -- 0: switched like a feature, not a tile (admin)
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Feature_Mst_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Feature_Mst_Active DEFAULT (1),
    CONSTRAINT PK_Feature_Mst PRIMARY KEY CLUSTERED (f_Feature_Key)
);
END
GO

/* ----- t_Unotp_Ref_List ------------------------------------------------------------
   The one master for every small list the pages offer. One row per entry of a
   list, in f_Seq order.

     f_Code     what the pages post and save
     f_Name     what they show. A text may say {renewFromDays} and the like: it is
                filled in from t_Unotp_App_Config
     f_Parent   the code of the entry this one belongs under, in another list; NULL
                where it belongs under none. This is how one drop-down depends on
                another: a sub occupation's parent is its occupation, and a row of
                'sourcingModeCategories' has the sourcing mode as its parent
     f_Attrs    the entry's own settings, as JSON (a category's flags, a payout's
                periods a year); NULL where it has none
     f_Active   0 takes an entry off the pages without losing what was saved with it

   The README ("The lists") says what each list is for and which settings it takes.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Ref_List', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Ref_List
(
    f_Id                 INT IDENTITY(1,1) NOT NULL,
    f_List               VARCHAR(40)    NOT NULL,
    f_Seq                INT            NOT NULL,
    f_Code               NVARCHAR(200)  NOT NULL,
    f_Name               NVARCHAR(1000) NOT NULL,
    f_Parent             NVARCHAR(200)  NULL,
    f_Attrs              NVARCHAR(MAX)  NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Ref_List_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Ref_List_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Ref_List PRIMARY KEY CLUSTERED (f_Id),
    CONSTRAINT UQ_Ref_List UNIQUE (f_List, f_Parent, f_Code)
);
END
GO

/* ----- t_Unotp_Console_Window ------------------------------------------------------
   A stretch of time in which the features it names are off. Ending one early
   sets f_Ended_On; one ended before it began is cancelled, and is left out.
     f_Features   the feature keys, comma-separated
     f_Notice     the line partners are shown in the bell; '' leaves them untold
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Console_Window', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Console_Window
(
    f_Id                 INT IDENTITY(1,1) NOT NULL,
    f_Window_Id          VARCHAR(20)    NOT NULL,
    f_Features           VARCHAR(200)   NOT NULL,
    f_From               DATETIME2(0)   NOT NULL,
    f_To                 DATETIME2(0)   NOT NULL,
    f_Notice             NVARCHAR(300)  NOT NULL CONSTRAINT DF_Console_Window_Notice DEFAULT (''),
    f_Set_By             NVARCHAR(150)  NOT NULL,
    f_Set_On             DATETIME2(3)   NOT NULL CONSTRAINT DF_Console_Window_Set DEFAULT (SYSDATETIME()),
    f_Ended_On           DATETIME2(3)   NULL,
    f_Ended_By           NVARCHAR(150)  NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Console_Window_Active DEFAULT (1),
    CONSTRAINT PK_Console_Window PRIMARY KEY CLUSTERED (f_Id),
    CONSTRAINT UQ_Console_Window_Id UNIQUE (f_Window_Id)
);
END
GO

/* ----- t_Unotp_Console_Notice ------------------------------------------------------
   A notice in the bell that stands on its own. Taking it down sets
   f_Removed_On; it stays on record.
     f_Kind   one of the noticeKinds in t_Unotp_Ref_List
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Console_Notice', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Console_Notice
(
    f_Id                 INT IDENTITY(1,1) NOT NULL,
    f_Notice_Id          VARCHAR(20)    NOT NULL,
    f_Kind               NVARCHAR(40)   NOT NULL,
    f_Title              NVARCHAR(200)  NOT NULL,
    f_At                 DATETIME2(0)   NOT NULL,
    f_Detail             NVARCHAR(1000) NOT NULL CONSTRAINT DF_Console_Notice_Detail DEFAULT (''),
    f_Set_By             NVARCHAR(150)  NOT NULL,
    f_Set_On             DATETIME2(3)   NOT NULL CONSTRAINT DF_Console_Notice_Set DEFAULT (SYSDATETIME()),
    f_Removed_On         DATETIME2(3)   NULL,
    f_Removed_By         NVARCHAR(150)  NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Console_Notice_Active DEFAULT (1),
    CONSTRAINT PK_Console_Notice PRIMARY KEY CLUSTERED (f_Id),
    CONSTRAINT UQ_Console_Notice_Id UNIQUE (f_Notice_Id)
);
END
GO

IF OBJECT_ID(N'dbo.s_Console_Id', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_Console_Id AS INT START WITH 1 INCREMENT BY 1 NO CACHE;
GO

/* ----- t_Unotp_Logs -----------------------------------------------------------------
   Every error and critical error the app writes, one row each, written in the
   background so a request never waits on it. Append-only.
     f_App_Name        the application: UnoTP
     f_Environment     Production, Staging, Development ...
     f_Level           Error or Critical
     f_Category        where it was logged (the class)
     f_Exception       the exception and its stack, when there was one
     f_Trace_Id        the request's trace id, as the error page shows it
     f_Request_*       the request it happened in; the query string is not kept
     f_App_No          the application the request was about, when there was one
     f_Created_By      the partner signed in, or 'system' outside a request
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Logs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Logs
(
    f_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    f_App_Name           VARCHAR(50)    NOT NULL,
    f_Environment        VARCHAR(30)    NOT NULL CONSTRAINT DF_Unotp_Logs_Env DEFAULT (''),
    f_Level              VARCHAR(12)    NOT NULL,
    f_Category           VARCHAR(200)   NOT NULL CONSTRAINT DF_Unotp_Logs_Category DEFAULT (''),
    f_Event_Id           INT            NOT NULL CONSTRAINT DF_Unotp_Logs_Event DEFAULT (0),
    f_Message            NVARCHAR(4000) NOT NULL,
    f_Exception          NVARCHAR(MAX)  NULL,
    f_Trace_Id           VARCHAR(64)    NULL,
    f_Request_Method     VARCHAR(10)    NULL,
    f_Request_Path       NVARCHAR(400)  NULL,
    f_App_No             VARCHAR(20)    NULL,
    f_Client_Ip          VARCHAR(45)    NULL,
    f_Machine_Name       VARCHAR(100)   NOT NULL CONSTRAINT DF_Unotp_Logs_Machine DEFAULT (''),
    f_Logged_At          DATETIME2(3)   NOT NULL,   -- when it happened, on the app's clock

    f_Created_By         VARCHAR(50)    NOT NULL CONSTRAINT DF_Unotp_Logs_Created_By DEFAULT ('system'),
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Unotp_Logs_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(50)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Unotp_Logs_Active DEFAULT (1),
    CONSTRAINT PK_Unotp_Logs PRIMARY KEY CLUSTERED (f_Id)
);
CREATE INDEX IX_Unotp_Logs_App_Created ON dbo.t_Unotp_Logs (f_App_Name, f_Created_On DESC) INCLUDE (f_Level, f_Active);
CREATE INDEX IX_Unotp_Logs_Trace ON dbo.t_Unotp_Logs (f_Trace_Id);
END
GO

