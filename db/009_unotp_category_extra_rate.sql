/* =============================================================================
   Uno TP - what each deposit category earns over the public rate.

   The chart's "additional rates" - senior citizens and employees 0.35, women 0.05 -
   on the categories list as extraRate, so FD Configuration can say what a
   category's rate includes. The rate card rows already carry the
   addition; this is for the wording alone.

   Safe to run again: a category that already has extraRate is left alone.
   ============================================================================= */
SET NOCOUNT ON;
GO
UPDATE r SET j_Attrs = JSON_MODIFY(ISNULL(r.j_Attrs, N'{}'), '$.extraRate', v.extra)
FROM dbo.t_Unotp_Ref_List r
JOIN (VALUES
    (N'PUBLIC/GENERAL',   0.00),
    (N'WOMEN',            0.05),
    (N'SR CITIZEN',       0.35),
    (N'SR CITIZEN WOMEN', 0.40),
    (N'EMPLOYEE',         0.35),
    (N'EMPLOYEE WOMEN',   0.40)
) v (code, extra) ON v.code = r.c_Code
WHERE r.c_List = N'categories' AND JSON_VALUE(ISNULL(r.j_Attrs, N'{}'), '$.extraRate') IS NULL;
GO
