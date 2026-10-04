/* =============================================================================
   Uno TP - every table the app uses (SQL Server 2016 or later), in three parts:

     1. The tables that are new with this app: the application header and its
        working state, the settings, features and lists, the console's windows and
        notices, and the error log.
     2. The tables the database already has: what is entered on an application
        (KYC, addresses, documents, nominee, payment and repayment accounts, the
        deposit), the rate card, the partners, the payment links and the pay-in slips.
     3. The big tables the app looks rows up in: investor folios, brokers, staff,
        bank branches by IFSC and PIN codes, with the source of funds log.

       sqlcmd -d UnoTP -i db/create_tables.sql
       sqlcmd -d UnoTP -i db/insert_seed.sql      (the settings, features and lists)

   A table is created, with its indexes, only when it is not there yet. A table
   already there is not touched at all: no column, index or row of it changes,
   whatever its columns are. So on the database that already has the tables of
   parts 2 and 3, this script creates only part 1. Schema only: no rows are put
   in. Safe to run again.

   Every small list the pages offer (categories, payouts, relations, occupations,
   sources of funds and the rest) is rows of t_Unotp_Ref_List, not a table of its
   own. Only what has a master of its own is looked up where it is: the big tables
   of part 3, the rate card, the Axis CMS locations and the payment gateway's banks.

   Every column of the app's own tables starts with f_, as the FD system's do,
   and each table has f_Active: 0 takes a row out of use without deleting it.
   The FD system's tables keep its own names. No table has a foreign key or a
   CHECK constraint: the app keeps those rules itself.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =============================================================================
   PART 1 - THE TABLES THAT ARE NEW WITH THIS APP
   ============================================================================= */

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

/* =============================================================================
   PART 2 - THE TABLES THE DATABASE ALREADY HAS
   ============================================================================= */

/* ----- The FD system's own tables, column for column as the database has them -----
   What is entered on an application goes into these. None has a version column, a
   key or an index: a section's current rows are the application's active ones
   (f_Active = 1), and a save takes the rows it replaces out of use before it
   inserts the section afresh. Every row carries f_Source = 'UNO_TP', f_Status
   ('PEN' on a step's save; on submit 'APR', or 'PEN_E' for an application whose
   investor's KYC was fetched from CKYC), who wrote it (the user's
   Agency_Usr_Clustered_ID), from which session and address.

   A choice from a list is saved as the list's code (f_Code in t_Unotp_Ref_List);
   where the table has a description column beside it, the list's name goes there.
   ----------------------------------------------------------------------------- */

