/* =============================================================================
   Uno TP - the transaction tables (SQL Server 2016 or later).

   These are the FD system's own tables, written here column for column from the
   schemas it gave: what is entered on an application (documents, KYC, addresses,
   nominee, payment and repayment accounts, the deposit), the rate card, the staging table a
   cheque waits in for its pay-in slip and the slip's own two tables, the short
   links sent to investors (a purchase's, and a renewal's), and the source of
   funds log.

   The database these come from already has them, so there this script does
   nothing: a table is created only when it is not there yet, and one already
   there is not touched at all - no column, index or row of it changes. It is for
   a new or a development database. Schema only: no rows are put in. Safe to run
   again.

       sqlcmd -d UnoTP -i db/create_transaction_tables.sql
       sqlcmd -d UnoTP -i db/alter_transaction_tables.sql   (the columns the app adds to these tables)
       sqlcmd -d UnoTP -i db/create_new_tables.sql    (the tables that are new with the app)
       sqlcmd -d UnoTP -i db/insert_seed.sql          (the settings, features and lists)

   The three scripts may be run in any order of the first two; the seed goes last.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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
     Unlike the other tables here, a save writes only what changed: a document whose
     row already says the same is left as it is; one that is new or changed (another
     copy, a check's answer, the status on submit) has its old row taken out of use
     and a new one inserted; one taken off the application has its row taken out of
     use. So a row with f_Active = 0 is always an earlier state, never a copy.
     f_Holder_Type_Code   01 the investor, 02 and 03 the joint holders. The application's
                          own documents - the form, the cheque, an employee proof, the
                          Form 121 - go under 01
     f_Doc_Type_Code, f_Doc_Sub_Type_Code, and their descriptions
                          how the document is coded: in t_Unotp_Ref_List the
                          'filedDocuments' list gives each document its sub-type,
                          'documentSubTypes' the sub-type's name and its type, and
                          'documentTypes' the type's name
     f_Doc_FileName       the name the copy is filed under:
                          {Folio_}{ApplNo}_{HolderType}_{DocSubType}_{yyyyMMddHHmmssfff}.ext,
                          the folio only for a holder on one
     f_Doc_Filepath       where the copy is kept, in full: the document store's root
                          (Dms:Root), the application's folder, the file name; NULL for
                          one on record with no copy here. A copy uploaded again is a
                          new file: the earlier rows keep pointing at their own
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
   The gender is kept as the name prefix and read back off it: a code of the
   salutation master, MR, MRS (a married woman) or MS (any other woman, and a
   transgender holder, for whom the master has no code).
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
     f_AddType_Code   PER  - permanent: the address read off the proof of address filed
                             on the application, or the one on record for a holder on
                             a folio who filed none; always written, for its row
                             carries the holder's mobile and e-mail. The address is
                             broken across f_Add1 to f_Add3 at its spaces, and its
                             PIN code goes in f_AddPin
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

/* ----- t_FD_BOTC_SCHEME: the rate card -------------------------------------------------
   The FD system's own rate card: one row per scheme code. Here with the columns the
   app reads and those seen beside them; the column types are the app's reading of
   them, not the FD system's own script, and its table may have more columns.

     CATEGORY        the rate card's category (a code of the 'categories' list)
     MODE_STATUS     AF a fresh application, R a renewal
     SCHEME, INTEREST_FREQ   what a payout is (a code of the 'payouts' list, and its scheme)
     PERIOD          the tenure, months
     MINIMUM_AMOUNT, MAXIMUM_AMOUNT   the deposits the row is for, rupees, both ends included
     FROM_DATE, TO_DATE      a row is in effect from FROM_DATE while it has no TO_DATE; one with a TO_DATE is closed and not read
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

/* ----- t_FD_BT_Payinslip_Staging_Hdr: cheques waiting for a pay-in slip ---------------
   On the final submit of an application paid by cheque, once it stands as APR, one
   row goes in here with f_Queue_Status = 'PEN'. The app fills the application
   number, the applicant's name, the agency code, the cheque (number, date as
   dd-MM-yyyy, amount in whole rupees), the bank it is drawn on, the Axis CMS
   location, who wrote the row and when; every other column is left empty.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Payinslip_Staging_Hdr', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Payinslip_Staging_Hdr](
    [f_Pk_t_BT_Payinslip_Staging_Hdr_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Payinslip_Stg_Hdr_Abr] [nvarchar](20) NULL,
    [f_Payinslip_Stg_Hdr_Seq] [nvarchar](50) NULL,
    [f_Payinslip_Stg_HdrSequence] [nvarchar](50) NULL,
    [f_Payinslip_SourceName] [nvarchar](150) NULL,
    [f_PortalSourceCode] [nvarchar](50) NULL,
    [f_MCP_Code] [nvarchar](50) NULL,
    [f_SCP_Code] [nvarchar](50) NULL,
    [f_MCP_Broker_Code] [nvarchar](50) NULL,
    [f_SCP_Broker_Code] [nvarchar](50) NULL,
    [f_CP_Trans_Ref_No] [nvarchar](50) NULL,
    [f_Employee_Code] [nvarchar](50) NULL,
    [f_ADO_Indicator] [nvarchar](10) NULL,
    [f_MF_Sys_Ref_no] [nvarchar](50) NULL,
    [f_Ref_Cust_Code] [nvarchar](50) NULL,
    [f_Ref_Rm_Code] [nvarchar](50) NULL,
    [f_Ref_Othr_Code] [nvarchar](50) NULL,
    [f_Ref_Type] [nvarchar](50) NULL,
    [f_CMS_Type] [nvarchar](50) NULL,
    [f_CMS_Bank_Name] [nvarchar](150) NULL,
    [f_CMS_Branch_Name] [nvarchar](150) NULL,
    [f_CMS_Bank_IFSC_Code] [nvarchar](50) NULL,
    [f_CMS_Bank_MICR_Code] [nvarchar](50) NULL,
    [f_MF_Branch_Code] [nvarchar](50) NULL,
    [f_MF_Branch_Name] [nvarchar](50) NULL,
    [f_Cheque_Number] [nvarchar](50) NULL,
    [f_Cheque_Date] [nvarchar](50) NULL,
    [f_Cheque_Amt] [nvarchar](50) NULL,
    [f_Drawn_Bank_Name] [nvarchar](150) NULL,
    [f_Drawn_Branch_Name] [nvarchar](150) NULL,
    [f_Applicant_Name] [nvarchar](250) NULL,
    [f_Application_Number] [nvarchar](50) NULL,
    [f_SCP_Location_Code] [nvarchar](50) NULL,
    [f_SCP_Location_Name] [nvarchar](50) NULL,
    [f_Entry_Date] [nvarchar](50) NULL,
    [f_Pickup_Date] [nvarchar](50) NULL,
    [f_Remarks] [nvarchar](450) NULL,
    [f_Queue_State] [nvarchar](50) NULL,
    [f_Queue_Status] [nvarchar](50) NULL,
    [f_Queue_Process_Date] [datetime] NULL,
    [f_Queue_Remarks] [nvarchar](450) NULL,
    [f_CreatedType] [nvarchar](50) NULL,
    [f_CreatedByName] [nvarchar](150) NULL,
    [f_CreatedIP] [nvarchar](100) NULL,
    [f_CreatedDate] [datetime] NULL,
    [f_UpdatedType] [nvarchar](50) NULL,
    [f_UpdatedByName] [nvarchar](150) NULL,
    [f_UpdatedIP] [nvarchar](100) NULL,
    [f_UpdatedDate] [datetime] NULL,
    [f_SessionID] [nvarchar](50) NULL,
    [f_FormCode] [nvarchar](50) NULL,
    [f_Dummy] [bit] NULL,
    [f_Drawn_Bank_MICR] [nvarchar](50) NULL,
    [f_Drawn_Bank_IFSCCOde] [nvarchar](50) NULL,
    [F_CMS_Location_Code] [nvarchar](50) NULL,
    [F_CMS_Location_Name] [nvarchar](200) NULL,
    [f_Movedto_Stg_date] [datetime] NULL,
    [f_Movedto_Stg] [nvarchar](2) NULL,
    [f_Btp_Agency_Usr_Branch_Cd] [nvarchar](50) NULL,
    [f_Btp_Agency_Usr_Branch_Name] [nvarchar](100) NULL,
    [f_Agency_Code] [nvarchar](50) NULL,
    [f_Agency_Name] [nvarchar](100) NULL,
    [f_CMS_Bank_Sr_No] [nvarchar](15) NULL,
    [f_Is_CMS_NCMS] [nvarchar](20) NULL,
    [f_ChequeDepositedBy] [nvarchar](50) NULL,
    [f_NONCMSCheque_Deposit_Date] [date] NULL
);
END
GO

/* ----- t_FD_BT_Payinslip_Hdr: a pay-in slip ---------------------------------------------
   One row for each slip generated on Pay In Slip Generation, from the application's
   row in the staging table. The app fills the slip's number (f_Payinslip_No), the
   number of cheques on it (1), the agency code, the Axis CMS bank and location,
   who generated it and when; every other column is left empty. A reprint is a new
   slip with a fresh number.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Payinslip_Hdr', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Payinslip_Hdr](
    [f_Pk_t_BT_Payinslip_Hdr_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Payinslip_Hdr_Abr] [nvarchar](20) NULL,
    [f_Payinslip_Hdr_Seq] [nvarchar](50) NULL,
    [f_Payinslip_HdrSequence] [nvarchar](50) NULL,
    [f_Payinslip_Stg_HdrSequence] [nvarchar](50) NULL,
    [f_Payinslip_SourceName] [nvarchar](150) NULL,
    [f_PortalSourceCode] [nvarchar](50) NULL,
    [f_MCP_Code] [nvarchar](50) NULL,
    [f_SCP_Code] [nvarchar](50) NULL,
    [f_MCP_Broker_Code] [nvarchar](50) NULL,
    [f_SCP_Broker_Code] [nvarchar](50) NULL,
    [f_Employee_Code] [nvarchar](50) NULL,
    [f_ADO_Indicator] [nvarchar](10) NULL,
    [f_CMS_Type] [nvarchar](50) NULL,
    [f_CMS_Bank_Name] [nvarchar](150) NULL,
    [f_CMS_Branch_Name] [nvarchar](150) NULL,
    [f_CMS_Bank_IFSC_Code] [nvarchar](50) NULL,
    [f_CMS_Bank_MICR_Code] [nvarchar](50) NULL,
    [f_MF_Branch_Code] [nvarchar](50) NULL,
    [f_MF_Branch_Name] [nvarchar](50) NULL,
    [f_SCP_Location_Code] [nvarchar](50) NULL,
    [f_SCP_Location_Name] [nvarchar](50) NULL,
    [f_Entry_Date] [date] NULL,
    [f_Pickup_Date] [date] NULL,
    [f_Payinslip_No] [nvarchar](50) NULL,
    [f_Remarks] [nvarchar](450) NULL,
    [f_Req_State] [nvarchar](50) NULL,
    [f_Req_Status] [nvarchar](50) NULL,
    [f_Req_Date] [datetime] NULL,
    [f_Req_Remarks] [nvarchar](450) NULL,
    [f_CreatedType] [nvarchar](50) NULL,
    [f_CreatedByName] [nvarchar](150) NULL,
    [f_CreatedIP] [nvarchar](100) NULL,
    [f_CreatedDate] [datetime] NULL,
    [f_UpdatedType] [nvarchar](50) NULL,
    [f_UpdatedByName] [nvarchar](150) NULL,
    [f_UpdatedIP] [nvarchar](100) NULL,
    [f_UpdatedDate] [datetime] NULL,
    [f_SessionID] [bigint] NULL,
    [f_FormCode] [nvarchar](50) NULL,
    [f_Dummy] [bit] NULL,
    [f_Cheque_Count] [int] NULL,
    [f_CMS_Location_Desc] [nvarchar](100) NULL,
    [f_CMS_Location_Code] [nvarchar](50) NULL,
    [f_BTP_Location_Code] [nvarchar](50) NULL,
    [f_BTP_Location_Desc] [nvarchar](100) NULL,
    [f_Agency_Code] [nvarchar](50) NULL,
    [f_Agency_Name] [nvarchar](100) NULL,
    [f_CMS_Branch_Code] [nvarchar](50) NULL,
    [f_CMS_Bank_Sr_No] [nvarchar](15) NULL,
    [f_Is_CMS_NCMS] [nvarchar](20) NULL,
    [f_ChequeDepositedBy] [nvarchar](50) NULL,
    [f_NONCMSCheque_Deposit_Date] [date] NULL
);
END
GO

/* ----- t_FD_BT_Payinslip_Dtl: the cheque on a pay-in slip -------------------------------
   One row beside each slip's row above, under the same f_Payinslip_No. The app
   fills the application number and the applicant's name, the cheque (number, date,
   amount), the bank it is drawn on, the Axis CMS bank and location, who generated
   the slip and when; every other column is left empty. An application's slip in
   force is its newest row here.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_BT_Payinslip_Dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_BT_Payinslip_Dtl](
    [f_Pk_t_BT_Payinslip_Dtl_ID] [bigint] IDENTITY(1,1) NOT NULL,
    [f_Payinslip_Dtl_Abr] [nvarchar](20) NULL,
    [f_Payinslip_Dtl_Seq] [nvarchar](50) NULL,
    [f_Payinslip_DtlSequence] [nvarchar](50) NULL,
    [f_Payinslip_HdrSequence] [nvarchar](50) NULL,
    [f_Payinslip_SourceName] [nvarchar](150) NULL,
    [f_PortalSourceCode] [nvarchar](50) NULL,
    [f_MCP_Code] [nvarchar](50) NULL,
    [f_SCP_Code] [nvarchar](50) NULL,
    [f_MCP_Broker_Code] [nvarchar](50) NULL,
    [f_SCP_Broker_Code] [nvarchar](50) NULL,
    [f_Employee_Code] [nvarchar](50) NULL,
    [f_ADO_Indicator] [nvarchar](10) NULL,
    [f_CP_Trans_Ref_No] [nvarchar](50) NULL,
    [f_MF_Sys_Ref_no] [nvarchar](50) NULL,
    [f_Ref_Cust_Code] [nvarchar](50) NULL,
    [f_Ref_Rm_Code] [nvarchar](50) NULL,
    [f_Ref_Othr_Code] [nvarchar](50) NULL,
    [f_Ref_Type] [nvarchar](50) NULL,
    [f_CMS_Type] [nvarchar](50) NULL,
    [f_CMS_Bank_Name] [nvarchar](150) NULL,
    [f_CMS_Branch_Name] [nvarchar](150) NULL,
    [f_CMS_Bank_IFSC_Code] [nvarchar](50) NULL,
    [f_CMS_Bank_MICR_Code] [nvarchar](50) NULL,
    [f_MF_Branch_Code] [nvarchar](50) NULL,
    [f_MF_Branch_Name] [nvarchar](50) NULL,
    [f_SCP_Location_Code] [nvarchar](50) NULL,
    [f_SCP_Location_Name] [nvarchar](50) NULL,
    [f_Payinslip_No] [nvarchar](50) NULL,
    [f_Cheque_Number] [nvarchar](50) NULL,
    [f_Cheque_Date] [date] NULL,
    [f_Cheque_Amt] [numeric](18, 0) NULL,
    [f_Drawn_Bank_Name] [nvarchar](150) NULL,
    [f_Drawn_Branch_Name] [nvarchar](150) NULL,
    [f_Applicant_Name] [nvarchar](250) NULL,
    [f_Application_Number] [nvarchar](50) NULL,
    [f_Entry_Date] [date] NULL,
    [f_Pickup_Date] [date] NULL,
    [f_Remarks] [nvarchar](450) NULL,
    [f_Process_Flg] [nvarchar](50) NULL,
    [f_Process_Date] [datetime] NULL,
    [f_Process_Remarks] [nvarchar](450) NULL,
    [f_Active_Flg] [nvarchar](10) NULL,
    [f_CreatedType] [nvarchar](50) NULL,
    [f_CreatedByName] [nvarchar](150) NULL,
    [f_CreatedIP] [nvarchar](100) NULL,
    [f_CreatedDate] [datetime] NULL,
    [f_UpdatedType] [nvarchar](50) NULL,
    [f_UpdatedByName] [nvarchar](150) NULL,
    [f_UpdatedIP] [nvarchar](100) NULL,
    [f_UpdatedDate] [datetime] NULL,
    [f_SessionID] [bigint] NULL,
    [f_FormCode] [nvarchar](50) NULL,
    [f_Dummy] [bit] NULL,
    [f_Drawn_Bank_MICR] [nvarchar](50) NULL,
    [f_Drawn_Bank_IFSCCOde] [nvarchar](50) NULL,
    [F_CMS_Location] [nvarchar](200) NULL,
    [f_cms_location_code] [nvarchar](50) NULL,
    [f_BTP_Location_Code] [nvarchar](50) NULL,
    [f_BTP_Location_Desc] [nvarchar](100) NULL,
    [f_CMS_Bank_Sr_No] [nvarchar](15) NULL,
    [f_CMS_Branch_Code] [nvarchar](50) NULL,
    [f_Is_CMS_NCMS] [nvarchar](20) NULL,
    [f_ChequeDepositedBy] [nvarchar](50) NULL,
    [f_NONCMSCheque_Deposit_Date] [date] NULL
);
END
GO

/* ----- The short link sent to an investor --------------------------------------------
   The FD system's own two tables: a purchase's link is a row of
   t_FD_PGW_PaymentUrlRequest_dtl, a renewal's of T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL.
   An application has one live link, its active row. The first link sent is a new
   row: the application number, the investor's name and PAN, the amount, the date
   it was submitted, the short link (f_ShotrURL) and the page it opens (f_FullURL),
   the hours it is open for (f_Link_Validity) and when that ends
   (f_Link_Expiry_Date), who sent it and when. Regenerate makes no new link and no
   new row: the same short link goes again, and its row is given 24 hours from then
   (f_Link_Validity, f_Link_Expiry_Date and the f_Updated columns). Every other
   column is left empty. The tables hold other systems' rows too.
   ----------------------------------------------------------------------------- */

