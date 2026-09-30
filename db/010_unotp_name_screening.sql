/* =============================================================================
   Uno TP - name screening on the KYC row.

   Investor Information asks the name screening service about every holder before
   the application goes on. What it said - allowed or blocked, the service's
   reference, and when - is kept on the holder's t_Unotp_Kyc_Dtls row.

   Safe to run again: each column is added only when it is not there yet.
   ============================================================================= */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO
IF COL_LENGTH(N'dbo.t_Unotp_Kyc_Dtls', N'c_Screening_Status') IS NULL
    ALTER TABLE dbo.t_Unotp_Kyc_Dtls ADD c_Screening_Status VARCHAR(10) NOT NULL CONSTRAINT DF_Kyc_Dtls_Screening DEFAULT ('');
IF COL_LENGTH(N'dbo.t_Unotp_Kyc_Dtls', N'c_Screening_Ref') IS NULL
    ALTER TABLE dbo.t_Unotp_Kyc_Dtls ADD c_Screening_Ref VARCHAR(50) NOT NULL CONSTRAINT DF_Kyc_Dtls_Screening_Ref DEFAULT ('');
IF COL_LENGTH(N'dbo.t_Unotp_Kyc_Dtls', N'd_Screened_On') IS NULL
    ALTER TABLE dbo.t_Unotp_Kyc_Dtls ADD d_Screened_On DATETIME2(3) NULL;
GO
