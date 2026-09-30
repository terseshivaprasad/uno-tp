/* =============================================================================
   Uno TP - the deposit maximum and its message, and sub occupations by occupation.

   maxAmount goes to Rs 5 crore, and over it the amount field says what the old
   page said (overMaxAmountMessage). Each sub occupation lists the occupations it
   goes with (occupations, in j_Attrs); one listing none goes with every
   occupation, and an occupation with none listed asks for no sub occupation.
   The mapping below is a first cut: correct it on the list.

   Safe to run again.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE dbo.t_Unotp_App_Config SET c_Value = N'50000000' WHERE c_Key = N'maxAmount' AND c_Value = N'20000000';

INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT N'overMaxAmountMessage', N'For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com', N'What the amount field says over maxAmount; blank for "Above the maximum"', 'SEED'
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = N'overMaxAmountMessage');
GO
UPDATE r SET j_Attrs = JSON_MODIFY(ISNULL(r.j_Attrs, N'{}'), '$.occupations', JSON_QUERY(v.occupations))
FROM dbo.t_Unotp_Ref_List r
JOIN (VALUES
    (N'MMFSL Employee',     N'["Salaried"]'),
    (N'Private sector',     N'["Salaried"]'),
    (N'Public sector',      N'["Salaried"]'),
    (N'Government service', N'["Salaried"]'),
    (N'Professional',       N'["Self-employed","Business"]')
) v (code, occupations) ON v.code = r.c_Code
WHERE r.c_List = N'subOccupations' AND JSON_QUERY(ISNULL(r.j_Attrs, N'{}'), '$.occupations') IS NULL;
GO
