/* =============================================================================
   Uno TP - the application tables (SQL Server 2016 or later).

   An application is one row in t_Application_Mst, and everything entered on it
   is rows in the detail tables below. A detail row is never updated or deleted:
   every save inserts the section afresh, so each earlier save stays on record.

     c_Status   'PEN' - saved on a step, before the application is submitted.
                'APR' - written once, on submit: the whole application as it was
                        submitted, in a fresh set of rows.

     n_App_Version  The application's version the rows were saved at. A section's
                    current rows are those at the version t_Application_Mst holds
                    for it (n_Details_Ver and the like); older versions are its
                    history.

   Section            Saved from                       Tables
   ---------------    ------------------------------   ---------------------------------------
   Upload             Upload Documents                 t_Upload_State, t_Kyc_Documents
   Details            Investor Information             t_Kyc_Dtls, t_Address_Dtls, t_Nominee_Dtls
   Payment            Bank Details & Payment           t_Payment_Bank_Dtls, t_Bank_Dtls
   Deposit            FD Configuration                 t_Investment_Dtls
   (every section)    Review Summary - submit          all of the above, as 'APR'

   f_Active   Every table has one. 0 takes a row out of use without deleting it:
              the API reads only active rows.

   Holder types, as DMS codes them: 01 the investor, 02 the second holder, 03 the
   third. Column prefixes: c_ text, n_ number, d_ date, f_ flag, j_ JSON.

   Safe to run again: each object is created only when it is not there yet.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ----- Application numbers: FBBMFL{yy}F{n} ---------------------------------- */
IF OBJECT_ID(N'dbo.s_App_No', N'SO') IS NULL
    CREATE SEQUENCE dbo.s_App_No AS BIGINT START WITH 10001 INCREMENT BY 1 NO CACHE;
GO

