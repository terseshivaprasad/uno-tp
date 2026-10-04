/* =============================================================================
   Uno TP - the deposit is written to the FD system's t_FD_BT_Investment_Dtl in
   place of t_Unotp_Investment_Dtls.

   That table has a column for the rate but none for the rest of the quote
   locked on submit, so the interest each payout, the maturity amount and date
   and the rate card's date go on t_Unotp_Application_Mst. Nothing is changed on
   t_FD_BT_Investment_Dtl, and t_Unotp_Investment_Dtls is left as it is.

   A renewal can renew the old deposit's principal only, so its principal is kept
   beside its maturity amount (n_Renew_Principal).

   Safe to run again: each column is added only when it is not there yet.
   ============================================================================= */
SET NOCOUNT ON;
GO
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'n_Quote_Interest_Each') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD n_Quote_Interest_Each DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'n_Quote_Maturity_Amount') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD n_Quote_Maturity_Amount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'd_Quote_Matures_On') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD d_Quote_Matures_On DATE NULL;
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'd_Quote_Rate_As_On') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD d_Quote_Rate_As_On DATE NULL;
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'n_Renew_Principal') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD n_Renew_Principal BIGINT NULL;
GO
