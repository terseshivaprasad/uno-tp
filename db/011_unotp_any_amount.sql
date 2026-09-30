/* =============================================================================
   Uno TP - any amount, not only a multiple of Rs 1,000.

   amountStep in t_Unotp_App_Config set to 1: FD Configuration takes any whole
   rupee amount between the minimum and the maximum. A larger step would ask for
   multiples of it again.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE dbo.t_Unotp_App_Config
SET c_Value = N'1', c_Description = N'A deposit is a multiple of this, rupees; 1 for any amount'
WHERE c_Key = N'amountStep' AND c_Value <> N'1';
GO
