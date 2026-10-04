/* =============================================================================
   Uno TP - t_FD_BT_Investment_Dtl.f_Int_Freq holds the payout as the FD system's
   rate card names it (INTEREST_FREQ in t_FD_BOTC_SCHEME).

   Each entry of the 'payouts' list takes that name as "interestFreq" in its
   j_Attrs; the app writes it to f_Int_Freq and reads the payout back off it. A
   payout with no such name is written under the app's own code, as before.

   Review with the business: the names must be the rate card's own, letter for
   letter.

   Safe to run again: a name is set only where the entry has none yet.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE r SET j_Attrs = JSON_MODIFY(ISNULL(r.j_Attrs, N'{}'), '$.interestFreq', v.n), c_Updated_By = 'SEED', d_Updated_On = SYSDATETIME()
FROM dbo.t_Unotp_Ref_List r
JOIN (VALUES
    (N'maturity', N'COMP. ANNUALY'),
    (N'yearly', N'YEARLY'),
    (N'halfyearly', N'HALF YEARLY'),
    (N'quarterly', N'QUARTERLY'),
    (N'monthly', N'MONTHLY')
) v (c, n) ON v.c = r.c_Code
WHERE r.c_List = 'payouts' AND JSON_VALUE(ISNULL(r.j_Attrs, N'{}'), '$.interestFreq') IS NULL;
GO
