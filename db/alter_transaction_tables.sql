/* =============================================================================
   Uno TP - columns added to the transaction tables (SQL Server 2016 or later).

   db/create_transaction_tables.sql has the FD system's own tables as their schemas
   were given. This script is every column the app needs that those schemas did
   not have. A column is added only when the table does not have it yet, so the
   script is safe to run again; nothing else about a table is touched - no column
   is changed or dropped, no row is written. Every column added takes NULL, so
   rows already there, and whatever else writes the table, are not affected.

       sqlcmd -d UnoTP -i db/create_transaction_tables.sql   (a new database only)
       sqlcmd -d UnoTP -i db/alter_transaction_tables.sql
       sqlcmd -d UnoTP -i db/create_new_tables.sql
       sqlcmd -d UnoTP -i db/insert_seed.sql

   Run this before the app that writes the columns is put in place: it reads and
   writes them on every save of Upload Documents.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ----- t_FD_BT_KYC_document: IDfy's ids for the checks on a document -------------------
   Every request the app sends IDfy carries two ids: a task id of its own, and the
   application's group id, the same for every request made for that application
   (made from the application number), so IDfy files them together. Both are kept
   on the document's row, so any check can be traced in IDfy by them:

     f_IdfyGroupId             the application's group id; the same on every
                               document of the application IDfy was asked about
     f_IdfyIdentifyTaskId      the task id of the request that identified the document
     f_IdfyExtractTaskId       the task id of the request that read it (OCR)
     f_IdfyVerifyTaskId        the task id of the request that confirmed it with its
                               issuer (a passport, a driving licence, a voter ID)
     f_IdfyFaceCompareTaskId   the task id of the request that compared the faces on
                               the PAN copy and the proof of address; on both rows

   Each is empty where that check was not asked of the document.
   ----------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IdfyGroupId') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IdfyGroupId] [nvarchar](50) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IdfyIdentifyTaskId') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IdfyIdentifyTaskId] [nvarchar](50) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IdfyExtractTaskId') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IdfyExtractTaskId] [nvarchar](50) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IdfyVerifyTaskId') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IdfyVerifyTaskId] [nvarchar](50) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IdfyFaceCompareTaskId') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IdfyFaceCompareTaskId] [nvarchar](50) NULL;
GO

/* ----- t_FD_BT_KYC_document: what the checks on a holder's PAN and proof said ----------
   Written on Upload Documents, for a holder with no folio. Every column is NULL
   where that check was not asked or gave no answer, and on every other document.

   On the row of the holder's PAN copy:
     f_NsdlPanMatch            1 when NSDL holds the PAN, 0 when it does not
     f_NsdlDobMatch            1 when NSDL holds the date of birth against the PAN
     f_NsdlNameMatch           1 when NSDL holds the name against the PAN
     f_NsdlRemark              'Name not verified with NSDL' where the name on the PAN
                               copy was taken after NSDL turned down every name typed
     f_IsPanAadhaarLinked      1 when an Aadhaar is held against the PAN, 0 when none is
     f_IdfyPanAadhaarLinkTaskId
                               the task id of the request IDfy answered the link with

   On the row of the holder's proof of address:
     f_PanPoaNameMatch         the name on the proof against the PAN's: MATCH, PARTIAL
                               or MISMATCH
     f_PanPoaDobMatch          1 when the date of birth on the proof is the PAN's, 0
                               when it is not; NULL for a proof that carries none

   On both:
     f_IsVerifiedWithSource    1 when the document's own source confirmed it, 0 when
                               it was asked and did not
     f_VerifiedSource          the source asked: NSDL for a PAN copy; Sarathi (a
                               driving licence), Passport Seva (a passport) or The
                               Election Commission (a voter ID) for a proof. An
                               Aadhaar has no source to ask

   What a document was identified as is already kept, in f_IdfyIdentifiedDocument.
   ----------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_NsdlPanMatch') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_NsdlPanMatch] [bit] NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_NsdlDobMatch') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_NsdlDobMatch] [bit] NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_NsdlNameMatch') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_NsdlNameMatch] [bit] NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_NsdlRemark') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_NsdlRemark] [nvarchar](100) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_PanPoaNameMatch') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_PanPoaNameMatch] [nvarchar](20) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_PanPoaDobMatch') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_PanPoaDobMatch] [bit] NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IsVerifiedWithSource') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IsVerifiedWithSource] [bit] NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_VerifiedSource') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_VerifiedSource] [nvarchar](50) NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IsPanAadhaarLinked') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IsPanAadhaarLinked] [bit] NULL;
GO

IF COL_LENGTH(N'dbo.t_FD_BT_KYC_document', N'f_IdfyPanAadhaarLinkTaskId') IS NULL
    ALTER TABLE [dbo].[t_FD_BT_KYC_document] ADD [f_IdfyPanAadhaarLinkTaskId] [nvarchar](50) NULL;
GO
