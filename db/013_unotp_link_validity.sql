/* =============================================================================
   Uno TP - the payment link runs 3 days; a new one may be sent until the
   application cancels itself, cancellationDays (14) after it was created.

   linkValidityHours.payment goes to 72. The count of resends (linkResends,
   n_Resends_Left) is no longer the rule: the window is the application's age.
   The SQL backend marks unpaid applications older than cancellationDays as
   cancelled once an hour (SqlAutoCancel).
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE dbo.t_Unotp_App_Config SET c_Value = N'72', c_Description = N'Hours a payment link stays open, from when it is sent or sent again'
WHERE c_Key = N'linkValidityHours.payment' AND c_Value <> N'72';
GO
