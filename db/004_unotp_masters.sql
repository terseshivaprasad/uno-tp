/* =============================================================================
   Uno TP - the masters the purchase journey reads (SQL Server 2016 or later).
   Run after 003_unotp_seed.sql.

   t_Unotp_Investor_Folio   the investors on record, by folio: Investor Identification
   t_Unotp_Broker_Mst       brokers a broker-sourced application is coded to
   t_Unotp_Staff_Mst        employees an employee-sourced application is coded to
   t_Unotp_Ifsc_Mst         bank branches by IFSC: Bank Details & Payment
   t_Unotp_Pincode_Mst      PIN codes: the communication address on Investor Information
   t_Unotp_Rate_Card        deposit rates by category and tenure: FD Configuration

   None of these is seeded for production. Each is loaded from its source of
   record before go-live and kept in step with it: folios from the FD system,
   brokers and staff from their masters, IFSC from the RBI's list, PIN codes from
   India Post's directory, rates from the published rate card.

   Safe to run again: each object is created only when it is not there yet.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ----- t_Unotp_Investor_Folio -------------------------------------------------------
   One row per folio. A PAN held on more than one folio is a record Operations
   has to merge; Investor Identification refuses it by PAN until they do.
     d_Dob        NULL when the register holds none: refused until it is updated
     f_Doc_*      the documents the folio already holds, not asked for again
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Investor_Folio', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Investor_Folio
(
    c_Folio              VARCHAR(20)    NOT NULL,
    c_Pan                VARCHAR(10)    NOT NULL,
    d_Dob                DATE           NULL,
    c_Name               NVARCHAR(150)  NOT NULL,
    c_Gender             VARCHAR(20)    NOT NULL CONSTRAINT DF_Investor_Folio_Gender DEFAULT (''),
    c_Address            NVARCHAR(500)  NOT NULL CONSTRAINT DF_Investor_Folio_Address DEFAULT (''),
    f_Doc_Pan            BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Pan DEFAULT (0),
    f_Doc_Photo          BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Photo DEFAULT (0),
    f_Doc_Poa            BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Poa DEFAULT (0),
    c_Note               NVARCHAR(200)  NOT NULL CONSTRAINT DF_Investor_Folio_Note DEFAULT (''),   -- e.g. CKYC available with us
    f_Active             BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Investor_Folio_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Investor_Folio PRIMARY KEY CLUSTERED (c_Folio)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Investor_Folio_Pan')
    CREATE INDEX IX_Investor_Folio_Pan ON dbo.t_Unotp_Investor_Folio (c_Pan) INCLUDE (f_Active);
GO

/* ----- t_Unotp_Broker_Mst, t_Unotp_Staff_Mst ---------------------------------------------
   The registers a sourcing code is searched on as it is typed. The staff
   register includes the partners who are employees, so one can code an
   application to themselves.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Broker_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Broker_Mst
(
    c_Code               VARCHAR(20)    NOT NULL,
    c_Name               NVARCHAR(150)  NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Broker_Mst_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Broker_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Broker_Mst PRIMARY KEY CLUSTERED (c_Code)
);
GO

IF OBJECT_ID(N'dbo.t_Unotp_Staff_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Staff_Mst
(
    c_Code               VARCHAR(20)    NOT NULL,
    c_Name               NVARCHAR(150)  NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Staff_Mst_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Staff_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Staff_Mst PRIMARY KEY CLUSTERED (c_Code)
);
GO

/* ----- t_Unotp_Ifsc_Mst ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.t_Unotp_Ifsc_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Ifsc_Mst
(
    c_Ifsc               CHAR(11)       NOT NULL,
    c_Bank               NVARCHAR(100)  NOT NULL,
    c_Branch             NVARCHAR(150)  NOT NULL,
    c_Micr               VARCHAR(9)     NOT NULL CONSTRAINT DF_Ifsc_Mst_Micr DEFAULT (''),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Ifsc_Mst_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Ifsc_Mst_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Ifsc_Mst PRIMARY KEY CLUSTERED (c_Ifsc)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Ifsc_Mst_Micr')
    CREATE INDEX IX_Ifsc_Mst_Micr ON dbo.t_Unotp_Ifsc_Mst (c_Micr);
GO

/* ----- t_Unotp_Pincode_Mst ---------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Pincode_Mst', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Pincode_Mst
(
    c_Pin_Code           CHAR(6)        NOT NULL,
    c_District           NVARCHAR(100)  NOT NULL,
    c_State              NVARCHAR(100)  NOT NULL,
    f_Active             BIT            NOT NULL CONSTRAINT DF_Pincode_Mst_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Pincode_Mst_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Pincode_Mst PRIMARY KEY CLUSTERED (c_Pin_Code)
);
GO

/* ----- t_Unotp_Rate_Card -----------------------------------------------------------
   The card rate by deposit category and tenure, from the day it takes effect. A
   new card is new rows with a later d_Effective_From: the rate a deposit gets is
   the latest one in effect on the day it starts (a renewal: on its maturity date).
   A category with no row of its own takes defaultRateCategory's (t_Unotp_App_Config).
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Rate_Card', N'U') IS NULL
CREATE TABLE dbo.t_Unotp_Rate_Card
(
    n_Id                 INT IDENTITY(1,1) NOT NULL,
    c_Category           VARCHAR(30)    NOT NULL,
    n_Tenure_Months      INT            NOT NULL,
    n_Rate               DECIMAL(5,2)   NOT NULL,   -- % a year
    d_Effective_From     DATE           NOT NULL,
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Rate_Card_Created DEFAULT (SYSDATETIME()),
    f_Active             BIT            NOT NULL CONSTRAINT DF_Rate_Card_Active DEFAULT (1),   -- 0 takes the row out of use without deleting it
    CONSTRAINT PK_Rate_Card PRIMARY KEY CLUSTERED (n_Id),
    CONSTRAINT UQ_Rate_Card UNIQUE (c_Category, n_Tenure_Months, d_Effective_From),
    CONSTRAINT CK_Rate_Card_Rate CHECK (n_Rate > 0 AND n_Rate < 100)
);
GO
