/* =============================================================================
   Uno TP - the source of funds on FD Configuration.

   Asked once the investor's active deposits with us, with the new one, pass
   sourceOfFundsFrom (Rs 1 crore) and their occupation is one of
   sourceOfFundsOccupations (homemaker, student, retired) or their annual income
   band is one of sourceOfFundsIncomeBands (up to Rs 5 lakh). A choice from the
   sourcesOfFunds list; "other" takes a typed remark. Both are kept on
   t_FD_BT_Investment_Dtl.

   Safe to run again: each change is made only when it is not there yet.
   ============================================================================= */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* ----- The settings ---------------------------------------------------------------- */
INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT v.k, v.v, v.d, 'SEED' FROM (VALUES
    (N'sourceOfFundsFrom', N'10000000', N'The source of funds is asked once the investor''s active deposits, with the new one, pass this many rupees...'),
    (N'sourceOfFundsOccupations', N'Homemaker; Student; Retired', N'...and their occupation is one of these (occupations names, a semicolon between)...'),
    (N'sourceOfFundsIncomeBands', N'Upto Rs.5,00,000', N'...or their annual income band is one of these (incomeBands names, a semicolon between)')
) v (k, v, d)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = v.k);
GO

/* ----- The list ---------------------------------------------------------------------- */
INSERT dbo.t_Unotp_Ref_List (c_List, n_Seq, c_Code, c_Name, j_Attrs, c_Created_By)
SELECT v.l, v.s, v.c, v.n, v.a, 'SEED' FROM (VALUES
    (N'sourcesOfFunds', 1, N'salary', N'Salary', NULL),
    (N'sourcesOfFunds', 2, N'business', N'Business income', NULL),
    (N'sourcesOfFunds', 3, N'savings', N'Savings', NULL),
    (N'sourcesOfFunds', 4, N'property', N'Sale of property', NULL),
    (N'sourcesOfFunds', 5, N'inheritance', N'Inheritance or gift', NULL),
    (N'sourcesOfFunds', 6, N'investments', N'Maturity of investments', NULL),
    (N'sourcesOfFunds', 7, N'loan', N'Loan', NULL),
    (N'sourcesOfFunds', 8, N'other', N'Other', NULL)
) v (l, s, c, n, a)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Ref_List r WHERE r.c_List = v.l AND r.c_Code = v.c);
GO
