/* =============================================================================
   Uno TP - t_FD_BT_Kyc_Data_Dtl.f_Data_Source says where a holder's KYC came from:
   FRESH for one given on the application, CKYC once it is fetched from CERSAI,
   and for a holder on a folio the source the folio's record names.

   The folio's source is read off t_Unotp_Investor_Folio (c_Source), which is loaded
   with the folios from the FD system, and is kept with the application
   (t_Unotp_Application_Mst.c_Data_Source) so every save writes the same one.

   Run on the main database and, where the folios are in a database of their own,
   on that one as well: each column is added only where its table is.

   Safe to run again: a column is added only when it is not there yet.
   ============================================================================= */
SET NOCOUNT ON;
GO
IF OBJECT_ID(N'dbo.t_Unotp_Application_Mst', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'c_Data_Source') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD c_Data_Source VARCHAR(50) NULL;
IF OBJECT_ID(N'dbo.t_Unotp_Investor_Folio', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.t_Unotp_Investor_Folio', N'c_Source') IS NULL
    ALTER TABLE dbo.t_Unotp_Investor_Folio ADD c_Source VARCHAR(50) NULL;
GO
