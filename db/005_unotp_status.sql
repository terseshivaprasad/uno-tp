/* =============================================================================
   Uno TP - 005: the stages an application goes through after it is submitted
   that Uno TP itself does not do, dated on the application's header for View
   Application's status list (entry, short link, acceptance, pay-in slip, penny
   drop, payment, KYC verification, FDR).

   The short link (t_Unotp_Payment_Link), the pay-in slip (t_Unotp_Pay_In_Slip), acceptance,
   payment and the FDR are already recorded. Two are not, and are written here by
   the Operations feed, never by Uno TP:

     d_Penny_Drop_On / c_Penny_Drop_Status   the repayment account's penny drop:
                                             '' not done yet, OK, FAILED
     d_Kyc_Verified_On / c_Kyc_Status        Operations' KYC verification:
                                             '' not done yet, OK, REJECTED

   Re-runnable: every column is added only where it is missing. No foreign keys.
   ============================================================================= */
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'd_Penny_Drop_On') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD d_Penny_Drop_On DATETIME2(3) NULL;
GO
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'c_Penny_Drop_Status') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD c_Penny_Drop_Status VARCHAR(10) NOT NULL
        CONSTRAINT DF_Application_Mst_Penny DEFAULT ('')
        CONSTRAINT CK_Application_Mst_Penny CHECK (c_Penny_Drop_Status IN ('', 'OK', 'FAILED'));
GO
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'd_Kyc_Verified_On') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD d_Kyc_Verified_On DATETIME2(3) NULL;
GO
IF COL_LENGTH(N'dbo.t_Unotp_Application_Mst', N'c_Kyc_Status') IS NULL
    ALTER TABLE dbo.t_Unotp_Application_Mst ADD c_Kyc_Status VARCHAR(10) NOT NULL
        CONSTRAINT DF_Application_Mst_Kyc DEFAULT ('')
        CONSTRAINT CK_Application_Mst_Kyc CHECK (c_Kyc_Status IN ('', 'OK', 'REJECTED'));
GO