/* ----- t_Application_Mst ------------------------------------------------------
   One row per application: its number, whose it is, who it was opened for, and
   where it stands. This is the one row that changes - it carries the version every
   save is checked against, so two saves of one application cannot cross. What
   was entered on the application is in the detail tables, never here.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Application_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Application_Mst
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

    -- The submission, once submitted.
    d_Submitted_On       DATETIME2(3)   NULL,
    c_Sub_Status         VARCHAR(30)    NULL,       -- payment-pending, then the backend's own
    c_Link_Sent_To       VARCHAR(20)    NULL,       -- masked
    c_Link_Emailed_To    VARCHAR(150)   NULL,       -- masked
    d_Link_Valid_Until   DATETIME2(3)   NULL,
    n_Resends_Left       INT            NULL,
    c_Short_Url          VARCHAR(300)   NULL,

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Application_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,

    f_Active             BIT            NOT NULL CONSTRAINT DF_Application_Mst_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Application_Mst PRIMARY KEY CLUSTERED (n_App_Id),
    CONSTRAINT UQ_Application_Mst_App_No UNIQUE (c_App_No),
    CONSTRAINT CK_Application_Mst_Status CHECK (c_Status IN ('PEN', 'APR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Application_Mst_Partner')
    CREATE INDEX IX_Application_Mst_Partner ON dbo.t_Application_Mst (c_Partner_Id, c_Status) INCLUDE (d_Created_On, d_Updated_On);
GO

/* ----- t_Upload_State ---------------------------------------------------------
   Upload Documents as a whole, as the web app saved it: the choices, every
   check's reading and the attempt log, as JSON. It holds no file and no Aadhaar
   number. t_Kyc_Documents carries its documents a row each.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Upload_State', N'U') IS NULL
CREATE TABLE dbo.t_Upload_State
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,
    j_Upload             NVARCHAR(MAX)  NOT NULL,
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Upload_State_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Upload_State_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Upload_State PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Upload_State_Status CHECK (c_Status IN ('PEN', 'APR')),
    CONSTRAINT CK_Upload_State_Json CHECK (ISJSON(j_Upload) = 1)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Upload_State_App')
    CREATE INDEX IX_Upload_State_App ON dbo.t_Upload_State (c_App_No, n_App_Version);
GO

/* ----- t_Kyc_Documents: one row per application, holder and document ----------
   Every document on the application as it stands after the save: the copy filed
   with DMS, and what its checks said. A document not uploaded has no row.
     c_Holder_Type   00 the application's own, 01 the investor, 02 and 03 the joint holders
     c_Doc_Type      the slot the web app filed it in: pan, photo, poa (proof of
                     address), mail (communication address proof) for a holder; form,
                     payment (the cheque or DD), empproof (employee proof), tdsform
                     (Form 121) for the application
     c_Doc_Sub_Type  which it is: the proof (Aadhaar, Passport, ...), the payment
                     mode, the employee proof, or the application type for the form
     c_File_Path     where the copy is kept, relative to the document store's root
                     (Dms:Root): {appNo}/{holder}/{slot}{extension}
     c_Result        ok, warn or bad
     f_On_Record     1 when it came over from the folio, with no copy here
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Kyc_Documents', N'U') IS NULL
CREATE TABLE dbo.t_Kyc_Documents
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,
    c_Holder_Type        CHAR(2)        NOT NULL,
    c_Doc_Type           VARCHAR(10)    NOT NULL,
    c_Doc_Sub_Type       VARCHAR(30)    NOT NULL CONSTRAINT DF_Kyc_Documents_Sub_Type DEFAULT (''),
    c_File_Name          NVARCHAR(260)  NOT NULL,   -- as it was uploaded
    c_File_Path          NVARCHAR(400)  NULL,       -- where the copy is kept; NULL for one on record with no copy here
    n_File_Size          BIGINT         NOT NULL CONSTRAINT DF_Kyc_Documents_Size DEFAULT (0),
    c_Content_Type       VARCHAR(100)   NOT NULL CONSTRAINT DF_Kyc_Documents_Type DEFAULT (''),
    c_Check              NVARCHAR(500)  NOT NULL CONSTRAINT DF_Kyc_Documents_Check DEFAULT (''),
    c_Result             VARCHAR(10)    NOT NULL CONSTRAINT DF_Kyc_Documents_Result DEFAULT (''),
    f_On_Record          BIT            NOT NULL CONSTRAINT DF_Kyc_Documents_On_Record DEFAULT (0),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Kyc_Documents_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Kyc_Documents_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Kyc_Documents PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Kyc_Documents_Status CHECK (c_Status IN ('PEN', 'APR')),
    CONSTRAINT CK_Kyc_Documents_Holder CHECK (c_Holder_Type IN ('00', '01', '02', '03'))
);
GO
-- A table created before c_File_Path was added takes it here.
IF COL_LENGTH(N'dbo.t_Kyc_Documents', N'c_File_Path') IS NULL
    ALTER TABLE dbo.t_Kyc_Documents ADD c_File_Path NVARCHAR(400) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kyc_Documents_App')
    CREATE INDEX IX_Kyc_Documents_App ON dbo.t_Kyc_Documents (c_App_No, n_App_Version, c_Holder_Type, c_Doc_Type);
GO

/* ----- t_Kyc_Dtls: one row per holder -----------------------------------------
   Who each holder is - from Investor Identification, or the joint holder's own
   search - what Investor Information took down about them, and where their KYC
   stands on Upload Documents (NSDL, CKYC).
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Kyc_Dtls', N'U') IS NULL
CREATE TABLE dbo.t_Kyc_Dtls
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,
    c_Holder_Type        CHAR(2)        NOT NULL,

    c_Pan                VARCHAR(10)    NOT NULL,
    d_Dob                DATE           NULL,
    c_Name               NVARCHAR(150)  NOT NULL,
    c_Folio              VARCHAR(20)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Folio DEFAULT (''),

    c_Gender             VARCHAR(20)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Gender DEFAULT (''),
    c_Name_Type          VARCHAR(20)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Name_Type DEFAULT (''),   -- father's / spouse's
    c_Parent_Name        NVARCHAR(150)  NOT NULL CONSTRAINT DF_Kyc_Dtls_Parent DEFAULT (''),
    c_Annual_Income      VARCHAR(50)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Income DEFAULT (''),
    c_Occupation         VARCHAR(50)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Occupation DEFAULT (''),
    c_Sub_Occupation     VARCHAR(50)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Sub_Occ DEFAULT (''),
    c_Marital_Status     VARCHAR(20)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Marital DEFAULT (''),
    c_Mobile             VARCHAR(15)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Mobile DEFAULT (''),
    c_Email              VARCHAR(150)   NOT NULL CONSTRAINT DF_Kyc_Dtls_Email DEFAULT (''),
    f_Fatca_Tax_Res      BIT            NOT NULL CONSTRAINT DF_Kyc_Dtls_Fatca_Tax DEFAULT (0),   -- tax resident elsewhere
    f_Fatca_Perm_Res     BIT            NOT NULL CONSTRAINT DF_Kyc_Dtls_Fatca_Perm DEFAULT (0),
    c_Pep                VARCHAR(3)     NOT NULL CONSTRAINT DF_Kyc_Dtls_Pep DEFAULT (''),        -- yes, no, '' unanswered
    c_Pep_Related        VARCHAR(3)     NOT NULL CONSTRAINT DF_Kyc_Dtls_Pep_Rel DEFAULT (''),

    -- From Upload Documents, as it stood when the row was written.
    c_Nsdl_Status        VARCHAR(10)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Nsdl DEFAULT (''),   -- '', verified, name, failed
    c_Nsdl_Name          NVARCHAR(150)  NOT NULL CONSTRAINT DF_Kyc_Dtls_Nsdl_Name DEFAULT (''),
    f_Ckyc               BIT            NOT NULL CONSTRAINT DF_Kyc_Dtls_Ckyc DEFAULT (0),   -- KYC from CERSAI (the investor only)
    f_Mail_Different     BIT            NOT NULL CONSTRAINT DF_Kyc_Dtls_Mail_Diff DEFAULT (0),   -- post goes to another address

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Kyc_Dtls_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Kyc_Dtls_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Kyc_Dtls PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Kyc_Dtls_Status CHECK (c_Status IN ('PEN', 'APR')),
    CONSTRAINT CK_Kyc_Dtls_Holder CHECK (c_Holder_Type IN ('01', '02', '03'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kyc_Dtls_App')
    CREATE INDEX IX_Kyc_Dtls_App ON dbo.t_Kyc_Dtls (c_App_No, n_App_Version, c_Holder_Type);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kyc_Dtls_Pan')
    CREATE INDEX IX_Kyc_Dtls_Pan ON dbo.t_Kyc_Dtls (c_Pan) INCLUDE (c_App_No, c_Status);
GO

/* ----- t_Address_Dtls: one row per holder and address type --------------------
     c_Addr_Type   PER - permanent, as on record for the holder
                   COR - communication, typed on Investor Information when post
                         goes elsewhere; no row when it goes to the permanent one
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Address_Dtls', N'U') IS NULL
CREATE TABLE dbo.t_Address_Dtls
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,
    c_Holder_Type        CHAR(2)        NOT NULL,
    c_Addr_Type          CHAR(3)        NOT NULL,

    c_Line1              NVARCHAR(500)  NOT NULL CONSTRAINT DF_Address_Dtls_Line1 DEFAULT (''),
    c_Line2              NVARCHAR(250)  NOT NULL CONSTRAINT DF_Address_Dtls_Line2 DEFAULT (''),
    c_Line3              NVARCHAR(250)  NOT NULL CONSTRAINT DF_Address_Dtls_Line3 DEFAULT (''),
    c_City               NVARCHAR(100)  NOT NULL CONSTRAINT DF_Address_Dtls_City DEFAULT (''),
    c_Pin_Code           VARCHAR(6)     NOT NULL CONSTRAINT DF_Address_Dtls_Pin DEFAULT (''),
    c_District           NVARCHAR(100)  NOT NULL CONSTRAINT DF_Address_Dtls_District DEFAULT (''),
    c_State              NVARCHAR(100)  NOT NULL CONSTRAINT DF_Address_Dtls_State DEFAULT (''),

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Address_Dtls_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Address_Dtls_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Address_Dtls PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Address_Dtls_Status CHECK (c_Status IN ('PEN', 'APR')),
    CONSTRAINT CK_Address_Dtls_Holder CHECK (c_Holder_Type IN ('01', '02', '03')),
    CONSTRAINT CK_Address_Dtls_Type CHECK (c_Addr_Type IN ('PER', 'COR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Address_Dtls_App')
    CREATE INDEX IX_Address_Dtls_App ON dbo.t_Address_Dtls (c_App_No, n_App_Version, c_Holder_Type, c_Addr_Type);
GO

/* ----- t_Nominee_Dtls ---------------------------------------------------------
   The nominee, with a guardian for one under the minimum age. No row when no
   nominee is named.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Nominee_Dtls', N'U') IS NULL
CREATE TABLE dbo.t_Nominee_Dtls
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,

    c_Name               NVARCHAR(150)  NOT NULL CONSTRAINT DF_Nominee_Dtls_Name DEFAULT (''),
    d_Dob                DATE           NULL,
    c_Relation           VARCHAR(30)    NOT NULL CONSTRAINT DF_Nominee_Dtls_Relation DEFAULT (''),
    c_Guardian_Name      NVARCHAR(150)  NOT NULL CONSTRAINT DF_Nominee_Dtls_G_Name DEFAULT (''),
    c_Guardian_Line1     NVARCHAR(250)  NOT NULL CONSTRAINT DF_Nominee_Dtls_G_Line1 DEFAULT (''),
    c_Guardian_Line2     NVARCHAR(250)  NOT NULL CONSTRAINT DF_Nominee_Dtls_G_Line2 DEFAULT (''),
    c_Guardian_Line3     NVARCHAR(250)  NOT NULL CONSTRAINT DF_Nominee_Dtls_G_Line3 DEFAULT (''),
    c_Guardian_Pin_Code  VARCHAR(6)     NOT NULL CONSTRAINT DF_Nominee_Dtls_G_Pin DEFAULT (''),
    c_Guardian_City      NVARCHAR(100)  NOT NULL CONSTRAINT DF_Nominee_Dtls_G_City DEFAULT (''),

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Nominee_Dtls_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Nominee_Dtls_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Nominee_Dtls PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Nominee_Dtls_Status CHECK (c_Status IN ('PEN', 'APR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Nominee_Dtls_App')
    CREATE INDEX IX_Nominee_Dtls_App ON dbo.t_Nominee_Dtls (c_App_No, n_App_Version);
GO

/* ----- t_Payment_Bank_Dtls ----------------------------------------------------
   The account the deposit is paid from, how it is paid, and the cheque or DD.
   One row per save of Bank Details & Payment.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Payment_Bank_Dtls', N'U') IS NULL
CREATE TABLE dbo.t_Payment_Bank_Dtls
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,

    c_Pay_Mode           VARCHAR(30)    NOT NULL CONSTRAINT DF_Payment_Bank_Mode DEFAULT (''),   -- Cheque, DD, Net banking, ...
    c_Ifsc               VARCHAR(11)    NULL,       -- NULL when no payment account is given
    c_Account_No         VARCHAR(30)    NULL,
    c_Bank_Name          NVARCHAR(100)  NULL,       -- as GET ifsc/{code} named the branch at save
    c_Branch_Name        NVARCHAR(150)  NULL,
    c_Micr               VARCHAR(9)     NULL,

    c_Cheque_No          VARCHAR(10)    NULL,       -- NULL when not paid by cheque
    d_Cheque_Date        DATE           NULL,
    c_Cms_Location       VARCHAR(50)    NULL,

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Payment_Bank_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Payment_Bank_Dtls_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Payment_Bank_Dtls PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Payment_Bank_Dtls_Status CHECK (c_Status IN ('PEN', 'APR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payment_Bank_Dtls_App')
    CREATE INDEX IX_Payment_Bank_Dtls_App ON dbo.t_Payment_Bank_Dtls (c_App_No, n_App_Version);
GO

/* ----- t_Bank_Dtls: the repayment account -------------------------------------
   Where interest and the maturity amount are paid. f_Same_As_Payment set, it is
   the payment account.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Bank_Dtls', N'U') IS NULL
CREATE TABLE dbo.t_Bank_Dtls
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,

    f_Same_As_Payment    BIT            NOT NULL CONSTRAINT DF_Bank_Dtls_Same DEFAULT (0),
    c_Ifsc               VARCHAR(11)    NULL,
    c_Account_No         VARCHAR(30)    NULL,
    c_Bank_Name          NVARCHAR(100)  NULL,
    c_Branch_Name        NVARCHAR(150)  NULL,
    c_Micr               VARCHAR(9)     NULL,

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Bank_Dtls_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Bank_Dtls_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Bank_Dtls PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Bank_Dtls_Status CHECK (c_Status IN ('PEN', 'APR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bank_Dtls_App')
    CREATE INDEX IX_Bank_Dtls_App ON dbo.t_Bank_Dtls (c_App_No, n_App_Version);
GO

/* ----- t_Investment_Dtls: the deposit as configured ---------------------------
   FD Configuration, and with it the application's other details chosen on Upload
   Documents: the application type and form, the deposit category, how it is
   sourced (broker, staff, sub-broker), the employee details for an employee
   deposit, and the deposit a renewal renews. The quote - rate, interest, maturity
   amount and date - is locked on submit, so it is set on the 'APR' row only.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Investment_Dtls', N'U') IS NULL
CREATE TABLE dbo.t_Investment_Dtls
(
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    n_App_Version        INT            NOT NULL,
    c_Status             CHAR(3)        NOT NULL,

    n_Amount             BIGINT         NOT NULL,   -- rupees
    n_Tenure_Months      INT            NOT NULL,
    c_Payout             VARCHAR(20)    NOT NULL,   -- maturity, monthly, quarterly, halfyearly, yearly
    f_Auto_Renewal       BIT            NOT NULL CONSTRAINT DF_Investment_Auto DEFAULT (0),
    c_Renew_Instruction  VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Renew DEFAULT (''),
    f_No_Tds             BIT            NOT NULL CONSTRAINT DF_Investment_No_Tds DEFAULT (0),   -- Form 121 filed
    c_Delivery_Type      VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Delivery DEFAULT (''),

    c_App_Type           VARCHAR(10)    NOT NULL CONSTRAINT DF_Investment_App_Type DEFAULT (''),   -- DIGITAL, PHYSICAL
    c_Form_No            VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Form_No DEFAULT (''),
    c_Category           VARCHAR(30)    NOT NULL CONSTRAINT DF_Investment_Category DEFAULT (''),
    c_Sourcing           VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Sourcing DEFAULT (''),
    c_Source_Code        VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Source DEFAULT (''),
    c_Sub_Broker         VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Sub_Broker DEFAULT (''),
    c_Emp_Code           VARCHAR(20)    NOT NULL CONSTRAINT DF_Investment_Emp_Code DEFAULT (''),
    c_Emp_Company        NVARCHAR(100)  NOT NULL CONSTRAINT DF_Investment_Emp_Company DEFAULT (''),
    c_Emp_Holder         VARCHAR(30)    NOT NULL CONSTRAINT DF_Investment_Emp_Holder DEFAULT (''),
    c_Emp_Relation       VARCHAR(30)    NOT NULL CONSTRAINT DF_Investment_Emp_Relation DEFAULT (''),
    c_Emp_Proof_Type     VARCHAR(30)    NOT NULL CONSTRAINT DF_Investment_Emp_Proof DEFAULT (''),
    c_Renew_Dep_No       VARCHAR(20)    NULL,       -- the deposit a renewal renews

    -- The quote, locked on submit (POST deposits/quote at the moment of submitting).
    n_Rate               DECIMAL(5,2)   NULL,       -- card rate, % a year
    n_Interest_Each      DECIMAL(18,2)  NULL,       -- interest each payout; 0 for a cumulative deposit
    n_Maturity_Amount    DECIMAL(18,2)  NULL,
    d_Matures_On         DATE           NULL,
    d_Rate_As_On         DATE           NULL,       -- the card the rate was read off

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Investment_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Investment_Dtls_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Investment_Dtls PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT CK_Investment_Dtls_Status CHECK (c_Status IN ('PEN', 'APR'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Investment_Dtls_App')
    CREATE INDEX IX_Investment_Dtls_App ON dbo.t_Investment_Dtls (c_App_No, n_App_Version);
GO

/* ----- t_Page_State -----------------------------------------------------------
   A wizard page's working state - typed but not yet saved, a joint holder still
   being searched for - kept so a page opens where it was left. Scratch, not a
   record: it is overwritten in place and moves no version.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Page_State', N'U') IS NULL
CREATE TABLE dbo.t_Page_State
(
    c_App_No             VARCHAR(20)    NOT NULL,
    c_Page               VARCHAR(30)    NOT NULL,
    j_State              NVARCHAR(MAX)  NOT NULL,
    d_Updated_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Page_State_Updated DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Page_State_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Page_State PRIMARY KEY CLUSTERED (c_App_No, c_Page)
);
GO
