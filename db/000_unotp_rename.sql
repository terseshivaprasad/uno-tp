/* =============================================================================
   Uno TP - 000: every Uno TP table carries the t_Unotp_ prefix.

   For a database made before the prefix: renames each table from its old name
   (t_Application_Mst) to its new one (t_Unotp_Application_Mst), keeping its rows,
   indexes and constraints. Run it FIRST, before 001-006 - they create the tables
   under the new names only where those do not exist, so run first they would
   stand up empty tables beside the old ones.

   Re-runnable: a table already renamed, or never made, is left alone. On a new
   database it does nothing.
   ============================================================================= */
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.t_Address_Dtls', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Address_Dtls', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Address_Dtls', N't_Unotp_Address_Dtls';
IF OBJECT_ID(N'dbo.t_App_Config', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_App_Config', N'U') IS NULL
    EXEC sp_rename N'dbo.t_App_Config', N't_Unotp_App_Config';
IF OBJECT_ID(N'dbo.t_Application_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Application_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Application_Mst', N't_Unotp_Application_Mst';
IF OBJECT_ID(N'dbo.t_Bank_Dtls', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Bank_Dtls', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Bank_Dtls', N't_Unotp_Bank_Dtls';
IF OBJECT_ID(N'dbo.t_Broker_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Broker_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Broker_Mst', N't_Unotp_Broker_Mst';
IF OBJECT_ID(N'dbo.t_Console_Notice', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Console_Notice', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Console_Notice', N't_Unotp_Console_Notice';
IF OBJECT_ID(N'dbo.t_Console_Window', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Console_Window', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Console_Window', N't_Unotp_Console_Window';
IF OBJECT_ID(N'dbo.t_Feature_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Feature_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Feature_Mst', N't_Unotp_Feature_Mst';
IF OBJECT_ID(N'dbo.t_Ifsc_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Ifsc_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Ifsc_Mst', N't_Unotp_Ifsc_Mst';
IF OBJECT_ID(N'dbo.t_Investment_Dtls', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Investment_Dtls', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Investment_Dtls', N't_Unotp_Investment_Dtls';
IF OBJECT_ID(N'dbo.t_Investor_Folio', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Investor_Folio', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Investor_Folio', N't_Unotp_Investor_Folio';
IF OBJECT_ID(N'dbo.t_Kyc_Documents', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Kyc_Documents', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Kyc_Documents', N't_Unotp_Kyc_Documents';
IF OBJECT_ID(N'dbo.t_Kyc_Dtls', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Kyc_Dtls', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Kyc_Dtls', N't_Unotp_Kyc_Dtls';
IF OBJECT_ID(N'dbo.t_Nominee_Dtls', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Nominee_Dtls', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Nominee_Dtls', N't_Unotp_Nominee_Dtls';
IF OBJECT_ID(N'dbo.t_Page_State', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Page_State', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Page_State', N't_Unotp_Page_State';
IF OBJECT_ID(N'dbo.t_Partner_Menu', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Partner_Menu', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Partner_Menu', N't_Unotp_Partner_Menu';
IF OBJECT_ID(N'dbo.t_Partner_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Partner_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Partner_Mst', N't_Unotp_Partner_Mst';
IF OBJECT_ID(N'dbo.t_Pay_In_Slip', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Pay_In_Slip', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Pay_In_Slip', N't_Unotp_Pay_In_Slip';
IF OBJECT_ID(N'dbo.t_Payment_Bank_Dtls', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Payment_Bank_Dtls', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Payment_Bank_Dtls', N't_Unotp_Payment_Bank_Dtls';
IF OBJECT_ID(N'dbo.t_Payment_Link', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Payment_Link', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Payment_Link', N't_Unotp_Payment_Link';
IF OBJECT_ID(N'dbo.t_Pincode_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Pincode_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Pincode_Mst', N't_Unotp_Pincode_Mst';
IF OBJECT_ID(N'dbo.t_Rate_Card', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Rate_Card', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Rate_Card', N't_Unotp_Rate_Card';
IF OBJECT_ID(N'dbo.t_Ref_List', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Ref_List', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Ref_List', N't_Unotp_Ref_List';
IF OBJECT_ID(N'dbo.t_Staff_Mst', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Staff_Mst', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Staff_Mst', N't_Unotp_Staff_Mst';
IF OBJECT_ID(N'dbo.t_Upload_State', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_Upload_State', N'U') IS NULL
    EXEC sp_rename N'dbo.t_Upload_State', N't_Unotp_Upload_State';
IF OBJECT_ID(N'dbo.t_User_Session', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.t_Unotp_User_Session', N'U') IS NULL
    EXEC sp_rename N'dbo.t_User_Session', N't_Unotp_User_Session';
GO