/* ----- t_FD_BT_KYC_document: one row per application, holder and document ----------
     f_Holder_Type_Code   01 the investor, 02 and 03 the joint holders. The application's
                          own documents - the form, the cheque, an employee proof, the
                          Form 121 - go under 01
     f_Doc_Type_Code, f_Doc_Sub_Type_Code, and their descriptions
                          how the document is coded: in t_Unotp_Ref_List the
                          'filedDocuments' list gives each document its sub-type,
                          'documentSubTypes' the sub-type's name and its type, and
                          'documentTypes' the type's name
     f_Doc_Filepath       where the copy is kept, in full: the document store's root
                          (Dms:Root), then application / holder / document; NULL for
                          one on the folio with no copy here
     f_Doc_Sequence       1, 2, 3... among a holder's documents of one type
     f_Document_Source, f_doc_source
                          UNO_TP for a copy uploaded here; NULL for one that came
                          over from the folio or the step before
     f_Doc_Ref_No         the number OCR read off it: a PAN, a passport, licence or
                          voter ID number, a cheque number; an Aadhaar's last four
                          digits only
     f_Doc_Exp_Date       when a passport or driving licence runs out
     f_IsDocumentMasked   1 for an Aadhaar filed with its number masked
     f_is_ocrextract      1 when OCR was asked to read it
     f_isocrdataextract, f_IsidfyDocDataExtracted   1 when OCR read something off it
     f_isocrdataverified, f_IsidfyDocDataVerified   1 when what was read was confirmed
     f_IsidfyDocIdentified, f_IdfyIdentifiedDocument
                          1 and what it was identified as
     f_IsidfyFaceCompared, f_IsidfyDocFaceDetected, f_IdfyFaceMatchPercentage
                          on the PAN copy and the proof of address: whether their
                          faces were compared, whether a face was found on this
                          copy, and the score, 0 to 100
                          Every one of these is NULL for a document no check was
                          run on. The new DMS columns are not the app's to fill.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_KYC_document', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_KYC_document](
    [f_Pk_t_FD_BT_KYC_document_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Appl_No] [nvarchar](50) NULL,
    [f_Holder_Type_Code] [nvarchar](50) NULL,
    [f_Doc_Type_Code] [nvarchar](50) NULL,
    [f_Doc_Sub_Type_Code] [nvarchar](100) NULL,
    [f_Doc_Type_Desc] [nvarchar](50) NULL,
    [f_Doc_Sub_Type_Desc] [nvarchar](50) NULL,
    [f_Doc_Ref_No] [nvarchar](50) NULL,
    [f_Doc_Exp_Date] [date] NULL,
    [f_Doc_FileName] [nvarchar](250) NULL,
    [f_Doc_Filepath] [nvarchar](max) NULL,
    [f_Active] [bit] NULL,
    [f_Session_ID] [nvarchar](50) NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](150) NULL,
    [f_CreatedType] [nvarchar](10) NULL,
    [f_CreatedDate] [datetime] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedByUName] [nvarchar](150) NULL,
    [f_UpdatedType] [nvarchar](10) NULL,
    [f_UpdatedDate] [datetime] NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_Source] [nvarchar](100) NULL,
    [f_Status] [nvarchar](20) NULL,
    [f_FolioNo] [nvarchar](50) NULL,
    [f_Doc_Sequence] [nvarchar](50) NULL,
    [f_Document_Source] [nvarchar](50) NULL,
    [f_Doc_New_DMS_Id] [nvarchar](100) NULL,
    [f_Doc_New_DMS_URL] [nvarchar](max) NULL,
    [f_IsAddedto_NEW_DMS] [bit] NULL,
    [f_IsDocumentMasked] [bit] NULL,
    [f_Doc_New_DMS_Error] [nvarchar](max) NULL,
    [f_doc_source] [nvarchar](50) NULL,
    [f_is_ocrextract] [bit] NULL,
    [f_isocrdataextract] [bit] NULL,
    [f_isocrdataverified] [bit] NULL,
    [f_IsidfyDocIdentified] [bit] NULL,
    [f_IdfyIdentifiedDocument] [nvarchar](50) NULL,
    [f_IdfyIdentifiedDocPercentage] [decimal](18, 0) NULL,
    [f_IsidfyDocFaceDetected] [bit] NULL,
    [f_IsidfyDocDataExtracted] [bit] NULL,
    [f_IsidfyDocDataVerified] [bit] NULL,
    [f_IsidfyFaceCompared] [bit] NULL,
    [f_IdfyFaceMatchPercentage] [decimal](18, 0) NULL,
    [F_Doc_New_DMS_Upload_Date] [datetime] NULL
);
END
GO

/* ----- t_FD_BT_Kyc_Data_Dtl: one row per holder --------------------------------------
   Who each holder is and what Investor Information took down about them. The
   holder's whole name goes in f_Kyc_FirstName and in f_Kyc_FullName; the name they
   gave under the father's, mother's or spouse's columns, which says whose it is.
   The gender is kept as the name prefix (Mr, Mrs or Miss) and read back off it.
   The mobile and e-mail are on the holder's permanent address in
   t_FD_BT_Address_Dtl. The FATCA answers have no column: the page keeps them with
   its typed fields, and a "yes" stops the application going on online.

     f_Kyc_Number    the CKYC reference number CERSAI's search gives, once CKYC is
                     fetched for the investor
     f_IsMinor       1 for a holder under the minimum age today, from the date of birth
     f_CustSeg_Type_Code, f_CustSeg_Subtype_Code, f_Kyc_Occupation_Code, f_Kyc_Occupation_Desc
                     the codes of the occupation master's row for the occupation and
                     sub occupation chosen (t_FD_CMN_Ckyc_CustSeg_Mst)
     f_Data_Source   where the holder's KYC came from: FRESH, given on the application;
                     CKYC, fetched from CERSAI; for a holder on a folio, where their
                     latest KYC is kept: ORA, BT or FHLD
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Kyc_Data_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Kyc_Data_Dtl](
    [f_Pk_t_FD_BT_Kyc_Data_Dtl_id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Holder_Type] [nvarchar](10) NOT NULL,
    [f_Appl_No] [nvarchar](50) NOT NULL,
    [f_Kyc_ConstiType] [nvarchar](2) NOT NULL, -- 01
    [f_Kyc_Number] [nvarchar](50) NULL, --CKYC Number
    [f_Kyc_NamePrefix] [nvarchar](5) NOT NULL, --Based on gender(M/F) and Marital status => (Mr/Mrs/Miss)
    [f_Kyc_FirstName] [nvarchar](50) NOT NULL, -- Full name
    [f_Kyc_MiddleName] [nvarchar](50) NULL,
    [f_Kyc_LastName] [nvarchar](50) NULL,
    [f_Kyc_FullName] [nvarchar](200) NULL, -- Full name
    [f_Kyc_FatherNamePrefix] [nvarchar](5) NULL,
    [f_Kyc_FatherFirstName] [nvarchar](50) NULL,
    [f_Kyc_FatherMiddleName] [nvarchar](50) NULL,
    [f_Kyc_FatherLastName] [nvarchar](50) NULL,
    [f_Kyc_FatherFullName] [nvarchar](200) NULL,
    [f_Kyc_SpouseNamePrefix] [nvarchar](5) NULL,
    [f_Kyc_SpouseFirstName] [nvarchar](50) NULL,
    [f_Kyc_SpouseMiddleName] [nvarchar](50) NULL,
    [f_Kyc_SpouseLastName] [nvarchar](50) NULL,
    [f_Kyc_SpouseFullName] [nvarchar](200) NULL,
    [f_Kyc_MotherNamePrefix] [nvarchar](5) NULL,
    [f_Kyc_MotherFirstName] [nvarchar](50) NULL,
    [f_Kyc_MotherMiddletName] [nvarchar](50) NULL,
    [f_Kyc_MotherLastName] [nvarchar](50) NULL,
    [f_Kyc_MotherFullName] [nvarchar](200) NULL,
    [f_Kyc_MaritalStatus] [nvarchar](10) NULL,
    [f_Kyc_Nationality_Code] [nvarchar](10) NULL, -- IN
    [f_Kyc_Nationality_Desc] [nvarchar](150) NULL, -- India
    [f_Kyc_Occupation_Code] [nvarchar](10) NULL,
    [f_Kyc_Occupation_Desc] [nvarchar](50) NULL,
    [f_Kyc_DOB] [date] NULL,
    [f_Active] [bit] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedType] [nvarchar](10) NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedType] [nvarchar](10) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_SessionID] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_UpdatedByUName] [nvarchar](100) NULL,
    [f_Kyc_PAN] [nvarchar](20) NULL,
    [f_IsMinor] [bit] NULL,
    [f_Source] [nvarchar](100) NULL, --UNO_TP
    [f_Status] [nvarchar](20) NULL,
    [f_IsEditForCKYC] [bit] NULL,
    [f_FolioNo] [nvarchar](50) NULL,
    [f_Data_Source] [nvarchar](10) NULL,
    [f_Source_Table_Id] [nvarchar](50) NULL,
    [f_Kyc_AnnualIncome_Code] [nvarchar](50) NULL,
    [f_Kyc_AnnualIncome_Desc] [nvarchar](100) NULL,
    [f_CustSeg_Type_Code] [nvarchar](50) NULL, --Occupation
    [f_CustSeg_Type_desc] [nvarchar](250) NULL,
    [f_CustSeg_Subtype_Code] [nvarchar](50) NULL, -- Sub Occupation
    [f_CustSeg_Subtype_Desc] [nvarchar](250) NULL,
    [f_NSA_Response] [nvarchar](50) NULL,
    [f_NSA_Date] [datetime] NULL,
    [f_IsPEP] [bit] NULL,
    [f_IsPEP_Relative] [bit] NULL,
    [f_IsFaceToFace] [bit] NULL,
    [f_IsOSV] [bit] NULL,
    [f_IsPanVerified] [char](1) NULL
);
END
GO

/* ----- t_FD_BT_Address_Dtl: one row per holder and address type --------------------
     f_AddType_Code   PER  - permanent, as on record for the holder; always written, for
                             its row carries the holder's mobile and e-mail. A long
                             address is broken across f_Add1 to f_Add3 at its spaces
                      MAIL - mailing, typed on Investor Information when post goes
                             elsewhere; no row when it goes to the permanent one
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Address_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Address_Dtl](
    [f_Pk_t_FD_BT_Address_Dtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Holder_Type] [nvarchar](10) NOT NULL,
    [f_Appl_No] [nvarchar](50) NOT NULL,
    [f_AddType_Code] [nvarchar](10) NOT NULL,
    [f_AddType_Desc] [nvarchar](50) NULL,
    [f_Add1] [nvarchar](100) NULL,
    [f_Add2] [nvarchar](100) NULL,
    [f_Add3] [nvarchar](100) NULL,
    [f_AddCity_Code] [nvarchar](50) NULL,
    [f_AddCity_Desc] [nvarchar](50) NULL,
    [f_AddDistrict_Code] [nvarchar](50) NULL,
    [f_AddDistrict_Desc] [nvarchar](50) NULL,
    [f_AddState_Code] [nvarchar](50) NULL,
    [f_AddState_Desc] [nvarchar](50) NULL,
    [f_AddCountry_Code] [nvarchar](50) NULL,
    [f_AddCountry_Desc] [nvarchar](150) NULL,
    [f_AddPin] [nvarchar](10) NULL,
    [f_MobileNumber] [nvarchar](50) NULL,
    [f_EmailAdd] [nvarchar](100) NULL,
    [f_Active] [bit] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedType] [nvarchar](10) NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedType] [nvarchar](10) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_SessionID] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_UpdatedByUName] [nvarchar](100) NULL,
    [f_Source] [nvarchar](100) NULL,
    [f_Status] [nvarchar](20) NULL,
    [f_FolioNo] [nvarchar](50) NULL
);
END
GO

/* ----- t_FD_BT_Nominee_Dtl -----------------------------------------------------------
   The nominee. f_Is_Nominee_Minor is worked out from the date of birth: under the
   minimum age on the day of the save. A guardian is asked for only for a minor;
   the address is the guardian's. No row when no nominee is named.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Nominee_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Nominee_Dtl](
    [f_Pk_t_FD_BT_Nominee_Dtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Appl_No] [nvarchar](50) NOT NULL,
    [f_Nominee_Salutation] [nvarchar](20) NULL,
    [f_Nominee_Name] [nvarchar](250) NOT NULL,
    [f_Nominee_First_Name] [nvarchar](50) NULL,
    [f_Nominee_Middle_Name] [nvarchar](50) NULL,
    [f_Nominee_Last_Name] [nvarchar](50) NULL,
    [f_Nominee_Relations] [nvarchar](20) NOT NULL,
    [f_Nominee_DOB] [date] NULL,
    [f_Is_Nominee_Minor] [bit] NULL,
    [f_EmailID] [nvarchar](50) NULL,
    [f_MobileNo] [nvarchar](10) NULL,
    [f_Nominee_Status] [nvarchar](50) NULL,
    [f_GuardianName] [nvarchar](100) NULL,
    [f_Address1] [nvarchar](140) NULL,
    [f_Address2] [nvarchar](140) NULL,
    [f_Address3] [nvarchar](140) NULL,
    [f_City] [nvarchar](100) NULL,
    [f_StateCode] [nvarchar](50) NULL,
    [f_DistrictCode] [nvarchar](50) NULL,
    [f_StateName] [nvarchar](150) NULL,
    [f_DistrictName] [nvarchar](150) NULL,
    [f_Active] [bit] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedByUName] [nvarchar](100) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_SessionId] [bigint] NULL,
    [f_Guardian_Salution] [nvarchar](50) NULL,
    [f_Guardian_First_Name] [nvarchar](50) NULL,
    [f_Guardian_Middle_Name] [nvarchar](50) NULL,
    [f_Guardian_Last_Name] [nvarchar](50) NULL,
    [f_PIN] [nvarchar](10) NULL,
    [f_Country_Code] [nvarchar](50) NULL,
    [f_Country_Desc] [nvarchar](100) NULL,
    [f_Source] [nvarchar](100) NULL,
    [f_Status] [nvarchar](20) NULL,
    [f_FolioNo] [nvarchar](50) NULL
);
END
GO

/* ----- t_FD_BT_Payment_Dtl ------------------------------------------------------------
   How the deposit is paid, the account it is paid from, and the cheque or DD with
   the Axis Bank CMS location it is presented at, by code and name (f_CMS_Loc_CD,
   f_CMS_Loc_Desc: the 'cmsLocations' list). One active row per application.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Payment_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Payment_Dtl](
    [f_Pk_t_FD_BT_Other_Dtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Appl_No] [nvarchar](50) NOT NULL,
    [f_Payment_Mode] [nvarchar](50) NULL,
    [f_Cheque_DD_No] [nvarchar](50) NULL,
    [f_Cheque_DD_Date] [date] NULL,
    [f_Drawn_Bank_Name] [nvarchar](50) NULL,
    [f_Bank_Branch_Name] [nvarchar](50) NULL,
    [f_Bank_MICR] [nvarchar](50) NULL,
    [f_Bank_NEFT] [nvarchar](50) NULL,
    [f_Active] [bit] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedByUName] [nvarchar](100) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_SessionId] [bigint] NULL,
    [f_Source] [nvarchar](100) NULL,
    [f_Status] [nvarchar](20) NULL,
    [f_CMS_Loc_CD] [nvarchar](50) NULL,
    [f_CMS_Loc_Desc] [nvarchar](100) NULL,
    [f_FolioNo] [nvarchar](50) NULL,
    [f_CMS_BANK_NAME] [nvarchar](50) NULL, -- Axis Bank
    [f_BankAccountNo] [nvarchar](100) NULL
);
END
GO

/* ----- t_FD_BT_Investor_Bank_Dtl: the repayment account ------------------------------
   Where interest and the maturity amount are paid. f_sameAsCheque set, it is the
   payment account. The account's columns take no NULL: one not given yet is blank.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Investor_Bank_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Investor_Bank_Dtl](
    [f_Pk_t_FD_BT_Investor_Bank_Dtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Appl_No] [nvarchar](50) NOT NULL,
    [f_MICRCode] [nvarchar](50) NOT NULL,
    [f_NEFTCode] [nvarchar](50) NOT NULL,
    [f_BankName] [nvarchar](50) NOT NULL,
    [f_BranchName] [nvarchar](50) NOT NULL,
    [f_BankAccountNo] [nvarchar](50) NOT NULL,
    [f_Active] [bit] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedByUName] [nvarchar](100) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_SessionId] [bigint] NULL,
    [f_Source] [nvarchar](100) NULL,
    [f_Status] [nvarchar](20) NULL,
    [f_FolioNo] [nvarchar](50) NULL,
    [f_IsProvBank] [bit] NULL,
    [f_sameAsCheque] [bit] NULL
);
END
GO

/* ----- t_FD_BOTC_SCHEME: the rate card -------------------------------------------------
   The FD system's own rate card: one row per scheme code. Here with the columns the
   app reads and those seen beside them; the column types are the app's reading of
   them, not the FD system's own script, and its table may have more columns.

     CATEGORY        the rate card's category (a code of the 'categories' list)
     MODE_STATUS     AF a fresh application, R a renewal
     SCHEME, INTEREST_FREQ   what a payout is (a code of the 'payouts' list, and its scheme)
     PERIOD          the tenure, months
     MINIMUM_AMOUNT, MAXIMUM_AMOUNT   the deposits the row is for, rupees, both ends included
     FROM_DATE, TO_DATE      when the row is in effect; no TO_DATE, it still is
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BOTC_SCHEME', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BOTC_SCHEME](
    [SCHEME_ID] [int] IDENTITY(1,1) NOT NULL,
    [SCHEME] [varchar](50) NULL,
    [PERIOD] [int] NULL,
    [CATEGORY] [varchar](50) NULL,
    [SCHEME_CODE] [varchar](50) NULL,
    [MINIMUM_AMOUNT] [numeric](18, 2) NULL,
    [INTEREST_RATES] [numeric](9, 4) NULL,
    [MATURITY_VALUE] [numeric](18, 4) NULL,
    [BROKERGARE_PAYABLE] [numeric](9, 4) NULL,
    [INTEREST_FREQ] [varchar](50) NULL,
    [FROM_DATE] [datetime] NULL,
    [TO_DATE] [datetime] NULL,
    [CATEGORY_CODE] [varchar](10) NULL,
    [MODE_STATUS] [varchar](10) NULL,
    [MAXIMUM_AMOUNT] [numeric](18, 2) NULL
);
END
GO

/* ----- t_FD_BT_Investment_Dtl: the deposit as configured ---------------------------
   The FD system's own table, column for column as the database has it. FD
   Configuration, with the application type, category, sourcing and employee
   details chosen on Upload Documents, and the deposit a renewal renews.

   The rate, scheme and scheme code are the rate card's row for the deposit, on
   every save; the rest of the quote is on t_Unotp_Application_Mst.

     f_Amount        rupees            f_Tenure       months, a number
     f_Category      the category: a code of the 'categories' list, which is the rate card's CATEGORY
     f_Scheme, f_Scheme_Code, f_Int_Rate   the SCHEME, SCHEME_CODE and INTEREST_RATES of the rate card's row for the deposit
     f_Int_Freq      the payout: a code of the 'payouts' list, which is the rate card's INTEREST_FREQ
     f_Renewal_For   under auto renewal: P principal, F principal and interest
     f_TDS_Flag      Y tax deducted, N the form for none was filed
     f_EmpHolder     01, 02, 03        f_Source       UNO_TP, always
     f_Broker_Code   the source code (a house code for the house's own modes)
     f_ExistingFDRNo                the deposit a renewal renews
     f_ExistingFDRNoRenewalFor      on a renewal, what of it is renewed: P or F
     f_Depositor_Status_Code, f_Ind_Nind   IND, always: an individual
     f_HNG                          121 when Form 121 is filed for no tax to be deducted
     f_Cms_Location_Code, _Name     the cheque's CMS location, as on t_FD_BT_Payment_Dtl
     f_FolioNo, f_CreatedByUName    the investor's folio and the signed-in user's name, on every table here
     f_CreatedBy                    the signed-in user's Agency_Usr_Clustered_ID
     f_AML_Source_Of_Funds_reason   why it was asked: Occupation or Annual Income
     f_SessionId, f_CreatedIP       the partner's backend session and browser address
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Investment_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Investment_Dtl](
    [f_Pk_t_FD_BT_Investment_Dtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Broker_Code] [nvarchar](50) NULL,
    [f_Appl_No] [nvarchar](50) NULL,
    [f_Depositor_Status_Code] [nvarchar](20) NULL,
    [f_Category] [nvarchar](50) NULL,
    [f_Scheme] [nvarchar](50) NULL,
    [f_Scheme_Code] [nvarchar](50) NULL,
    [f_Int_Rate] [numeric](18, 2) NULL,
    [f_Int_Freq] [nvarchar](50) NULL,
    [f_Tenure] [nvarchar](50) NULL,
    [f_FDR_Dispatch_Mode] [nvarchar](50) NULL,
    [f_Renewal_For] [nvarchar](50) NULL,
    [f_HNG] [nvarchar](50) NULL,
    [f_TDS_Flag] [nvarchar](1) NULL,
    [f_Is_Auto_Renewal] [bit] NULL,
    [f_Amount] [numeric](18, 2) NULL,
    [f_Cms_Location_Code] [nvarchar](50) NULL,
    [f_Cms_Location_Name] [nvarchar](50) NULL,
    [f_Active] [bit] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_UpdatedBy] [nvarchar](50) NULL,
    [f_UpdatedByUName] [nvarchar](100) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_UpdatedIP] [nvarchar](50) NULL,
    [f_SessionId] [bigint] NULL,
    [f_Employee_Code] [nvarchar](100) NULL,
    [f_Source] [nvarchar](100) NULL,
    [f_Status] [nvarchar](20) NULL,
    [f_FDRNo] [nvarchar](50) NULL,
    [f_FolioNo] [nvarchar](50) NULL,
    [f_Ind_Nind] [nvarchar](50) NULL,
    [f_ApplicationDeclarationType] [nvarchar](20) NULL, --PHYSICAL/DIGITAL
    [f_ExistingFDRNo] [nvarchar](50) NULL, --Renewal FDR No
    [f_ExistingFDRNoRenewalFor] [nvarchar](10) NULL, -- Future Renewal For(P - Principal/F - Principal+Intr)
    [f_Relation] [nvarchar](50) NULL,
    [f_AML_Source_Of_Funds] [varchar](50) NULL,
    [f_AML_Source_Of_Funds_Remarks] [varchar](400) NULL,
    [f_AML_Source_Of_Funds_reason] [varchar](100) NULL, -- Validation reason - Occupation or Annual Income
    [f_EmpRelation] [nvarchar](50) NULL,
    [f_EmpCompanyName] [nvarchar](50) NULL,
    [f_EmpHolder] [nvarchar](10) NULL
);
END
GO

/* ----- t_Unotp_Partner_Mst ---------------------------------------------------------
   A partner who may sign in: the user id the portal sends, and how they source.
     f_Agency_Type   the sourcingAgency in t_Unotp_App_Config sources as the house;
                     any other type sources as a broker under f_Broker_Code
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Partner_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Partner_Mst
(
    f_User_Id            VARCHAR(20)    NOT NULL,
    f_Name               NVARCHAR(150)  NOT NULL,
    f_Code               VARCHAR(20)    NOT NULL,
    f_Agency_Type        VARCHAR(10)    NOT NULL,
    f_Broker_Code        VARCHAR(20)    NOT NULL CONSTRAINT DF_Partner_Mst_Broker DEFAULT (''),
    f_Branch             NVARCHAR(100)  NOT NULL CONSTRAINT DF_Partner_Mst_Branch DEFAULT (''),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Partner_Mst_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Partner_Mst_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Partner_Mst PRIMARY KEY CLUSTERED (f_User_Id)
);
END
GO

/* ----- t_Unotp_Payment_Link --------------------------------------------------------
   Every link sent to an investor, by SMS and e-mail both: on submit, on a resend,
   and from Short URL. Never updated: the latest for an application and purpose
   is the live one, and every one before it has stopped working.
     f_Purpose   payment, acceptance
     f_Mobile, f_Email   where it went, masked as the lists show them
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Payment_Link', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Payment_Link
(
    f_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    f_App_No             VARCHAR(20)    NOT NULL,
    f_Purpose            VARCHAR(12)    NOT NULL,
    f_Url                VARCHAR(500)   NOT NULL CONSTRAINT DF_Payment_Link_Url DEFAULT (''),
    f_Short_Url          VARCHAR(300)   NOT NULL CONSTRAINT DF_Payment_Link_Short DEFAULT (''),
    f_Mobile             VARCHAR(20)    NOT NULL CONSTRAINT DF_Payment_Link_Mobile DEFAULT (''),
    f_Email              VARCHAR(150)   NOT NULL CONSTRAINT DF_Payment_Link_Email DEFAULT (''),
    f_Sent_On            DATETIME2(3)   NOT NULL CONSTRAINT DF_Payment_Link_Sent DEFAULT (SYSDATETIME()),
    f_Expires_On         DATETIME2(3)   NOT NULL,
    f_Sent_By            VARCHAR(20)    NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Payment_Link_Active DEFAULT (1),
    CONSTRAINT PK_Payment_Link PRIMARY KEY CLUSTERED (f_Id)
);
CREATE INDEX IX_Payment_Link_App ON dbo.t_Unotp_Payment_Link (f_App_No, f_Purpose, f_Id);
END
GO

IF OBJECT_ID(N'dbo.s_Slip_No', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_Slip_No AS BIGINT START WITH 1 INCREMENT BY 1 NO CACHE;
GO

/* ----- t_Unotp_Pay_In_Slip ---------------------------------------------------------
   Every slip generated for an application paying by an instrument (cheque, DD);
   a reprint is a new row with a fresh number, and the latest is the one in force.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Pay_In_Slip', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Pay_In_Slip
(
    f_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    f_App_No             VARCHAR(20)    NOT NULL,
    f_Slip_No            VARCHAR(20)    NOT NULL,
    f_Generated_On       DATETIME2(3)   NOT NULL CONSTRAINT DF_Pay_In_Slip_Generated DEFAULT (SYSDATETIME()),
    f_Generated_By       VARCHAR(20)    NOT NULL,
    f_Deposited_On       DATETIME2(3)   NULL,   -- paid in at the bank, from the bank's feed
    f_Active             BIT            NOT NULL CONSTRAINT DF_Pay_In_Slip_Active DEFAULT (1),
    CONSTRAINT PK_Pay_In_Slip PRIMARY KEY CLUSTERED (f_Id),
    CONSTRAINT UQ_Pay_In_Slip_No UNIQUE (f_Slip_No)
);
CREATE INDEX IX_Pay_In_Slip_App ON dbo.t_Unotp_Pay_In_Slip (f_App_No, f_Id);
END
GO

/* =============================================================================
   PART 3 - THE BIG TABLES THE APP LOOKS ROWS UP IN
   ============================================================================= */

