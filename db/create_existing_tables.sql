/* =============================================================================
   Uno TP - the tables the database already has, as the app reads them (SQL Server
   2016 or later): what is entered on an application (KYC, addresses, documents,
   nominee, payment and repayment accounts, the deposit), the partners and their
   menus and sessions, the payment links and the pay-in slips.

       sqlcmd -d UnoTP -i db/create_existing_tables.sql

   On the database that already has these tables this script does nothing. It is
   here for a database built from nothing, and as the record of the columns the
   app's queries expect (src/UnoTP.Data).

   An application is one row in t_Unotp_Application_Mst (create_new_tables.sql), and
   everything entered on it is rows in the detail tables here. A detail row is never
   updated or deleted: every save inserts the section afresh, so each earlier save
   stays on record.

     c_Status       'PEN' - saved on a step, before the application is submitted.
                    'APR' - written once, on submit: the whole application as it was
                            submitted, in a fresh set of rows.
     n_App_Version  The application's version the rows were saved at. A section's
                    current rows are those at the version t_Unotp_Application_Mst holds
                    for it (n_Details_Ver and the like); older versions are its history.

   Holder types, as DMS codes them: 01 the investor, 02 the second holder, 03 the third.

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

/* ----- t_Unotp_Kyc_Documents: one row per application, holder and document ----------
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
IF OBJECT_ID(N'dbo.t_Unotp_Kyc_Documents', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Kyc_Documents
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Kyc_Documents_Active DEFAULT (1),
    CONSTRAINT PK_Kyc_Documents PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Kyc_Documents_App ON dbo.t_Unotp_Kyc_Documents (c_App_No, n_App_Version, c_Holder_Type, c_Doc_Type);
END
GO

/* ----- t_Unotp_Kyc_Dtls: one row per holder -----------------------------------------
   Who each holder is - from Investor Identification, or the joint holder's own
   search - what Investor Information took down about them, and where their KYC
   stands on Upload Documents (NSDL, CKYC).
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Kyc_Dtls', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Kyc_Dtls
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

    -- What name screening said of the holder.
    c_Screening_Status   VARCHAR(10)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Screening DEFAULT (''),
    c_Screening_Ref      VARCHAR(50)    NOT NULL CONSTRAINT DF_Kyc_Dtls_Screening_Ref DEFAULT (''),
    d_Screened_On        DATETIME2(3)   NULL,

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Kyc_Dtls_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Kyc_Dtls_Active DEFAULT (1),
    CONSTRAINT PK_Kyc_Dtls PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Kyc_Dtls_App ON dbo.t_Unotp_Kyc_Dtls (c_App_No, n_App_Version, c_Holder_Type);
CREATE INDEX IX_Kyc_Dtls_Pan ON dbo.t_Unotp_Kyc_Dtls (c_Pan) INCLUDE (c_App_No, c_Status);
END
GO

/* ----- t_Unotp_Address_Dtls: one row per holder and address type --------------------
     c_Addr_Type   PER - permanent, as on record for the holder
                   COR - communication, typed on Investor Information when post
                         goes elsewhere; no row when it goes to the permanent one
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Address_Dtls', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Address_Dtls
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Address_Dtls_Active DEFAULT (1),
    CONSTRAINT PK_Address_Dtls PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Address_Dtls_App ON dbo.t_Unotp_Address_Dtls (c_App_No, n_App_Version, c_Holder_Type, c_Addr_Type);
END
GO

/* ----- t_Unotp_Nominee_Dtls ---------------------------------------------------------
   The nominee, with a guardian for one under the minimum age. No row when no
   nominee is named.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Nominee_Dtls', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Nominee_Dtls
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Nominee_Dtls_Active DEFAULT (1),
    CONSTRAINT PK_Nominee_Dtls PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Nominee_Dtls_App ON dbo.t_Unotp_Nominee_Dtls (c_App_No, n_App_Version);
END
GO

/* ----- t_Unotp_Payment_Bank_Dtls ----------------------------------------------------
   The account the deposit is paid from, how it is paid, and the cheque or DD.
   One row per save of Bank Details & Payment.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Payment_Bank_Dtls', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Payment_Bank_Dtls
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Payment_Bank_Dtls_Active DEFAULT (1),
    CONSTRAINT PK_Payment_Bank_Dtls PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Payment_Bank_Dtls_App ON dbo.t_Unotp_Payment_Bank_Dtls (c_App_No, n_App_Version);
END
GO

/* ----- t_Unotp_Bank_Dtls: the repayment account -------------------------------------
   Where interest and the maturity amount are paid. f_Same_As_Payment set, it is
   the payment account.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Bank_Dtls', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Bank_Dtls
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Bank_Dtls_Active DEFAULT (1),
    CONSTRAINT PK_Bank_Dtls PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Bank_Dtls_App ON dbo.t_Unotp_Bank_Dtls (c_App_No, n_App_Version);
END
GO

/* ----- t_Unotp_Investment_Dtls: the deposit as configured ---------------------------
   FD Configuration, and with it the application's other details chosen on Upload
   Documents: the application type and form, the deposit category, how it is
   sourced (broker, staff, sub-broker), the employee details for an employee
   deposit, and the deposit a renewal renews. The quote - rate, interest, maturity
   amount and date - is locked on submit, so it is set on the 'APR' row only.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Investment_Dtls', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Investment_Dtls
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

    c_Source_Of_Funds    VARCHAR(30)    NOT NULL CONSTRAINT DF_Investment_Source_Of_Funds DEFAULT (''),
    c_Source_Of_Funds_Remark NVARCHAR(200) NOT NULL CONSTRAINT DF_Investment_Source_Of_Funds_Remark DEFAULT (''),

    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Investment_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Investment_Dtls_Active DEFAULT (1),
    CONSTRAINT PK_Investment_Dtls PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Investment_Dtls_App ON dbo.t_Unotp_Investment_Dtls (c_App_No, n_App_Version);
END
GO

/* ----- t_Unotp_Partner_Mst ---------------------------------------------------------
   A partner who may sign in: the user id the portal sends, and how they source.
     c_Agency_Type   the sourcingAgency in t_Unotp_App_Config sources as the house;
                     any other type sources as a broker under c_Broker_Code
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Partner_Mst', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Partner_Mst
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
END
GO

-- t_Unotp_Partner_Menu: The features each partner's menu opens.
IF OBJECT_ID(N'dbo.t_Unotp_Partner_Menu', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Partner_Menu
(
    c_User_Id            VARCHAR(20)    NOT NULL,
    c_Feature_Key        VARCHAR(20)    NOT NULL,
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Partner_Menu_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Partner_Menu_Active DEFAULT (1),
    CONSTRAINT PK_Partner_Menu PRIMARY KEY CLUSTERED (c_User_Id, c_Feature_Key)
);
END
GO

/* ----- t_Unotp_User_Session --------------------------------------------------------
   Started on entry from the portal; every call after carries it (X-Session-Id)
   and is refused with 401 once it has expired or ended.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_User_Session', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_User_Session
(
    c_Session_Id         CHAR(32)       NOT NULL,
    c_User_Id            VARCHAR(20)    NOT NULL,
    c_Sys_Code           VARCHAR(20)    NOT NULL,
    d_Started_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_User_Session_Started DEFAULT (SYSDATETIME()),
    d_Expires_On         DATETIME2(3)   NOT NULL,
    d_Ended_On           DATETIME2(3)   NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_User_Session_Active DEFAULT (1),
    CONSTRAINT PK_User_Session PRIMARY KEY CLUSTERED (c_Session_Id)
);
CREATE INDEX IX_User_Session_User ON dbo.t_Unotp_User_Session (c_User_Id, d_Expires_On);
END
GO

/* ----- t_Unotp_Payment_Link --------------------------------------------------------
   Every link sent to an investor, by SMS and e-mail both: on submit, on a resend,
   and from Short URL. Never updated: the latest for an application and purpose
   is the live one, and every one before it has stopped working.
     c_Purpose   payment, acceptance
     c_Mobile, c_Email   where it went, masked as the lists show them
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Payment_Link', N'U') IS NULL
BEGIN
CREATE TABLE dbo.t_Unotp_Payment_Link
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
    f_Active             BIT            NOT NULL CONSTRAINT DF_Payment_Link_Active DEFAULT (1),
    CONSTRAINT PK_Payment_Link PRIMARY KEY CLUSTERED (n_Id)
);
CREATE INDEX IX_Payment_Link_App ON dbo.t_Unotp_Payment_Link (c_App_No, c_Purpose, n_Id);
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
    n_Id                 BIGINT IDENTITY(1,1) NOT NULL,
    c_App_No             VARCHAR(20)    NOT NULL,
    c_Slip_No            VARCHAR(20)    NOT NULL,
    d_Generated_On       DATETIME2(3)   NOT NULL CONSTRAINT DF_Pay_In_Slip_Generated DEFAULT (SYSDATETIME()),
    c_Generated_By       VARCHAR(20)    NOT NULL,
    d_Deposited_On       DATETIME2(3)   NULL,   -- paid in at the bank, from the bank's feed
    f_Active             BIT            NOT NULL CONSTRAINT DF_Pay_In_Slip_Active DEFAULT (1),
    CONSTRAINT PK_Pay_In_Slip PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Pay_In_Slip_No UNIQUE (c_Slip_No)
);
CREATE INDEX IX_Pay_In_Slip_App ON dbo.t_Unotp_Pay_In_Slip (c_App_No, n_Id);
END
GO
