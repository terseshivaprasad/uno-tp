/* =============================================================================
   Uno TP - the masters the purchase journey reads, as the app reads them (SQL
   Server 2016 or later): investor folios, brokers, staff, bank branches by IFSC,
   PIN codes, and the FD system's own masters: its document master, and the marital
   status, relation, occupation and source of funds masters, with its source of funds
   log, which is kept beside them. Its rate card (t_FD_BOTC_SCHEME) is in the main
   database: create_existing_tables.sql. The query that reads each master is in
   UnoTP.Data/MasterQueries.cs.

       sqlcmd -d UnoTP -i db/create_master_tables.sql

   On the database that already has the masters this script does nothing. Each
   master is loaded from its source of record, never from a script here: folios
   from the FD system, brokers and staff from their masters, IFSC from the RBI's
   list, PIN codes from India Post's directory.

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

/* ----- t_Unotp_Investor_Folio -------------------------------------------------------
   One row per folio. A PAN held on more than one folio is a record Operations
   has to merge; Investor Identification refuses it by PAN until they do.
     d_Dob        NULL when the register holds none: refused until it is updated
     f_Doc_*      the documents the folio already holds, not asked for again
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Investor_Folio', N'U') IS NULL
BEGIN
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
    c_Source             VARCHAR(50)    NULL,       -- where the folio's KYC came from, as the FD system says: written to f_Data_Source
    f_Active             BIT            NOT NULL CONSTRAINT DF_Investor_Folio_Active DEFAULT (1),
    c_Created_By         VARCHAR(20)    NOT NULL,
    d_Created_On         DATETIME2(3)   NOT NULL CONSTRAINT DF_Investor_Folio_Created DEFAULT (SYSDATETIME()),
    c_Updated_By         VARCHAR(20)    NULL,
    d_Updated_On         DATETIME2(3)   NULL,
    CONSTRAINT PK_Investor_Folio PRIMARY KEY CLUSTERED (c_Folio)
);
CREATE INDEX IX_Investor_Folio_Pan ON dbo.t_Unotp_Investor_Folio (c_Pan) INCLUDE (f_Active);
END
GO

/* ----- t_Unotp_Broker_Mst, t_Unotp_Staff_Mst ---------------------------------------------
   The registers a sourcing code is searched on as it is typed. The staff
   register includes the partners who are employees, so one can code an
   application to themselves.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Broker_Mst', N'U') IS NULL
BEGIN
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
END
GO

-- t_Unotp_Staff_Mst: Employees an employee-sourced application is coded to.
IF OBJECT_ID(N'dbo.t_Unotp_Staff_Mst', N'U') IS NULL
BEGIN
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
END
GO

/* ----- t_Unotp_Ifsc_Mst ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.t_Unotp_Ifsc_Mst', N'U') IS NULL
BEGIN
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
CREATE INDEX IX_Ifsc_Mst_Micr ON dbo.t_Unotp_Ifsc_Mst (c_Micr);
END
GO

/* ----- t_Unotp_Pincode_Mst ---------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_Unotp_Pincode_Mst', N'U') IS NULL
BEGIN
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
END
GO

/* ----- t_FD_CMN_AML_Source_Of_Funds_Log -----------------------------------------------
   The FD system's log of the source of funds an investor gave, with the amount,
   annual income and occupation it was asked for. It is kept with the masters, so
   the app writes it on the masters connection.
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

/* ----- The FD system's document master -------------------------------------------------
   T_FD_CMN_KYC_Document_Type_Mst and T_FD_CMN_KYC_Document_Sub_Type_Mst: the types a document
   can be, and under each its sub-types, by depositor status ('IND' for an
   individual). Here with the columns the app reads and those seen beside them; the
   FD system's own tables have more. Created for a database built from nothing.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.T_FD_CMN_KYC_Document_Type_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[T_FD_CMN_KYC_Document_Type_Mst](
    [f_T_FD_BT_KYC_Document_Type] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Depositor_Status_Code] [nvarchar](20) NULL,
    [f_KYC_Document_Type_Code] [nvarchar](50) NULL,
    [f_KYC_Document_Type_Desc] [nvarchar](50) NULL,
    [f_From_Date] [datetime] NULL
);
END
GO

IF OBJECT_ID(N'dbo.T_FD_CMN_KYC_Document_Sub_Type_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[T_FD_CMN_KYC_Document_Sub_Type_Mst](
    [f_Depositor_Status_Code] [nvarchar](20) NULL,
    [f_KYC_Document_Sub_Type_Code] [nvarchar](100) NULL,
    [f_KYC_Document_Sub_Type_Desc] [nvarchar](250) NULL,
    [f_KYC_Document_Type_Code] [nvarchar](50) NULL,
    [f_KYC_IsMultiple] [bit] NULL,
    [f_Is_Doc_Ref_No_Required] [bit] NULL,
    [f_Is_Doc_Exp_Date_Required] [bit] NULL,
    [f_IsActive] [bit] NULL
);
END
GO

/* ----- The FD system's masters behind four of the pages' lists ---------------------------
   Marital status, the nominee's relation, the employee's relation and the source of
   funds are offered as these list them and saved as their codes (db/021). Here with
   the columns the app reads; the FD system's own tables have more. Created for a
   database built from nothing.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Marital_Status_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Marital_Status_Mst](
    [f_Pk_t_FD_BT_Marital_Status_Mst_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_MaritalStatus_Code] [nvarchar](20) NULL,
    [f_MaritalStatus_Name] [nvarchar](50) NULL,
    [f_Active] [bit] NULL
);
END
GO

IF OBJECT_ID(N'dbo.t_FD_CMN_Relation_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_CMN_Relation_Mst](
    [f_Pk_t_FD_CMN_Relation_Mst_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Relation_Code] [nvarchar](20) NULL,
    [f_Relation_Name] [nvarchar](50) NULL,
    [f_Active] [bit] NULL
);
END
GO

IF OBJECT_ID(N'dbo.t_FD_MMFSL_Employee_Relation_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_MMFSL_Employee_Relation_Mst](
    [f_Pk_t_FD_MMFSL_Employee_Relation_Mst_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Relation_Code] [nvarchar](20) NULL,
    [f_Relation_Name] [nvarchar](50) NULL,
    [f_Active] [bit] NULL
);
END
GO

IF OBJECT_ID(N'dbo.t_FD_CMN_AML_Source_Of_Funds_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_CMN_AML_Source_Of_Funds_Mst](
    [f_Pk_t_FD_CMN_AML_Source_Of_Funds_Mst_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_AML_Source_Of_Funds_Code] [nvarchar](20) NULL,
    [f_AML_Source_Of_Funds_Desc] [nvarchar](250) NULL,
    [f_Active] [bit] NULL
);
END
GO

/* ----- t_FD_CMN_Ckyc_CustSeg_Mst: the occupation master --------------------------------
   A row is a customer segment type (the
   occupation the page offers), a sub-type under it (the sub occupation) and the
   CKYC occupation they stand for (db/022). Here with the columns the app reads.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_CMN_Ckyc_CustSeg_Mst', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_CMN_Ckyc_CustSeg_Mst](
    [f_Ckyc_CustSeg_Type_Code] [nvarchar](50) NULL,
    [f_Ckyc_CustSeg_Type_Desc] [nvarchar](250) NULL,
    [f_Ckyc_CustSeg_SubType_Code] [nvarchar](50) NULL,
    [f_Ckyc_CustSeg_SubType_Desc] [nvarchar](250) NULL,
    [f_Ckyc_Occupation_Code] [nvarchar](10) NULL,
    [f_Ckyc_Occupation_Desc] [nvarchar](50) NULL
);
END
GO