/* ----- t_Unotp_Investor_Folio -------------------------------------------------------
   One row per folio. A PAN held on more than one folio is a record Operations
   has to merge; Investor Identification refuses it by PAN until they do.
     f_Dob        NULL when the register holds none: refused until it is updated
     f_Doc_*      the documents the folio already holds, not asked for again
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Investor_Folio', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Investor_Folio
(
    f_Folio              VARCHAR(20)    NOT NULL,
    f_Pan                VARCHAR(10)    NOT NULL,
    f_Dob                DATE           NULL,
    f_Name               NVARCHAR(150)  NOT NULL,
    f_Gender             VARCHAR(20)    NOT NULL CONSTRAINT DF_Investor_Folio_Gender DEFAULT (''),
    f_Address            NVARCHAR(500)  NOT NULL CONSTRAINT DF_Investor_Folio_Address DEFAULT (''),
    f_Doc_Pan            BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Pan DEFAULT (0),
    f_Doc_Photo          BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Photo DEFAULT (0),
    f_Doc_Poa            BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Poa DEFAULT (0),
    f_Note               NVARCHAR(200)  NOT NULL CONSTRAINT DF_Investor_Folio_Note DEFAULT (''),   -- e.g. CKYC available with us
    f_Source             VARCHAR(50)    NULL,       -- where the folio's KYC came from, as the FD system says: written to f_Data_Source
    f_Kyc_Compliant      BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Compliant DEFAULT (0),   -- 1: the KYC is complete, so its details open Investor Information
    f_Active             BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Investor_Folio_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Investor_Folio PRIMARY KEY CLUSTERED (f_Folio)
);
CREATE INDEX IX_Investor_Folio_Pan ON dbo.t_Unotp_Investor_Folio (f_Pan) INCLUDE (f_Active);
END
GO

/* ----- t_Unotp_Broker_Mst, t_Unotp_Staff_Mst ---------------------------------------------
   The registers a sourcing code is searched on as it is typed. Both are big, so
   the app never reads one whole: it looks a code up by itself and searches only
   once three characters are typed. The staff register includes the partners who
   are employees, so one can code an application to themselves.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Broker_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Broker_Mst
(
    f_Code               VARCHAR(20)    NOT NULL,
    f_Name               NVARCHAR(150)  NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Broker_Mst_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Broker_Mst_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Broker_Mst PRIMARY KEY CLUSTERED (f_Code)
);
END
GO

-- t_Unotp_Staff_Mst: a development stand-in for the employee register. The app reads
-- the staff from the employee view named in UnoTP.Data/MasterQueries.cs, each sourcing
-- mode by its own query there.
IF OBJECT_ID(N'dbo.t_Unotp_Staff_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Staff_Mst
(
    f_Code               VARCHAR(20)    NOT NULL,
    f_Name               NVARCHAR(150)  NOT NULL,
    f_Department         NVARCHAR(100)  NOT NULL CONSTRAINT DF_Staff_Mst_Department DEFAULT (N''),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Staff_Mst_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Staff_Mst_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Staff_Mst PRIMARY KEY CLUSTERED (f_Code)
);
END
GO

/* ----- t_Unotp_Ifsc_Mst ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.t_Unotp_Ifsc_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Ifsc_Mst
(
    f_Ifsc               CHAR(11)       NOT NULL,
    f_Bank               NVARCHAR(100)  NOT NULL,
    f_Branch             NVARCHAR(150)  NOT NULL,
    f_Micr               VARCHAR(9)     NOT NULL CONSTRAINT DF_Ifsc_Mst_Micr DEFAULT (''),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Ifsc_Mst_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Ifsc_Mst_Created DEFAULT (SYSDATETIME()),
    f_Updated_By         VARCHAR(20)    NULL,
    f_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Ifsc_Mst PRIMARY KEY CLUSTERED (f_Ifsc)
);
CREATE INDEX IX_Ifsc_Mst_Micr ON dbo.t_Unotp_Ifsc_Mst (f_Micr);
END
GO

/* ----- t_Unotp_Pincode_Mst ---------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Pincode_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Pincode_Mst
(
    f_Pin_Code           CHAR(6)        NOT NULL,
    f_District           NVARCHAR(100)  NOT NULL,
    f_State              NVARCHAR(100)  NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Pincode_Mst_Active DEFAULT (1),
    f_Created_By         VARCHAR(20)    NOT NULL,
    f_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Pincode_Mst_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Pincode_Mst PRIMARY KEY CLUSTERED (f_Pin_Code)
);
END
GO

/* ----- t_FD_CMN_AML_Source_Of_Funds_Log -----------------------------------------------
   The FD system's log of the source of funds an investor gave, with the amount,
   annual income and occupation it was asked for. The app writes a row
   once the deposit's save has committed.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_CMN_AML_Source_Of_Funds_Log', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_CMN_AML_Source_Of_Funds_Log](
    [f_Pk_t_FD_CMN_AML_Source_Of_Funds_Log_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Sys_Ref_no] [nvarchar](50) NULL,
    [f_Appl_No] [nvarchar](50) NULL,
    [f_Holder_Type] [nvarchar](20) NULL,
    [f_Investment_Amt] [decimal](18, 2) NULL,
    [f_AnnualIncome_Code] [nvarchar](20) NULL,
    [f_AnnualIncome_Desc] [nvarchar](250) NULL,
    [f_AML_Source_Of_Funds] [nvarchar](50) NULL,
    [f_AML_Source_Of_Funds_Remarks] [nvarchar](1000) NULL,
    [f_AML_Source_Of_Funds_reason] [nvarchar](1000) NULL,
    [f_Active] [bit] NULL,
    [f_CreatedBy] [nvarchar](50) NULL,
    [f_CreatedByUName] [nvarchar](100) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_CreatedIP] [nvarchar](50) NULL,
    [f_SessionId] [bigint] NULL,
    [f_FormCode] [nvarchar](50) NULL,
    [f_Source] [nvarchar](50) NULL,
    [f_Folio_No] [varchar](20) NULL,
    [f_Occupation_Code] [varchar](20) NULL,
    [f_Occupation_Desc] [varchar](50) NULL
);
END
GO
