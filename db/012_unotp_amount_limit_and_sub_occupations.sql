/* =============================================================================
   Uno TP - the deposit maximum and its message, and sub occupations by occupation.

   maxAmount goes to Rs 5 crore, and over it the amount field says what the old
   page said (overMaxAmountMessage). Each occupation lists its own sub occupations
   (subOccupations, in j_Attrs): the page offers those and no other; an occupation
   whose list is empty asks for no sub occupation; one with no list offers all.
   The lists below are a first cut: set them from the business's list.

   Safe to run again: a list already set is left alone.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE dbo.t_Unotp_App_Config SET c_Value = N'50000000' WHERE c_Key = N'maxAmount' AND c_Value = N'20000000';

INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT N'overMaxAmountMessage', N'For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com', N'What the amount field says over maxAmount; blank for "Above the maximum"', 'SEED'
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = N'overMaxAmountMessage');
GO
UPDATE r SET j_Attrs = JSON_MODIFY(ISNULL(r.j_Attrs, N'{}'), '$.subOccupations', JSON_QUERY(v.subs))
FROM dbo.t_Unotp_Ref_List r
JOIN (VALUES
    (N'Salaried',      N'["MMFSL Employee","Private sector","Public sector","Government service"]'),
    (N'Self-employed', N'["Professional"]'),
    (N'Business',      N'["Professional"]'),
    (N'Retired',       N'[]'),
    (N'Homemaker',     N'[]'),
    (N'Student',       N'[]')
) v (code, subs) ON v.code = r.c_Code
WHERE r.c_List = N'occupations' AND JSON_QUERY(ISNULL(r.j_Attrs, N'{}'), '$.subOccupations') IS NULL;
GO
-- The earlier, reversed attribute on the sub occupations list is no longer read.
UPDATE dbo.t_Unotp_Ref_List SET j_Attrs = JSON_MODIFY(j_Attrs, '$.occupations', NULL)
WHERE c_List = N'subOccupations' AND JSON_QUERY(ISNULL(j_Attrs, N'{}'), '$.occupations') IS NOT NULL;
GO
