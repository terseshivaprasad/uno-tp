/* =============================================================================
   Uno TP - the tables the database already has, as the app reads them (SQL Server
   2016 or later): what is entered on an application (KYC, addresses, documents,
   nominee, payment and repayment accounts, the deposit), the partners, the payment
   links and the pay-in slips.

       sqlcmd -d UnoTP -i db/create_existing_tables.sql

   On the database that already has these tables this script does nothing. It is
   here for a database built from nothing, and as the record of the columns the
   app's queries expect (UnoTP.Data).

   An application is one row in t_Unotp_Application_Mst (create_new_tables.sql), and
   everything entered on it is rows in the FD system's detail tables here. A detail
   row is never deleted: a save takes the section's rows out of use (f_Active = 0)
   and inserts the section afresh, so each earlier save stays on record.

     f_Status       'PEN' - saved on a step, before the application is submitted.
                    'APR' - written once, on submit: the whole application as it was
                            submitted, in a fresh set of rows.

   Holder types, as DMS codes them: 01 the investor, 02 the second holder, 03 the third.

   A table is created, with its indexes, only when it is not there yet. A table
   already there is not touched at all: no column, index or row of it changes,
   whatever its columns are. Schema only: no rows are put in. Safe to run again.

   The app's own tables here (partners, links, slips) use the prefixes
   c_ text, n_ number, d_ date, f_ flag, j_ JSON, and have f_Active: 0 takes a row
   out of use without deleting it. The FD system's tables keep its own names. No
   table has a foreign key or a CHECK constraint: the app keeps those rules itself.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ----- The FD system's own tables, column for column as the database has them -----
   What is entered on an application goes into these. None has a version column, a
   key or an index: a section's current rows are the application's active ones
   (f_Active = 1), and a save takes the rows it replaces out of use before it
   inserts the section afresh. Every row carries f_Source = 'UNO_TP', f_Status
   ('PEN' on a step's save, 'APR' on submit), who wrote it (the user's
   Agency_Usr_Clustered_ID), from which session and address.

   The lists' names go in the description columns (f_..._Desc); the FD system's
   codes beside them stay empty until the lists are read off its masters.
   ----------------------------------------------------------------------------- */

/* ----- t_FD_BT_KYC_document: one row per application, holder and document ----------
     f_Holder_Type_Code   01 the investor, 02 and 03 the joint holders. The application's
                          own documents - the form, the cheque, an employee proof, the
                          Form 121 - go under 01
     f_Doc_Type_Code, f_Doc_Sub_Type_Code, and their descriptions
                          as the FD system's document master codes the document
                          (T_FD_CMN_KYC_Document_Sub_Type_Mst); which sub-type each is,
                          the 'documentSubTypes' list in t_Unotp_Ref_List says
     f_Doc_Filepath       where the copy is kept, relative to the document store's
                          root (Dms:Root); NULL for one on the folio with no copy here
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
                     CKYC, fetched from CERSAI; for a holder on a folio, the source
                     the folio's record names
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

     CATEGORY        the rate card's category (rateCategory in the categories list)
     MODE_STATUS     AF a fresh application, R a renewal
     SCHEME, INTEREST_FREQ   what a payout is (scheme and interestFreq in the payouts list)
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
     f_Category      the category as the rate card names it (rateCategory in the categories list)
     f_Scheme, f_Scheme_Code, f_Int_Rate   the SCHEME, SCHEME_CODE and INTEREST_RATES of the rate card's row for the deposit
     f_Int_Freq      the payout as the rate card names it (interestFreq in the payouts list)
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