/* ----- t_FD_PGW_PaymentUrlRequest_dtl: a purchase's link -------------------------------
   TO CONFIRM: the columns from f_TermsCondition down are as the database has them.
   Those above it (the key down to f_Disclaimer2) were not seen: they are written
   here as the renewal table below has them, which is line for line the same
   wherever both were seen.
   ----------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.t_FD_PGW_PaymentUrlRequest_dtl', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[t_FD_PGW_PaymentUrlRequest_dtl](
    [f_Pk_PayReqDtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_RequestDtl_Abr] [nvarchar](5) NULL,
    [f_RequestDtl_Seq] [nvarchar](20) NULL,
    [f_RequestDtlSequence] [nvarchar](50) NULL,
    [f_RequestHdrSequence] [nvarchar](50) NULL,
    [f_PGW_Username] [nvarchar](50) NULL,
    [f_MCPCode] [nvarchar](50) NULL,
    [f_SCPCode] [nvarchar](50) NULL,
    [f_Mf_Sys_Ref_no] [nvarchar](50) NULL,
    [f_Ref_Rm_Code] [nvarchar](50) NULL,
    [f_Ref_Othr_Code] [nvarchar](50) NULL,
    [f_Ref_Type] [nvarchar](50) NULL,
    [f_Mcp_Busi_Code] [nvarchar](50) NULL,
    [f_Cp_Location_Code] [nvarchar](50) NULL,
    [f_Cp_Api_User_Name] [nvarchar](100) NULL,
    [f_Mf_EntityName] [nvarchar](100) NULL,
    [F_Mf_EntityId] [nvarchar](50) NULL,
    [F_Mf_Branchcode] [nvarchar](50) NULL,
    [F_Mf_Branchname] [nvarchar](100) NULL,
    [f_Mf_AppName] [nvarchar](100) NULL,
    [f_InvestorName] [nvarchar](100) NULL,
    [f_AppNo] [nvarchar](50) NULL,
    [f_InvestmentAmount] [numeric](18, 2) NULL,
    [f_Investor_Pan] [nvarchar](50) NULL,
    [F_ApplicationDate] [datetime] NULL,
    [f_Key_Plaintext] [nvarchar](100) NULL,
    [f_Key_Encryppted] [nvarchar](100) NULL,
    [f_Key_GUID] [nvarchar](100) NULL,
    [f_ShotrURL] [nvarchar](100) NULL,
    [f_FullURL] [nvarchar](max) NULL,
    [f_Link_Validity] [int] NULL,
    [f_Link_Expiry_Date] [datetime] NULL,
    [f_Link_Expiry_Remark] [nvarchar](max) NULL,
    [f_Disclaimer1] [bit] NULL,
    [f_Disclaimer2] [bit] NULL,
    [f_TermsCondition] [bit] NULL,
    [f_KycConfirmation] [bit] NULL,
    [f_Active] [bit] NULL,
    [f_Dummy] [bit] NULL,
    [f_SourceAPI] [nvarchar](50) NULL,
    [f_CreatedID] [nvarchar](85) NULL,
    [f_CreatedType] [nvarchar](3) NULL,
    [f_CreatedIP] [nvarchar](100) NULL,
    [f_CreatedBy] [nvarchar](85) NULL,
    [f_CreatedByUName] [nvarchar](85) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_UpdatedID] [nvarchar](85) NULL,
    [f_UpdatedType] [nvarchar](3) NULL,
    [f_UpdatedIP] [nvarchar](100) NULL,
    [f_UpdatedBy] [nvarchar](85) NULL,
    [f_UpdatedByUName] [nvarchar](85) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_IsMobile] [bit] NULL,
    [f_RequestUrl] [nvarchar](500) NULL,
    [f_ClientIPAddress] [nvarchar](50) NULL,
    [f_ServerDomainName] [nvarchar](50) NULL,
    [f_BrowserType] [nvarchar](20) NULL,
    [f_BrowserVersion] [nvarchar](20) NULL,
    [f_BrowserMajorVersion] [nvarchar](10) NULL,
    [f_BrowserMinorVersion] [nvarchar](10) NULL,
    [f_SessionId] [bigint] NULL,
    [f_FormCode] [nvarchar](50) NULL,
    [f_Cp_Code] [nvarchar](50) NULL,
    [f_CP_Trans_Ref_No] [nvarchar](50) NULL,
    [f_CP_Name] [nvarchar](100) NULL,
    [f_Ref_Cust_Code] [nvarchar](50) NULL,
    [f_Is_Session_Expired] [bit] NULL,
    [f_PayUForm] [nvarchar](max) NULL,
    [f_PP_CP_Ext_Code] [nvarchar](50) NULL,
    [f_Map_Code] [nvarchar](50) NULL,
    [f_PP_CP_Code] [nvarchar](50) NULL,
    [f_CAMS_FormData] [nvarchar](max) NULL,
    [IsOTP_Verfied] [nvarchar](50) NULL,
    [f_VAN] [nvarchar](50) NULL
);
END
GO

/* ----- T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL: a renewal's link ------------------------ */
IF OBJECT_ID(N'dbo.T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL](
    [f_Pk_PayReqDtl_Id] [bigint] IDENTITY(1,1) NOT NULL,
    [f_RequestDtl_Abr] [nvarchar](5) NULL,
    [f_RequestDtl_Seq] [nvarchar](20) NULL,
    [f_RequestDtlSequence] [nvarchar](50) NULL,
    [f_RequestHdrSequence] [nvarchar](50) NULL,
    [f_PGW_Username] [nvarchar](50) NULL,
    [f_MCPCode] [nvarchar](50) NULL,
    [f_SCPCode] [nvarchar](50) NULL,
    [f_Mf_Sys_Ref_no] [nvarchar](50) NULL,
    [f_Ref_Rm_Code] [nvarchar](50) NULL,
    [f_Ref_Othr_Code] [nvarchar](50) NULL,
    [f_Ref_Type] [nvarchar](50) NULL,
    [f_Mcp_Busi_Code] [nvarchar](50) NULL,
    [f_Cp_Location_Code] [nvarchar](50) NULL,
    [f_Cp_Api_User_Name] [nvarchar](100) NULL,
    [f_Mf_EntityName] [nvarchar](100) NULL,
    [F_Mf_EntityId] [nvarchar](50) NULL,
    [F_Mf_Branchcode] [nvarchar](50) NULL,
    [F_Mf_Branchname] [nvarchar](100) NULL,
    [f_Mf_AppName] [nvarchar](100) NULL,
    [f_InvestorName] [nvarchar](100) NULL,
    [f_AppNo] [nvarchar](50) NULL,
    [f_InvestmentAmount] [numeric](18, 2) NULL,
    [f_Investor_Pan] [nvarchar](50) NULL,
    [F_ApplicationDate] [datetime] NULL,
    [f_Key_Plaintext] [nvarchar](100) NULL,
    [f_Key_Encryppted] [nvarchar](100) NULL,
    [f_Key_GUID] [nvarchar](100) NULL,
    [f_ShotrURL] [nvarchar](100) NULL,
    [f_FullURL] [nvarchar](max) NULL,
    [f_Link_Validity] [int] NULL,
    [f_Link_Expiry_Date] [datetime] NULL,
    [f_Link_Expiry_Remark] [nvarchar](max) NULL,
    [f_Disclaimer1] [bit] NULL,
    [f_Disclaimer2] [bit] NULL,
    [f_TermsCondition] [bit] NULL,
    [f_KycConfirmation] [bit] NULL,
    [f_Active] [bit] NULL,
    [f_Dummy] [bit] NULL,
    [f_SourceAPI] [nvarchar](50) NULL,
    [f_CreatedID] [nvarchar](85) NULL,
    [f_CreatedType] [nvarchar](3) NULL,
    [f_CreatedIP] [nvarchar](100) NULL,
    [f_CreatedBy] [nvarchar](85) NULL,
    [f_CreatedByUName] [nvarchar](85) NULL,
    [f_CreatedOn] [datetime] NULL,
    [f_UpdatedID] [nvarchar](50) NULL,
    [f_UpdatedType] [nvarchar](3) NULL,
    [f_UpdatedIP] [nvarchar](100) NULL,
    [f_UpdatedBy] [nvarchar](85) NULL,
    [f_UpdatedByUName] [nvarchar](85) NULL,
    [f_UpdatedOn] [datetime] NULL,
    [f_IsMobile] [bit] NULL,
    [f_RequestUrl] [nvarchar](500) NULL,
    [f_ClientIPAddress] [nvarchar](50) NULL,
    [f_ServerDomainName] [nvarchar](50) NULL,
    [f_BrowserType] [nvarchar](20) NULL,
    [f_BrowserVersion] [nvarchar](20) NULL,
    [f_BrowserMajorVersion] [nvarchar](10) NULL,
    [f_BrowserMinorVersion] [nvarchar](10) NULL,
    [f_SessionId] [bigint] NULL,
    [f_FormCode] [nvarchar](50) NULL,
    [f_Cp_Code] [nvarchar](50) NULL,
    [f_CP_Trans_Ref_No] [nvarchar](50) NULL,
    [f_CP_Name] [nvarchar](100) NULL,
    [f_Ref_Cust_Code] [nvarchar](50) NULL,
    [f_Is_Session_Expired] [bit] NULL,
    [f_PayUForm] [nvarchar](max) NULL,
    [f_PP_CP_Code] [nvarchar](100) NULL,
    [f_PP_CP_Ext_Code] [nvarchar](100) NULL,
    [f_Map_Code] [nvarchar](100) NULL,
    [f_File_Path] [nvarchar](max) NULL,
    CONSTRAINT [PK_T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL] PRIMARY KEY CLUSTERED ([f_Pk_PayReqDtl_Id] ASC)
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
