/* =============================================================================
   Uno TP - a utility bill is not taken as a proof of address.

   The proofs of address are an Aadhaar, a passport, a driving licence and a voter
   ID. Where the proofsOfAddress list in t_Unotp_Ref_List still carries a utility
   bill, it is taken off the pages (f_Active 0): no box offers it or accepts it,
   and what was saved with it on an earlier application is kept.

   Safe to run again: a row already off is left as it is.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE dbo.t_Unotp_Ref_List
SET f_Active = 0, c_Updated_By = 'SYSTEM', d_Updated_On = SYSDATETIME()
WHERE c_List = 'proofsOfAddress' AND c_Code = N'Utility bill' AND f_Active = 1;
GO
