/* =============================================================================
   Uno TP - the rates come from the FD system's own rate card, t_FD_BOTC_SCHEME,
   and t_FD_BT_Investment_Dtl takes f_Category, f_Scheme and f_Scheme_Code off it.

   The card names each category and scheme its own way, so:
     - each entry of the 'categories' list takes the card's CATEGORY as
       "rateCategory" in its j_Attrs: the rows read for it, and what is written
       to f_Category;
     - each entry of the 'payouts' list takes the card's SCHEME as "scheme" in its
       j_Attrs. With its "interestFreq" (script 018) it says which rows of the card
       the payout is. A row of a scheme no payout names is not offered.
   defaultRateCategory stays a code of the 'categories' list.

   Review with the business: the names must be the rate card's own, letter for
   letter.

   Safe to run again: a name is set only where the entry has none yet.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE r SET j_Attrs = JSON_MODIFY(ISNULL(r.j_Attrs, N'{}'), '$.rateCategory', v.n), c_Updated_By = 'SEED', d_Updated_On = SYSDATETIME()
FROM dbo.t_Unotp_Ref_List r
JOIN (VALUES
    (N'PUBLIC/GENERAL', N'PUBLIC'),
    (N'WOMEN', N'GENERAL-WOMEN'),
    (N'SR CITIZEN', N'SR CITIZEN'),
    (N'SR CITIZEN WOMEN', N'SR CITIZEN-WOMEN'),
    (N'EMPLOYEE', N'EMPLOYEE'),
    (N'EMPLOYEE WOMEN', N'EMPLOYEE-WOMEN')
) v (c, n) ON v.c = r.c_Code
WHERE r.c_List = 'categories' AND JSON_VALUE(ISNULL(r.j_Attrs, N'{}'), '$.rateCategory') IS NULL;
GO
UPDATE r SET j_Attrs = JSON_MODIFY(ISNULL(r.j_Attrs, N'{}'), '$.scheme', v.n), c_Updated_By = 'SEED', d_Updated_On = SYSDATETIME()
FROM dbo.t_Unotp_Ref_List r
JOIN (VALUES
    (N'maturity', N'CUMULATIVE'),
    (N'yearly', N'NON-CUMULATIVE'),
    (N'halfyearly', N'NON-CUMULATIVE'),
    (N'quarterly', N'NON-CUMULATIVE'),
    (N'monthly', N'NON-CUMULATIVE')
) v (c, n) ON v.c = r.c_Code
WHERE r.c_List = 'payouts' AND JSON_VALUE(ISNULL(r.j_Attrs, N'{}'), '$.scheme') IS NULL;
GO
